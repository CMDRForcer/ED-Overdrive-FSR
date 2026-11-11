// AMD FSR 4 / 3.1 through a D3D12 bridge. See fsr.hpp. EXPERIMENTAL.
//
// The bridge is the one of xess.cpp: per frame, on the game's immediate D3D11
// context and a D3D12 queue of our own on the same GPU,
//   1. D3D11 copies colour, depth (R32_FLOAT), motion vectors and the reactive
//      mask into textures it shares with D3D12, signals a shared fence;
//   2. D3D12 waits for that fence, runs FSR (inputs in the non-pixel shader
//      resource state, output in the UAV state, back to COMMON afterwards),
//      signals the fence;
//   3. D3D11 waits for it and copies the shared output into in.output.
// The image is needed in the same frame, so D3D11 waits for FSR on the GPU,
// exactly as for XeSS. The CPU only waits when it would reuse a command
// allocator still in use (never in practice).
//
// amd_fidelityfx_loader_dx12.dll is loaded at run time from the add-on folder
// (no import library, no delay-load); it picks the newest FSR that runs on this
// GPU from amd_fidelityfx_upscaler_dx12.dll: FSR 4 on Radeon RX 6000 and newer,
// FSR 3.1 elsewhere. Without the DLLs the add-on still loads and FSR reports
// itself unavailable.

#include "fsr.hpp"

#include "state.hpp"

#include <atomic>
#include <cmath>
#include <cstdio>
#include <mutex>
#include <Windows.h>
#include <d3d11_4.h>
#include <d3d12.h>
#include <dxgi1_2.h>
#include <wrl/client.h>

#if EDAA_FSR
#include <dx12/ffx_api_dx12.h>
#include <ffx_upscale.h>
#endif

using Microsoft::WRL::ComPtr;

namespace edaa::fsr
{
#if EDAA_FSR
    namespace
    {
        std::atomic<int> probed{ -1 }; // probe() result
        std::mutex status_mutex; // the overlay reads the status and info from its own thread
        std::string status_text = "not used yet";
        std::string version_text;
        upscale_info last_info;
        uint32_t info_w = 0, info_h = 0; // display size of last_info

        // FSR's quality modes: display size / render size (AMD's fixed ratios).
        struct quality_mode
        {
            float ratio;
            const char* name;
        };

        const quality_mode scaling_modes[] = {
            { 1.5f, "Quality" },
            { 1.7f, "Balanced" },
            { 2.0f, "Performance" },
            { 3.0f, "Ultra Performance" },
        };


        // The FidelityFX API, from amd_fidelityfx_loader_dx12.dll.
        struct api
        {
            HMODULE module = nullptr;
            PfnFfxCreateContext create = nullptr;
            PfnFfxDestroyContext destroy = nullptr;
            PfnFfxConfigure configure = nullptr;
            PfnFfxQuery query = nullptr;
            PfnFfxDispatch dispatch = nullptr;
        };

        api ffx;


        void set_status(const std::string& text)
        {
            const std::lock_guard<std::mutex> lock(status_mutex);
            status_text = text;
        }


        const char* result_name(ffxReturnCode_t result)
        {
            switch (result) {
                case FFX_API_RETURN_ERROR_UNKNOWN_DESCTYPE: return "unknown description type";
                case FFX_API_RETURN_ERROR_RUNTIME_ERROR: return "D3D12 runtime error";
                case FFX_API_RETURN_NO_PROVIDER: return "no FSR provider for this GPU";
                case FFX_API_RETURN_ERROR_MEMORY: return "out of memory";
                case FFX_API_RETURN_ERROR_PARAMETER: return "invalid parameter";
                default: return "error";
            }
        }


        bool failed(const char* what, ffxReturnCode_t result)
        {
            if (result == FFX_API_RETURN_OK) {
                return false;
            }

            char text[160];
            snprintf(text, sizeof(text), "%s: %s (FidelityFX %u)", what, result_name(result), static_cast<unsigned>(result));
            set_status(text);
            reshade::log::message(reshade::log::level::warning, (std::string("EDAA: FSR ") + text).c_str());
            return true;
        }


