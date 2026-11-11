# EDAA - Temporal Anti-Aliasing for Elite Dangerous

A ReShade add-on that adds TAA (Temporal Anti-Aliasing) to Elite Dangerous
Odyssey (DX11): sub-pixel jitter, motion vectors from the game's camera and
depth, and a TAA resolve inserted into the game's frame before the cockpit
HUD, bloom and tonemapping. NVIDIA DLAA (DLSS at native resolution, RTX
GPUs), AMD FSR 4 / 3.1 (any GPU with D3D12; AMD's DLL picks FSR 4 on Radeon
RX 6000 and newer) or Intel XeSS Native AA (any recent GPU) can replace the
EDAA TAA at the same point; below 1.0 supersampling the same method upscales.

**Frame Generation (added in ED-Overdrive):** the final image is presented
through a D3D12 swap chain, with EDAA's depth and motion vectors and the
cockpit HUD kept steady. Two backends:

| | NVIDIA DLSS-G | AMD FSR Frame Generation (experimental) |
|---|---|---|
| GPUs | RTX 40/50 | any GPU with D3D12 |
| Frame limit | Reflex | EDAA's own |
| Own ship | game stencil | game stencil + automatic detection, optional distance |
| In the build | NVIDIA | NVIDIA and AMD |

Turn it on in the overlay (Pos1, EDAA, "Frame Generation"), restart the game
once, and use its frame limit (just below the refresh rate, e.g. 140 at
144 Hz) instead of any other limiter. Needs Windows hardware-accelerated GPU
scheduling.

**Two builds** from the same source, each with everything that runs on its
vendor's GPUs: NVIDIA (`EDAA-<version>.zip`) and AMD
(`EDAA-<version>-amd.zip`, no NVIDIA DLLs). See
[docs/vendors.md](docs/vendors.md).

* Original project by Siegfried-Origin: https://github.com/Siegfried-Origin/EDAA
  (website: https://siegfried-origin.github.io/EDAA/); this copy adds Frame Generation, AMD FSR and the vendor builds
* How it works, settings, diagnostics: [docs/addon.md](docs/addon.md)
* The game's rendering, reverse-engineered: [docs/reverse-engineering.md](docs/reverse-engineering.md)
* Finding camera matrices in any DX11 game: [docs/finding-camera-matrices.md](docs/finding-camera-matrices.md)
* Motion vectors design: [docs/motion-vectors.md](docs/motion-vectors.md)
* Analysis scripts (RenderDoc): [tools/README.md](tools/README.md)

## Install

The easiest way is **ED-Overdrive** (the app in this repository, see the
[main README](../README.md)): it installs ReShade and EDAA, keeps backups
and sets everything up.

### By hand

Game folder (Steam): `C:\Program Files (x86)\Steam\steamapps\common\Elite Dangerous\Products\elite-dangerous-odyssey-64`

1. Download **ReShade 6.8.0 or newer, "with full add-on support"** from
   https://reshade.me (the normal build does not load add-ons).
2. Run it, select `EliteDangerous64.exe` in the game folder and the API
   **DirectX 10/11/12**. It installs `dxgi.dll` and `ReShade.ini`. Skip the
   effect packages (none are needed).
3. Copy everything from the zip for your GPU (attached to each
   [release](https://github.com/CMDRForcer/ED-Overdrive-FSR/releases))
   next to `dxgi.dll`:

   | | NVIDIA `EDAA-<version>.zip` | AMD `EDAA-<version>-amd.zip` |
   |---|---|---|
   | Add-on | `EDAA.addon64` | `EDAA.addon64` |
   | AMD FSR and FSR FG | `amd_fidelityfx_*.dll` | `amd_fidelityfx_*.dll` |
   | Intel XeSS | `libxess.dll` | `libxess.dll` |
   | NVIDIA DLAA | `nvngx_dlss.dll` | – |
   | NVIDIA DLSS-G | `sl.*.dll`, `nvngx_dlssg.dll` | – |

4. Set the game's anti-aliasing to off and start the game.

## Uninstall

Close the game and delete the files from step 3. To remove ReShade too,
delete `dxgi.dll`, `ReShade.ini`, `ReShade.log` and the `reshade-shaders`
folder if present. Do not use the ReShade setup's "Uninstall" when EDHM is
installed: it also deletes EDHM's `d3d11.dll`.

## Building

Visual Studio 2022 (C++), CMake 3.20+ and the Windows SDK (fxc):

```
git submodule update --init --depth 1
cmake -S edaa -B edaa/build -G "Visual Studio 17 2022" -A x64
cmake --build edaa/build --config Release
```

AMD build: `-B edaa/build-amd -DEDAA_VENDOR=amd` (no DLSS or Streamline SDK
needed). NVIDIA build with DLSS-G: extract the
[Streamline SDK 2.14.16](https://github.com/NVIDIA-RTX/Streamline/releases/tag/v2.14.16)
to `edaa/third_party/streamline` first. The output is in
`edaa/build/Release` (or `edaa/build-amd/Release`). CI builds both for every
push and pull request
([.github/workflows/build.yml](../.github/workflows/build.yml)) and attaches
both zips to every release.

## Hotkeys

| Keys | Action |
|---|---|
| `Ctrl+Shift+T` | TAA on/off |

Developer builds (`-DEDAA_DEVELOPER=ON`, see [docs/addon.md](docs/addon.md))
add `Ctrl+Shift+J` (jitter alone), `Ctrl+Shift+M` (debug views) and
`Ctrl+Shift+C` (auto capture to `Documents\EDAA-captures`).

## License

EDAA is released under the [MIT License](LICENSE). The third-party
components below keep their own licenses.

## Third-party components

ReShade (BSD 3-Clause), Dear ImGui (MIT), the NVIDIA DLSS SDK (NVIDIA RTX
SDKs license, NVIDIA build only), the AMD FidelityFX SDK (AMD FidelityFX
License) and the Intel XeSS SDK (Intel Simplified Software License) are git
submodules in `third_party/`; see
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). NVIDIA and DLSS are
trademarks of NVIDIA Corporation, AMD, Radeon, FidelityFX and FSR of
Advanced Micro Devices, Inc., Intel and XeSS of Intel Corporation; EDAA is
not sponsored or endorsed by NVIDIA, AMD or Intel.
