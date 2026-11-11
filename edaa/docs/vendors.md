# Vendor builds

ED-Overdrive comes in two builds from the same source. Rule: **every build carries
everything that runs on that vendor's graphics cards.** Vendor-neutral parts are in both.

| Feature | Runs on NVIDIA | Runs on AMD | NVIDIA build | AMD build |
|---|---|---|---|---|
| EDAA TAA, motion vectors, HUD protection, ship detection, frame limit | yes | yes | yes | yes |
| Intel XeSS | yes | yes | yes | yes |
| AMD FSR 4 / 3.1 upscaling and native AA | yes (FSR 3.1) | yes | yes (*) | yes |
| AMD FSR Frame Generation | yes | yes | yes (*) | yes |
| NVIDIA DLSS / DLAA | yes | no | yes | no |
| NVIDIA DLSS-G (Streamline) | RTX 40/50 | no | yes | no |
| NVIDIA Reflex | yes | no | yes | no |
| App: DLSS 5 (GITC Uplift) | RTX | no | yes | no |
| App: NVIDIA driver profile presets | yes | no | yes | no |

(*) FSR stays in the private repository until it is stable; the public NVIDIA build
gets it then.

A new feature gets a row here before it ships.

Building:

- Add-on: `cmake -S edaa -B edaa/build-amd -DEDAA_VENDOR=amd` (default `nvidia`). The AMD
  build defines `EDAA_VENDOR_AMD=1`, leaves out the DLSS and Streamline SDKs, forces
  `fg_backend=1` and maps a saved DLSS method to FSR.
- Package: `edaa/tools/bundle.ps1 -Vendor amd` (no NVIDIA DLLs or licenses).
- App: `dotnet publish app/GenFrame.Setup.csproj -p:PublishProfile=Release -p:Vendor=amd`
  (exe `ED-Overdrive-AMD.exe`, red theme, embeds `app/ThirdParty/EDAA-amd.zip`). Delete
  `app/obj` between the two app builds.
