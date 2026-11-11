# ED-Overdrive – FSR experimentell

> **Experimenteller Zweig (privat).** Diese Fassung bringt zusätzlich **AMD FSR 4 / 3.1** als Methode in EDAA:
> FSR 4 auf Radeon RX 6000 und neuer, auf allen anderen Karten (auch NVIDIA) FSR 3.1 – die Wahl trifft AMDs DLL.
> Die exe heißt `ED-Overdrive-FSR.exe`, der Kanal „Entwicklung“ ist hier abgeschaltet. FSR 4 ist bisher nicht auf
> einer Radeon getestet.

Einrichtungs-Programm für **Elite Dangerous Odyssey** (DX11): installiert und pflegt
[EDAA](edaa) – Kantenglättung (NVIDIA DLAA, Intel XeSS, TAA) und echte
**DLSS Frame Generation** – samt ReShade, Filter-Set und optional **DLSS 5** (GITC Uplift).

Eine Datei, keine Installation: `ED-Overdrive.exe` aus den [Releases](https://github.com/CMDRForcer/ED-Overdrive/releases)
herunterladen und starten. Die App bringt .NET mit.

## Was mitgeliefert wird und was nicht

| Teil | In der exe | Lizenz |
|---|---|---|
| ReShade 6.8.0 (Add-on-Version, `dxgi.dll`) | ✅ | BSD-3-Clause |
| EDAA (Add-on, Quellcode in [`edaa/`](edaa)) | ✅ | MIT |
| NVIDIA Streamline, DLSS (Super Resolution, Frame Generation) | ✅ als Teil von EDAA | NVIDIA RTX SDK / NGX SDK |
| Intel XeSS | ✅ als Teil von EDAA | Intel Simplified Software License |
| AMD FSR 4 / 3.1 (FidelityFX SDK 2.4.0, signierte AMD-DLLs) | ✅ als Teil von EDAA | AMD FidelityFX License (nur als Binärdatei, kein Reverse Engineering) |
| GITC Uplift 1.8.0 (DLSS 5) | ✅ | MIT |
| Filter-Shader (Deband, CAS, Vibrance, Fake-HDR, LUT, LocalContrast, Schwarzwert-Fix …) | ❌ werden beim Einrichten aus den Original-Repos der Autoren geladen | MIT, CC0, GPL-3.0 (je Shader) |
| qUINT (MXAO, Bloom, DOF) | ❌ optionaler Download | „All rights reserved“, darf nicht mitgeliefert werden |
| `nvngx_dlssnr.dll` (DLSS 5) | ❌ wählst du selbst aus | NVIDIA, darf nicht weitergegeben werden |

Die Installation von ReShade und EDAA braucht also **kein Internet**; nur die Filter und der Kanal
„Entwicklung“ laden etwas herunter. Jedes Release enthält außerdem `EDAA-<version>.zip` für die
Installation von Hand (siehe [`edaa/README.md`](edaa/README.md)). Alle Lizenztexte stehen in [`licenses/`](licenses) und
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md); bei der Installation legt die App sie zusätzlich in
`ED-Overdrive-Lizenzen` im Spielordner ab.

## Voraussetzungen

- Windows 10/11, 64 Bit
- Elite Dangerous Odyssey (Steam, Epic oder Frontier) im DX11-Modus
- Für DLAA/DLSS: NVIDIA RTX. Für Frame Generation: **RTX 40/50**. XeSS und TAA laufen auf jeder DX11-Karte, AMD FSR (3.1) auf jeder Karte mit D3D12.
- Für DLSS 5: die passende `nvngx_dlssnr.dll` (RTX 50: NVIDIA-Original; RTX 20/30/40: angepasster Community-Build,
  unsigniert, auf eigenes Risiko)

### Welche `nvngx_dlssnr.dll` wir für DLSS 5 nutzen

Die Datei liegt nicht bei und darf nicht weitergegeben werden. Die App erkennt bekannte Versionen an der SHA-256
und zeigt sie auf der Seite „DLSS 5“ an.

| Datei | Version | Größe | SHA-256 | Status |
|---|---|---|---|---|
| Community-Build „compat-fp16“ (unsigniert) | 310.9.16 | 276 MB | `cdfa6c1ec784867182164c7836f324f087b5c9f374d0c9fe46f54999feabf3ed` | **unsere Wahl**: getestet auf RTX 4070 Ti mit GITC Uplift 1.8.0, Netz „Small FP8“, ~67 → 135 Bilder/s |
| angepasster Build für RTX 20/30/40 (RenoDX-Discord) | 310.8 | 166 MB | `e67dee209320cdafe0e93e45675d7aa34323a53acc57a72b2e40a181581c989a` | läuft, kann aber nur das Netz „Legacy“ (langsamer, ~56 echte Bilder/s) |
| NVIDIA-Original | 310.9.x | – | – | nur RTX 50, von uns nicht getestet |

Die unsignierten Builds sind von NVIDIAs Datei abgeleitet und nicht von NVIDIA freigegeben: Nutzung auf eigenes Risiko.

## So geht's

1. **Elite beenden.** Die App ändert nur bei geschlossenem Spiel etwas und legt vorher immer ein Backup an.
2. **Übersicht:** Spielordner wählen (wird meist gefunden) → *Installieren*. Für einen sauberen Neuanfang
   *Saubere Neuinstallation*: alte Dateien werden nach `%LOCALAPPDATA%\GenFrame\Entfernt` verschoben, nicht gelöscht.
