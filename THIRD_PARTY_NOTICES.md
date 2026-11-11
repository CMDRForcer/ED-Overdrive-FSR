# Third-party notices

ED-Overdrive (MIT, see `LICENSE`) ships the following parts inside `ED-Overdrive.exe`. Their full license texts are in [`licenses/`](licenses);
the app also copies them to `ED-Overdrive-Lizenzen` in the game folder when it installs.

| Component | Version | Source | License | License file |
|---|---|---|---|---|
| EDAA | see `edaa/VERSION` | `edaa/` in this repository, based on https://github.com/Siegfried-Origin/EDAA | MIT, Copyright 2026 Siegfried Origin | `EDAA-LICENSE.txt` |
| ReShade (add-on build, `ReShade64.dll`) | 6.8.0 | https://reshade.me, https://github.com/crosire/reshade | BSD-3-Clause, Copyright 2014 Patrick Mours | `ReShade-LICENSE.md` |
| Dear ImGui (inside EDAA) | – | https://github.com/ocornut/imgui | MIT | `ImGui-LICENSE.txt` |
| NVIDIA Streamline (`sl.*.dll`) | 2.14.16 | https://github.com/NVIDIAGameWorks/Streamline | MIT / NVIDIA RTX SDKs License | `NVIDIA-Streamline-LICENSE.txt`, `NVIDIA-RTX-SDKs-LICENSE.txt` |
| NVIDIA DLSS (`nvngx_dlss.dll`, `nvngx_dlssg.dll`) | 310.9.16 | https://github.com/NVIDIA/DLSS | NVIDIA RTX SDKs / NGX SDK License | `NVIDIA-RTX-SDKs-LICENSE.txt`, `NVIDIA-NGX-SDK-LICENSE.txt` |
| Intel XeSS (`libxess.dll`) | – | https://github.com/intel/xess | Intel Simplified Software License | `Intel-XeSS-LICENSE.txt`, `Intel-XeSS-third-party-programs.txt` |
| AMD FSR 4 / 3.1 (`amd_fidelityfx_loader_dx12.dll`, `amd_fidelityfx_upscaler_dx12.dll`), experimental | FidelityFX SDK 2.4.0 (FSR 4.1.2 / 3.1.6) | https://github.com/GPUOpen-LibrariesAndSDKs/FidelityFX-SDK | AMD FidelityFX license (binary redistribution, no reverse engineering) | `AMD-FidelityFX-LICENSE.md` |
| GITC Uplift | 1.8.0 | https://github.com/ghostinthecamera/GITC-Uplift | MIT | `GITC-Uplift-LICENSE.txt` |

The NVIDIA, Intel and AMD binaries are distributed only as part of EDAA, unmodified, not as stand-alone products.

## Downloaded at setup, not shipped

| Component | Source | License |
|---|---|---|
| Deband, ReShade.fxh, ReShadeUI.fxh | https://github.com/crosire/reshade-shaders (slim) | MIT (Deband, Niklas Haas), CC0 (headers) |
| prod80 Cinetools LUT | https://github.com/prod80/prod80-ReShade-Repository | MIT |
| LocalContrastCS | https://github.com/LordOfLunacy/Insane-Shaders | CC0-1.0 |
| CAS, Vibrance, FakeHDR, FilmGrain | https://github.com/CeeJayDK/SweetFX | MIT (CAS: MIT, AMD) |
| lilium SDR TRC fix | https://github.com/EndlesslyFlowering/ReShade_HDR_shaders | GPL-3.0 |
| qUINT (MXAO, Bloom, DOF), optional | https://github.com/martymcmodding/qUINT | All rights reserved: download only |

## Not shipped and not downloaded

`nvngx_dlssnr.dll` (DLSS 5 neural rendering runtime) is NVIDIA's file and may not be redistributed.
The user selects it once; the app identifies it by its SHA-256.