        bool failed(const char* what, HRESULT hr)
        {
            if (SUCCEEDED(hr)) {
                return false;
            }

            char text[160];
            snprintf(text, sizeof(text), "%s (HRESULT 0x%08x)", what, static_cast<unsigned>(hr));
            set_status(text);
            return true;
        }


        // Messages of the FidelityFX runtime into ReShade.log.
        void on_message(uint32_t type, const wchar_t* message)
        {
            char text[512];
            snprintf(text, sizeof(text), "EDAA: FSR runtime: %ls", message != nullptr ? message : L"");
            reshade::log::message(type == FFX_API_MESSAGE_TYPE_ERROR ? reshade::log::level::error : reshade::log::level::warning, text);
        }


        // A texture created by D3D11 and opened by D3D12.
        struct shared_texture
        {
            ComPtr<ID3D12Resource> d3d12;
            ComPtr<ID3D11Texture2D> d3d11;
        };


        // What the shared textures and the FSR context were made for.
        struct setup
        {
            uint32_t in_w = 0, in_h = 0, out_w = 0, out_h = 0;
            uint32_t flags = 0;

            bool operator==(const setup&) const = default;
        };


        constexpr uint32_t allocator_count = 8; // see xess.cpp


        struct bridge
        {
            ID3D11Device* device11 = nullptr; // the game's device, not owned
            ComPtr<ID3D11Device5> device11_5;
            ComPtr<ID3D12Device> device12;
            ComPtr<ID3D12CommandQueue> queue;
            ComPtr<ID3D12CommandAllocator> allocators[allocator_count];
            ComPtr<ID3D12GraphicsCommandList> list;
            ComPtr<ID3D12Fence> fence12;
            ComPtr<ID3D11Fence> fence11; // the same fence, opened by D3D11
            HANDLE fence_event = nullptr;
            uint64_t fence_value = 0;
            uint64_t allocator_done[allocator_count] = {};
            uint32_t frame = 0;
            LARGE_INTEGER last_dispatch = {};

            shared_texture color, depth, motion, output, mask;
            ComPtr<ID3D11RenderTargetView> mask_rtv; // clears the mask when there is none this frame
            setup current;
            ffxContext context = nullptr;
        };

        bridge b;
        bool init_failed = false;


        void wait_for(uint64_t value)
        {
            if (b.fence12 && b.fence12->GetCompletedValue() < value) {
                b.fence12->SetEventOnCompletion(value, b.fence_event);
                WaitForSingleObject(b.fence_event, 5000);
            }
        }


        void destroy_context()
        {
            if (b.context != nullptr) {
                ffx.destroy(&b.context, nullptr);
                b.context = nullptr;
            }
        }


        void shutdown()
        {
            wait_for(b.fence_value);
            destroy_context();

            if (b.fence_event != nullptr) {
                CloseHandle(b.fence_event);
            }

            b = {};
            info_w = info_h = 0;
        }


        bool library_loaded()
        {
            if (ffx.module != nullptr) {
                return true;
            }

            const std::wstring path = addon_folder() + L"\\amd_fidelityfx_loader_dx12.dll";
            const HMODULE module = LoadLibraryExW(path.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);

            if (module == nullptr) {
                set_status("amd_fidelityfx_loader_dx12.dll not found next to EDAA.addon64");
                return false;
            }

            api loaded;
            loaded.module = module;
            loaded.create = reinterpret_cast<PfnFfxCreateContext>(GetProcAddress(module, "ffxCreateContext"));
            loaded.destroy = reinterpret_cast<PfnFfxDestroyContext>(GetProcAddress(module, "ffxDestroyContext"));
            loaded.configure = reinterpret_cast<PfnFfxConfigure>(GetProcAddress(module, "ffxConfigure"));
            loaded.query = reinterpret_cast<PfnFfxQuery>(GetProcAddress(module, "ffxQuery"));
            loaded.dispatch = reinterpret_cast<PfnFfxDispatch>(GetProcAddress(module, "ffxDispatch"));

            if (!loaded.create || !loaded.destroy || !loaded.configure || !loaded.query || !loaded.dispatch) {
                FreeLibrary(module);
                set_status("amd_fidelityfx_loader_dx12.dll is not the FidelityFX API loader");
                return false;
            }

            ffx = loaded;
            return true;
        }