3. **Schnellwahl → Spiel-Setup:** mit einem Klick das ganze Setup wählen
   - **Supersampling 1,25:** DLAA, Ultra-Grafik, DLSS 5 aus – gemessen auf einer RTX 4070 Ti in 1440p:
     70 echte → 140 angezeigte Bilder/s im All, etwa 65 echte im vollen Concourse.
   - **DLSS 5:** Elite rendert mit 0,85, DLSS 5 verbessert das Bild, DLSS skaliert hoch – etwa 67 → 135 Bilder/s.
4. Spiel starten. Im Spiel öffnet **Pos1** das ReShade-Overlay (Reiter „EDAA“ und „GITC Uplift“).

## Seiten der App

| Seite | Wofür |
|---|---|
| Übersicht | Spielordner, Installation, Kanal (Stabil = in der App enthalten, Entwicklung = neuester Build aus `main`) |
| Schnellwahl | Spiel-Setups und EDAA-Modi (DLAA + FG, nur DLAA, nur FG, aus) |
| Filter | ReShade-Filter-Varianten (Natürlich, Lebendig, Leistung, DLSS 5) |
| Grafik | Elite-Grafikwerte über das Spielmenü hinaus, vier Stufen |
| DLSS 5 | GITC Uplift installieren, DLL wählen, optimieren, entfernen |
| Elite-Check | prüft Einstellungen, die mit EDAA kollidieren (Supersampling, CAS, Checkerboard …) |
| Presets | FG-Preset im NVIDIA-Treiberprofil von Elite (nur auf Knopfdruck, mit Backup) |
| Einstellungen | EDAA-Werte aus `ReShade.ini` |
| Diagnose | packt Logs, Einstellungen und einen Prüfbericht in ein Zip zum Weitergeben (ohne Journale) |
| Backups | alle Backups ansehen und wiederherstellen |

## Hinweise

- Im EDAA-Overlay **„Resolve before the HUD pass“** anlassen. DLSS 5 braucht EDAAs Kantenglättung mit NVIDIA DLSS.
- Nur **ein** Bildlimit nutzen: das von EDAA (knapp unter der Bildwiederholrate, z. B. 140 bei 144 Hz).
- EDHM/3DMigoto wird unterstützt. RenoDX mit Frame Generation gibt ein schwarzes Bild: im Overlay unter „Add-ons“ abschalten.
- Die App schreibt nie in die Registry. Einzige Ausnahme ist die Seite „Presets“, nur auf Knopfdruck und mit Backup.

## Aufbau des Repos

| Ordner | Inhalt |
|---|---|
| [`app/`](app) | ED-Overdrive (WPF, .NET 10), eine exe ohne Installation |
| [`edaa/`](edaa) | EDAA, das ReShade-Add-on (C++, CMake): TAA, DLAA, XeSS und DLSS Frame Generation |
| [`docs/`](docs) | Recherche zur App (Treiberprofil-Werte, Quellen) |
| [`licenses/`](licenses) | Lizenztexte aller mitgelieferten Teile |

EDAA stammt von [Siegfried-Origin/EDAA](https://github.com/Siegfried-Origin/EDAA) (MIT). In diesem Repo kamen
DLSS Frame Generation (Streamline), DLSS-Modellwahl und weitere Korrekturen dazu (übernommen auf dem Stand EDAA 0.7.0).

## Bauen

Voraussetzungen: Visual Studio 2022 mit C++, CMake 3.20+, Windows SDK, .NET 10 SDK.

```
git clone --recurse-submodules --shallow-submodules https://github.com/CMDRForcer/ED-Overdrive.git
cd ED-Overdrive
```

1. **Add-on** (optional, sonst nimmt der App-Build das EDAA-Paket aus dem neuesten Release):
   ```
   cmake -S edaa -B edaa/build -G "Visual Studio 17 2022" -A x64
   cmake --build edaa/build --config Release
   ```
   AMD FSR kommt aus dem Submodul `edaa/third_party/fidelityfx` (FidelityFX SDK 2.4.0).
   Für Frame Generation vorher das [Streamline SDK 2.14.16](https://github.com/NVIDIA-RTX/Streamline/releases/tag/v2.14.16)
   nach `edaa/third_party/streamline` entpacken.
2. **App:**
   ```
   dotnet publish app/GenFrame.Setup.csproj -p:PublishProfile=Release
   ```
   Ergebnis: `app/bin/publish/ED-Overdrive.exe`. Vor dem Bauen holt `app/ThirdParty/fetch-bundle.ps1` den
   ReShade-Installer (per SHA-256 geprüft) und packt EDAA aus `edaa/build/Release` in `app/ThirdParty/EDAA.zip`.
   Liegt dort schon eine `EDAA.zip`, bleibt sie; zum Erneuern löschen.

GitHub Actions baut bei jedem Push und Pull Request Add-on und App (`.github/workflows/build.yml`) und legt das
Add-on von `main` in den Zweig `builds/main` (Kanal „Entwicklung“). Ein veröffentlichtes Release baut beides neu
und hängt `ED-Overdrive.exe` und `EDAA-<version>.zip` an (`.github/workflows/release.yml`). Die EDAA-Version
steht in `edaa/VERSION`.

## Lizenz

ED-Overdrive steht unter der [MIT-Lizenz](LICENSE). EDAA (MIT, Siegfried Origin) und alle mitgelieferten Teile
behalten ihre eigenen Lizenzen, siehe [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md). NVIDIA, DLSS und
GeForce RTX sind Marken der NVIDIA Corporation, Intel und XeSS der Intel Corporation, AMD, FidelityFX und FSR
der Advanced Micro Devices, Inc., Elite Dangerous von
Frontier Developments. ED-Overdrive ist kein offizielles Produkt dieser Firmen.
