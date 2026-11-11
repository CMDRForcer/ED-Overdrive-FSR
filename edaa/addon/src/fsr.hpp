// AMD FSR (FidelityFX Super Resolution) through the AMD FidelityFX API: FSR 4 on
// the GPUs AMD's upscaler DLL supports it on (Radeon RX 6000 and newer), FSR 3.1
// on every other GPU. Native AA (render size = output size) as an alternative to
// the EDAA TAA resolve, or upscaling in place of the game's final upscale.
// Built in when the FidelityFX SDK is present (submodule third_party/fidelityfx,
// EDAA_FSR=1). EXPERIMENTAL.
//
// The FidelityFX API has no D3D11 backend, so FSR runs on D3D12 through the same
// kind of bridge as XeSS (xess.cpp): a D3D12 device on the same adapter, textures
// and a fence shared with the game's D3D11 device (fsr.cpp).

#pragma once

#include "upscaler.hpp"

#include <string>

struct ID3D11Device;
struct ID3D11DeviceContext;

namespace edaa::fsr
{
    // Runs FSR Native AA. Returns true on success; otherwise the caller falls
    // back to the EDAA TAA and status() says why. Render thread only.
    bool evaluate(ID3D11Device* device, ID3D11DeviceContext* ctx, const upscaler_inputs& in);

    // Upscaling (in.output_width/height larger than the render size), the
    // tonemapped image, in.hud_mask as reactive mask. Same return convention as evaluate().
    bool upscale(ID3D11Device* device, ID3D11DeviceContext* ctx, const upscaler_inputs& in);

    // FSR's quality modes for the last display size and the one nearest to the render size. Any thread.
    upscale_info info();

    // Checks once per device whether FSR can run (AMD DLLs, D3D12 bridge) and
    // which FSR version AMD's DLL picks for this GPU. Render thread.
    void probe(ID3D11Device* device);

    // Result of probe(): -1 not checked yet, 0 unavailable, 1 available. Any thread.
    int availability();

    // The FSR version in use ("FSR 4.1.2", "FSR 3.1.6"), "" before probe(). Any thread.
    std::string version();

    // State for the overlay. Any thread.
    std::string status();

    // Waits for pending work, destroys the FSR context and the D3D12 bridge.
    void release(ID3D11Device* device);
}