        // D3D12 device on the game's GPU, queue, command list, shared fence (as in xess.cpp).
        bool create_bridge(ID3D11Device* device)
        {
            if (!library_loaded()) {
                return false;
            }

            if (failed("Windows 10 1703 or newer needed (ID3D11Device5)", device->QueryInterface(IID_PPV_ARGS(&b.device11_5)))) {
                return false;
            }

            ComPtr<IDXGIDevice> dxgi;
            ComPtr<IDXGIAdapter> adapter;

            if (failed("cannot get the game's GPU", device->QueryInterface(IID_PPV_ARGS(&dxgi))) ||
                failed("cannot get the game's GPU", dxgi->GetAdapter(&adapter))) {
                return false;
            }

            if (failed("cannot create a D3D12 device on the game's GPU",
                    D3D12CreateDevice(adapter.Get(), D3D_FEATURE_LEVEL_12_0, IID_PPV_ARGS(&b.device12)))) {
                return false;
            }

            D3D12_COMMAND_QUEUE_DESC queue = {};
            queue.Type = D3D12_COMMAND_LIST_TYPE_DIRECT;
            queue.Priority = D3D12_COMMAND_QUEUE_PRIORITY_HIGH;

            if (failed("D3D12 queue creation failed", b.device12->CreateCommandQueue(&queue, IID_PPV_ARGS(&b.queue)))) {
                return false;
            }

            for (ComPtr<ID3D12CommandAllocator>& allocator : b.allocators) {
                if (failed("D3D12 allocator creation failed", b.device12->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&allocator)))) {
                    return false;
                }
            }

            if (failed("D3D12 command list creation failed",
                    b.device12->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, b.allocators[0].Get(), nullptr, IID_PPV_ARGS(&b.list)))) {
                return false;
            }

            b.list->Close();
            HANDLE handle = nullptr;

            if (failed("shared fence creation failed", b.device12->CreateFence(0, D3D12_FENCE_FLAG_SHARED, IID_PPV_ARGS(&b.fence12))) ||
                failed("shared fence creation failed", b.device12->CreateSharedHandle(b.fence12.Get(), nullptr, GENERIC_ALL, nullptr, &handle))) {
                return false;
            }

            const HRESULT opened = b.device11_5->OpenSharedFence(handle, IID_PPV_ARGS(&b.fence11));
            CloseHandle(handle);

            if (failed("D3D11 cannot open the shared fence", opened)) {
                return false;
            }

            b.fence_event = CreateEventW(nullptr, FALSE, FALSE, nullptr);
            b.device11 = device;
            return true;
        }


        bool init(ID3D11Device* device)
        {
            if (b.device11 == device && b.device12) {
                return true;
            }

            if (b.device11 != nullptr) {
                shutdown();
                init_failed = false;
            }

            if (init_failed) {
                return false;
            }

            if (!create_bridge(device)) {
                shutdown();
                init_failed = true;
                return false;
            }

            return true;
        }


        bool create_shared(const char* name, uint32_t width, uint32_t height, DXGI_FORMAT format, UINT bind_flags, shared_texture& t)
        {
            D3D11_TEXTURE2D_DESC desc = {};
            desc.Width = width;
            desc.Height = height;
            desc.MipLevels = 1;
            desc.ArraySize = 1;
            desc.Format = format;
            desc.SampleDesc.Count = 1;
            desc.BindFlags = D3D11_BIND_SHADER_RESOURCE | D3D11_BIND_RENDER_TARGET | bind_flags;
            desc.MiscFlags = D3D11_RESOURCE_MISC_SHARED_NTHANDLE | D3D11_RESOURCE_MISC_SHARED;
            char what[96];
            snprintf(what, sizeof(what), "cannot create the shared %s texture", name);

            if (failed(what, b.device11_5->CreateTexture2D(&desc, nullptr, &t.d3d11))) {
                return false;
            }

            ComPtr<IDXGIResource1> resource;
            HANDLE handle = nullptr;

            if (failed(what, t.d3d11.As(&resource)) ||
                failed(what, resource->CreateSharedHandle(nullptr, DXGI_SHARED_RESOURCE_READ | DXGI_SHARED_RESOURCE_WRITE, nullptr, &handle))) {
                return false;
            }

            const HRESULT opened = b.device12->OpenSharedHandle(handle, IID_PPV_ARGS(&t.d3d12));
            CloseHandle(handle);
            snprintf(what, sizeof(what), "D3D12 cannot open the shared %s texture", name);
            return !failed(what, opened);
        }


        // The FSR context for these sizes and flags; the version AMD's DLL chose.
        bool create_context(const setup& want)
        {
            ffxCreateBackendDX12Desc backend = {};
            backend.header.type = FFX_API_CREATE_CONTEXT_DESC_TYPE_BACKEND_DX12;
            backend.device = b.device12.Get();

            ffxCreateContextDescUpscale desc = {};
            desc.header.type = FFX_API_CREATE_CONTEXT_DESC_TYPE_UPSCALE;
            desc.header.pNext = &backend.header;
            desc.flags = want.flags;
            desc.maxRenderSize = { want.in_w, want.in_h };
            desc.maxUpscaleSize = { want.out_w, want.out_h };
            desc.fpMessage = on_message;

            if (failed("FSR context creation failed", ffx.create(&b.context, &desc.header, nullptr))) {
                b.context = nullptr;
                return false;
            }

            ffxQueryGetProviderVersion provider = {};
            provider.header.type = FFX_API_QUERY_DESC_TYPE_GET_PROVIDER_VERSION;

            if (ffx.query(&b.context, &provider.header) == FFX_API_RETURN_OK && provider.versionName != nullptr) {
                const std::lock_guard<std::mutex> lock(status_mutex);
                version_text = std::string("FSR ") + provider.versionName;
            }

            return true;
        }


        bool resize(const setup& want)
        {
            if (b.current == want && b.output.d3d11 && b.context != nullptr) {
                return true;
            }

            wait_for(b.fence_value); // FSR must be idle before its context goes
            destroy_context();
            b.color = b.depth = b.motion = b.output = b.mask = {};
            b.mask_rtv.Reset();
            b.current = {};
            const bool hdr = (want.flags & FFX_UPSCALE_ENABLE_HIGH_DYNAMIC_RANGE) != 0;

            if (!create_shared("colour", want.in_w, want.in_h, hdr ? DXGI_FORMAT_R11G11B10_FLOAT : DXGI_FORMAT_R8G8B8A8_UNORM, 0, b.color) ||
                !create_shared("depth", want.in_w, want.in_h, DXGI_FORMAT_R32_FLOAT, 0, b.depth) ||
                !create_shared("motion", want.in_w, want.in_h, DXGI_FORMAT_R16G16_FLOAT, 0, b.motion) ||
                !create_shared("output", want.out_w, want.out_h, hdr ? DXGI_FORMAT_R16G16B16A16_FLOAT : DXGI_FORMAT_R8G8B8A8_UNORM,
                    D3D11_BIND_UNORDERED_ACCESS, b.output) ||
                !create_shared("reactive mask", want.in_w, want.in_h, DXGI_FORMAT_R8_UNORM, 0, b.mask) ||
                failed("cannot create the mask view", b.device11->CreateRenderTargetView(b.mask.d3d11.Get(), nullptr, &b.mask_rtv))) {
                return false;
            }

            if (!create_context(want)) {
                return false;
            }

            b.current = want;
            char text[160];
            snprintf(text, sizeof(text), "EDAA: %s initialised, %ux%u -> %ux%u, flags 0x%x", version().c_str(), want.in_w, want.in_h, want.out_w,
                want.out_h, want.flags);
            reshade::log::message(reshade::log::level::info, text);
            return true;
        }


        void transition(ID3D12Resource* resource, D3D12_RESOURCE_STATES from, D3D12_RESOURCE_STATES to, D3D12_RESOURCE_BARRIER& barrier)
        {
            barrier = {};
            barrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
            barrier.Transition.pResource = resource;
            barrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
            barrier.Transition.StateBefore = from;
            barrier.Transition.StateAfter = to;
        }


        // Inputs and output between COMMON (shared with D3D11) and the states FSR is told about.
        void transitions(bool to_fsr)
        {
            const D3D12_RESOURCE_STATES input = D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE;
            const D3D12_RESOURCE_STATES common = D3D12_RESOURCE_STATE_COMMON;
            const D3D12_RESOURCE_STATES uav = D3D12_RESOURCE_STATE_UNORDERED_ACCESS;
            D3D12_RESOURCE_BARRIER barriers[5];
            transition(b.color.d3d12.Get(), to_fsr ? common : input, to_fsr ? input : common, barriers[0]);
            transition(b.depth.d3d12.Get(), to_fsr ? common : input, to_fsr ? input : common, barriers[1]);
            transition(b.motion.d3d12.Get(), to_fsr ? common : input, to_fsr ? input : common, barriers[2]);
            transition(b.mask.d3d12.Get(), to_fsr ? common : input, to_fsr ? input : common, barriers[3]);
            transition(b.output.d3d12.Get(), to_fsr ? common : uav, to_fsr ? uav : common, barriers[4]);
            b.list->ResourceBarrier(5, barriers);
        }


        // Milliseconds since the last FSR dispatch, for FSR's history weighting.
        float frame_time_ms()
        {
            LARGE_INTEGER now, frequency;
            QueryPerformanceCounter(&now);
            QueryPerformanceFrequency(&frequency);
            const float ms = b.last_dispatch.QuadPart != 0
                ? static_cast<float>(static_cast<double>(now.QuadPart - b.last_dispatch.QuadPart) * 1000.0 / static_cast<double>(frequency.QuadPart))
                : 16.7f;
            b.last_dispatch = now;
            return ms < 1.0f ? 1.0f : ms > 100.0f ? 100.0f : ms;
        }


        bool run(ID3D11Device* device, ID3D11DeviceContext* ctx, const upscaler_inputs& in, const setup& want)
        {
            if (!init(device)) {
                return false;
            }

            if (!resize(want)) {
                shutdown(); // keep the error in the status, do not retry every frame
                init_failed = true;
                return false;
            }

            ComPtr<ID3D11DeviceContext4> ctx4;

            if (failed("Windows 10 1703 or newer needed (ID3D11DeviceContext4)", ctx->QueryInterface(IID_PPV_ARGS(&ctx4)))) {
                return false;
            }

            // 1. D3D11: inputs into the shared textures, then hand over to D3D12.
            ctx->CopyResource(b.color.d3d11.Get(), in.color);
            ctx->CopyResource(b.depth.d3d11.Get(), in.depth);
            ctx->CopyResource(b.motion.d3d11.Get(), in.motion);

            if (in.hud_mask != nullptr) {
                ctx->CopyResource(b.mask.d3d11.Get(), in.hud_mask);
            }
            else {
                const float none[4] = {};
                ctx->ClearRenderTargetView(b.mask_rtv.Get(), none);
            }

            const uint64_t inputs_ready = ++b.fence_value;
            ctx4->Signal(b.fence11.Get(), inputs_ready);

            if (state().xess_flush.load(std::memory_order_relaxed)) {
                ctx->Flush();
            }

            // 2. D3D12: FSR on our queue, after the D3D11 copies.
            const uint32_t slot = b.frame++ % allocator_count;
            wait_for(b.allocator_done[slot]);
            b.allocators[slot]->Reset();
            b.list->Reset(b.allocators[slot].Get(), nullptr);
            transitions(true);

            const bool hdr = (want.flags & FFX_UPSCALE_ENABLE_HIGH_DYNAMIC_RANGE) != 0;
            ffxDispatchDescUpscale d = {};
            d.header.type = FFX_API_DISPATCH_DESC_TYPE_UPSCALE;
            d.commandList = b.list.Get();
            d.color = ffxApiGetResourceDX12(b.color.d3d12.Get(), FFX_API_RESOURCE_STATE_COMPUTE_READ);
            d.depth = ffxApiGetResourceDX12(b.depth.d3d12.Get(), FFX_API_RESOURCE_STATE_COMPUTE_READ);
            d.motionVectors = ffxApiGetResourceDX12(b.motion.d3d12.Get(), FFX_API_RESOURCE_STATE_COMPUTE_READ);
            d.reactive = ffxApiGetResourceDX12(b.mask.d3d12.Get(), FFX_API_RESOURCE_STATE_COMPUTE_READ);
            d.output = ffxApiGetResourceDX12(b.output.d3d12.Get(), FFX_API_RESOURCE_STATE_UNORDERED_ACCESS, FFX_API_RESOURCE_USAGE_UAV);
            // Viewport offset in pixels, as for DLSS and XeSS; motion vectors in UV (prev - current) -> pixels.
            d.jitterOffset = { in.jitter_x, in.jitter_y };
            d.motionVectorScale = { static_cast<float>(in.width), static_cast<float>(in.height) };
            d.renderSize = { in.width, in.height };
            d.upscaleSize = { want.out_w, want.out_h };
            d.enableSharpening = in.sharpness > 0.0f;
            d.sharpness = in.sharpness < 0.0f ? 0.0f : in.sharpness > 1.0f ? 1.0f : in.sharpness;
            d.frameTimeDelta = frame_time_ms();
            d.preExposure = 1.0f;
            d.reset = in.reset;
            // Reverse-Z with an infinite far plane: FidelityFX takes near = FLT_MAX, far = the near plane.
            d.cameraNear = 3.402823466e+38f;
            d.cameraFar = in.near_plane > 0.0f ? in.near_plane : 0.025f;
            d.cameraFovAngleVertical = in.vfov_rad > 0.0f ? in.vfov_rad : 1.0f;
            d.viewSpaceToMetersFactor = 1.0f;
            d.flags = hdr ? 0u : static_cast<uint32_t>(FFX_UPSCALE_FLAG_NON_LINEAR_COLOR_SRGB);
            const ffxReturnCode_t result = ffx.dispatch(&b.context, &d.header);

            transitions(false);
            b.list->Close();
            b.queue->Wait(b.fence12.Get(), inputs_ready);
            ID3D12CommandList* const lists[] = { b.list.Get() };
            b.queue->ExecuteCommandLists(1, lists);
            const uint64_t output_ready = ++b.fence_value;
            b.queue->Signal(b.fence12.Get(), output_ready);
            b.allocator_done[slot] = output_ready;

            // 3. D3D11: wait for FSR on the GPU, take the output.
            ctx4->Wait(b.fence11.Get(), output_ready);

            if (failed("FSR dispatch failed", result)) {
                return false;
            }

            ctx->CopyResource(in.output, b.output.d3d11.Get());
            return true;
        }


        // FSR's modes for a display size (fixed ratios; FSR accepts any render size up to the display size).
        const upscale_info& modes_for(uint32_t display_w, uint32_t display_h)
        {
            if (info_w == display_w && info_h == display_h) {
                return last_info;
            }

            upscale_info info;

            for (const quality_mode& m : scaling_modes) {
                upscale_mode mode;
                mode.name = m.name;
                mode.optimal_w = static_cast<uint32_t>(std::lround(display_w / m.ratio));
                mode.optimal_h = static_cast<uint32_t>(std::lround(display_h / m.ratio));
                mode.min_w = static_cast<uint32_t>(std::lround(display_w / 3.0f));
                mode.min_h = static_cast<uint32_t>(std::lround(display_h / 3.0f));
                mode.max_w = display_w;
                mode.max_h = display_h;
                info.modes.push_back(mode);
            }

            const std::lock_guard<std::mutex> lock(status_mutex);
            last_info = info;
            info_w = display_w;
            info_h = display_h;
            return last_info;
        }
    }


    bool evaluate(ID3D11Device* device, ID3D11DeviceContext* ctx, const upscaler_inputs& in)
    {
        // Native AA: HDR scene before exposure (FSR's auto exposure), reverse-Z infinite depth.
        setup want;
        want.in_w = want.out_w = in.width;
        want.in_h = want.out_h = in.height;
        want.flags = FFX_UPSCALE_ENABLE_HIGH_DYNAMIC_RANGE | FFX_UPSCALE_ENABLE_AUTO_EXPOSURE | FFX_UPSCALE_ENABLE_DEPTH_INVERTED |
            FFX_UPSCALE_ENABLE_DEPTH_INFINITE;

        if (!run(device, ctx, in, want)) {
            return false;
        }

        set_status("running (Native AA, D3D12 bridge)");
        return true;
    }


    bool upscale(ID3D11Device* device, ID3D11DeviceContext* ctx, const upscaler_inputs& in)
    {
        // Tonemapped (sRGB) image, reverse-Z infinite depth, the cockpit HUD as reactive mask.
        setup want;
        want.in_w = in.width;
        want.in_h = in.height;
        want.out_w = in.output_width;
        want.out_h = in.output_height;
        want.flags = FFX_UPSCALE_ENABLE_DEPTH_INVERTED | FFX_UPSCALE_ENABLE_DEPTH_INFINITE | FFX_UPSCALE_ENABLE_NON_LINEAR_COLORSPACE;

        if (!run(device, ctx, in, want)) {
            return false;
        }

        // The mode whose ratio is nearest to this frame's.
        const upscale_info& modes = modes_for(in.output_width, in.output_height);
        const float ratio = static_cast<float>(in.output_width) / static_cast<float>(in.width);
        const char* nearest = "";
        float distance = 1e9f;

        for (const quality_mode& m : scaling_modes) {
            if (std::fabs(m.ratio - ratio) < distance) {
                distance = std::fabs(m.ratio - ratio);
                nearest = m.name;
            }
        }

        {
            const std::lock_guard<std::mutex> lock(status_mutex);
            last_info.mode = modes.modes.empty() ? "" : nearest;
        }

        char text[96];
        snprintf(text, sizeof(text), "running (upscaling %.2fx, D3D12 bridge)", ratio);
        set_status(text);
        return true;
    }


    upscale_info info()
    {
        const std::lock_guard<std::mutex> lock(status_mutex);
        return last_info;
    }


    void probe(ID3D11Device* device)
    {
        // The bridge and a small context once, to learn whether FSR runs and
        // which version AMD's DLL picks; then freed (players who do not use FSR
        // should not keep a D3D12 device).
        bool ok = init(device);

        if (ok) {
            setup tiny = { 64, 64, 64, 64, FFX_UPSCALE_ENABLE_HIGH_DYNAMIC_RANGE | FFX_UPSCALE_ENABLE_DEPTH_INVERTED };
            ok = create_context(tiny);
        }

        probed = ok ? 1 : 0;

        if (ok) {
            const std::string v = version();
            reshade::log::message(reshade::log::level::info, ("EDAA: AMD FSR available (" + (v.empty() ? std::string("version unknown") : v) + ", D3D12 bridge)").c_str());
            shutdown();
            set_status("available (D3D12 bridge)");
        }
        else {
            shutdown();
        }
    }


    int availability()
    {
        return probed;
    }


    std::string version()
    {
        const std::lock_guard<std::mutex> lock(status_mutex);
        return version_text;
    }


    std::string status()
    {
        const std::lock_guard<std::mutex> lock(status_mutex);
        return status_text;
    }


    void release(ID3D11Device* device)
    {
        if (b.device11 != nullptr && (device == nullptr || device == b.device11)) {
            shutdown();
            set_status("not used yet");
        }

        init_failed = false;
    }


#else
    bool evaluate(ID3D11Device*, ID3D11DeviceContext*, const upscaler_inputs&)
    {
        return false;
    }


    bool upscale(ID3D11Device*, ID3D11DeviceContext*, const upscaler_inputs&)
    {
        return false;
    }


    upscale_info info()
    {
        return {};
    }


    void probe(ID3D11Device*)
    {
    }


    int availability()
    {
        return 0;
    }


    std::string version()
    {
        return {};
    }


    std::string status()
    {
        return "this build has no FSR (FidelityFX SDK not found at build time)";
    }


    void release(ID3D11Device*)
    {
    }
#endif
}
