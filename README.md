# ED-Overdrive

> **Vorschau (privat).** Dieses Repo ist der Testzweig mit **AMD FSR 4 / 3.1**, **AMD FSR Frame Generation**
> und der **AMD-Fassung** der App. Es kommt ins öffentliche Repo
> [CMDRForcer/ED-Overdrive](https://github.com/CMDRForcer/ED-Overdrive), sobald es stabil ist.
> FSR 4 ist bisher nicht auf einer echten Radeon getestet.

Einrichtungs-Programm für **Elite Dangerous Odyssey** (DX11): installiert und pflegt
[EDAA](edaa) – Kantenglättung, Hochskalierung und echte Frame Generation – samt ReShade und Filter-Set.
Eine Datei, keine Installation, .NET ist eingebaut.

## Zwei Fassungen

Beide entstehen aus demselben Quellcode. Regel: **jede Fassung enthält alles, was auf den Karten ihres
Herstellers läuft** (Einzelheiten: [`edaa/docs/vendors.md`](edaa/docs/vendors.md)).

| | NVIDIA | AMD |
|---|---|---|
| Datei | `ED-Overdrive-FSR.exe` (öffentlich später `ED-Overdrive-NVIDIA.exe`) | `ED-Overdrive-AMD.exe` |
| Farben | Schwarz mit NVIDIA-Grün | Schwarz mit AMD-Rot |
| Kantenglättung | NVIDIA DLAA, AMD FSR, Intel XeSS, EDAA TAA | AMD FSR, Intel XeSS, EDAA TAA |
| Frame Generation | NVIDIA DLSS-G (RTX 40/50, mit Reflex) oder AMD FSR FG | AMD FSR FG (mit EDAAs Bildlimit) |
| DLSS 5 (GITC Uplift) | ✅ | – |
| FG-Preset im NVIDIA-Treiberprofil | ✅ | – |
| Add-on zum Installieren von Hand | `EDAA-<version>.zip` | `EDAA-<version>-amd.zip` |

**AMD FSR:** Welche Fassung läuft, entscheidet AMDs eigene DLL. Auf Radeon RX 6000 und neuer ist das FSR 4,
auf allen anderen Karten FSR 3.1. Die KI-gestützte FSR-FG 4.0 gibt es nur auf RX 9000. Im EDAA-Overlay
steht die Version in der FSR-Zeile.

## Download

Unter [Releases](https://github.com/CMDRForcer/ED-Overdrive-FSR/releases) die exe für deine Grafikkarte
herunterladen und starten. Jedes Release enthält beide Fassungen und beide Add-on-Pakete.

## Was mitgeliefert wird und was nicht

| Teil | NVIDIA | AMD | Lizenz |
|---|---|---|---|
| ReShade 6.8.0 (Add-on-Version, `dxgi.dll`) | ✅ | ✅ | BSD-3-Clause |
| EDAA (Add-on, Quellcode in [`edaa/`](edaa)) | ✅ | ✅ | MIT |
| AMD FSR 4 / 3.1 und FSR Frame Generation (FidelityFX SDK 2.4.0, signierte AMD-DLLs) | ✅ | ✅ | AMD FidelityFX License (nur als Binärdatei, kein Reverse Engineering) |
| Intel XeSS | ✅ | ✅ | Intel Simplified Software License |
| NVIDIA Streamline, DLSS (Super Resolution, Frame Generation) | ✅ | – | NVIDIA RTX SDK / NGX SDK |
| GITC Uplift 1.8.0 (DLSS 5) | ✅ | – | MIT |
| Filter-Shader (Deband, CAS, Vibrance, Fake-HDR, LUT, LocalContrast, Schwarzwert-Fix …) | ❌ beim Einrichten geladen | ❌ beim Einrichten geladen | MIT, CC0, GPL-3.0 (je Shader) |
| qUINT (MXAO, Bloom, DOF) | ❌ optionaler Download | ❌ optionaler Download | „All rights reserved“, darf nicht mitgeliefert werden |
| `nvngx_dlssnr.dll` (DLSS 5) | ❌ wählst du selbst aus | – | NVIDIA, darf nicht weitergegeben werden |

Die Installation von ReShade und EDAA braucht **kein Internet**; nur die Filter laden etwas herunter. Alle
Lizenztexte stehen in [`licenses/`](licenses) und [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md); bei der
Installation legt die App sie zusätzlich in `ED-Overdrive-Lizenzen` im Spielordner ab.

## Voraussetzungen

- Windows 10/11, 64 Bit, Hardware-beschleunigte GPU-Planung an (für Frame Generation)
- Elite Dangerous Odyssey (Steam, Epic oder Frontier) im DX11-Modus
- **NVIDIA:** DLAA ab RTX 20, DLSS Frame Generation ab **RTX 40**. AMD FSR und FSR FG laufen auch hier
  (sinnvoll z. B. auf RTX 20/30).
- **AMD:** eine Karte mit D3D12. FSR 4 ab Radeon RX 6000, sonst FSR 3.1.
- XeSS und TAA laufen in beiden Fassungen auf jeder DX11-Karte.

### Nur NVIDIA: welche `nvngx_dlssnr.dll` wir für DLSS 5 nutzen

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
   Auf AMD-Karten stellt die App dabei FSR und FSR FG ein.
3. **Schnellwahl:** mit einem Klick den EDAA-Modus wählen.
   - **NVIDIA:** Spiel-Setups (Supersampling 1,25 mit DLAA, oder DLSS 5) und die Modi DLAA + FG, nur DLAA,
     nur FG, FSR, FSR + FSR FG, aus. Gemessen auf einer RTX 4070 Ti in 1440p: 70 echte → 140 angezeigte
     Bilder/s im All mit DLAA + FG.
   - **AMD:** FSR, FSR + FSR FG, aus.
4. Spiel starten. Im Spiel öffnet **Pos1** das ReShade-Overlay, Reiter „EDAA“.

## Seiten der App

| Seite | Wofür | NVIDIA | AMD |
|---|---|---|---|
| Übersicht | Spielordner, Installation, Stand der Teile | ✅ | ✅ |
| Schnellwahl | Spiel-Setups und EDAA-Modi | ✅ | ✅ |
| Filter | ReShade-Filter-Varianten (Natürlich, Lebendig, Leistung, DLSS 5) | ✅ | ✅ |
| Grafik | Elite-Grafikwerte über das Spielmenü hinaus, vier Stufen | ✅ | ✅ |
| DLSS 5 | GITC Uplift installieren, DLL wählen, optimieren, entfernen | ✅ | – |
| Elite-Check | prüft Einstellungen, die mit EDAA kollidieren (Supersampling, CAS, Checkerboard …) | ✅ | ✅ |
| Presets | FG-Preset im NVIDIA-Treiberprofil von Elite (nur auf Knopfdruck, mit Backup) | ✅ | – |
| Einstellungen | die EDAA-Werte, die Spieler brauchen; mit `--dev` gestartet auch die Feinwerte | ✅ | ✅ |
| Diagnose | packt Logs, Einstellungen und einen Prüfbericht in ein Zip zum Weitergeben (ohne Journale) | ✅ | ✅ |
| Backups | alle Backups ansehen und wiederherstellen | ✅ | ✅ |

## Hinweise

- Im EDAA-Overlay unter „Advanced settings“ **„Resolve before the HUD pass“** anlassen.
- Nur **ein** Bildlimit nutzen: das von EDAA (knapp unter der Bildwiederholrate, z. B. 140 bei 144 Hz).
- **AMD FSR FG:** „Keep own ship and cockpit HUD steady“ ist an und erkennt das eigene Schiff beim Fliegen.
  Flimmert der Außenrumpf noch, „Own ship up to (m)“ auf etwa die Schiffslänge stellen (z. B. 95).
- **NVIDIA:** DLSS 5 braucht EDAAs Kantenglättung mit NVIDIA DLSS.
- EDHM/3DMigoto wird unterstützt. RenoDX mit Frame Generation gibt ein schwarzes Bild: im Overlay unter „Add-ons“ abschalten.
- Die App schreibt nie in die Registry. Einzige Ausnahme ist die Seite „Presets“ (nur NVIDIA), nur auf Knopfdruck und mit Backup.

## Aufbau des Repos

| Ordner | Inhalt |
|---|---|
| [`app/`](app) | ED-Overdrive (WPF, .NET 10), eine exe ohne Installation, beide Fassungen |
| [`edaa/`](edaa) | EDAA, das ReShade-Add-on (C++, CMake): TAA, DLAA, XeSS, FSR, DLSS-G und FSR FG |
| [`docs/`](docs) | Recherche zur App (Treiberprofil-Werte, Quellen) |
| [`licenses/`](licenses) | Lizenztexte aller mitgelieferten Teile |

EDAA stammt von [Siegfried-Origin/EDAA](https://github.com/Siegfried-Origin/EDAA) (MIT). In diesem Repo kamen
Frame Generation (DLSS-G und AMD FSR FG), DLSS-Modellwahl, AMD FSR und weitere Korrekturen dazu
(übernommen auf dem Stand EDAA 0.7.0).

## Bauen

Voraussetzungen: Visual Studio 2022 mit C++, CMake 3.20+, Windows SDK, .NET 10 SDK.

```
git clone --recurse-submodules --shallow-submodules https://github.com/CMDRForcer/ED-Overdrive-FSR.git
cd ED-Overdrive-FSR
```

| Schritt | NVIDIA | AMD |
|---|---|---|
| Add-on | `cmake -S edaa -B edaa/build -G "Visual Studio 17 2022" -A x64` | `cmake -S edaa -B edaa/build-amd -G "Visual Studio 17 2022" -A x64 -DEDAA_VENDOR=amd` |
| | `cmake --build edaa/build --config Release` | `cmake --build edaa/build-amd --config Release` |
| App | `dotnet publish app/GenFrame.Setup.csproj -p:PublishProfile=Release` | `dotnet publish app/GenFrame.Setup.csproj -p:PublishProfile=Release -p:Vendor=amd` |
| Ergebnis | `app/bin/publish/ED-Overdrive-FSR.exe` | `app/bin/publish/ED-Overdrive-AMD.exe` |

- AMD FSR kommt aus dem Submodul `edaa/third_party/fidelityfx` (FidelityFX SDK 2.4.0).
- Nur NVIDIA: für DLSS-G vorher das [Streamline SDK 2.14.16](https://github.com/NVIDIA-RTX/Streamline/releases/tag/v2.14.16)
  nach `edaa/third_party/streamline` entpacken.
- Vor dem App-Bau holt `app/ThirdParty/fetch-bundle.ps1` den ReShade-Installer (per SHA-256 geprüft) und packt EDAA
  in `app/ThirdParty/EDAA.zip` bzw. `EDAA-amd.zip`. Liegt dort schon ein Zip, bleibt es; zum Erneuern löschen.
  Zwischen den beiden App-Bauten `app/obj` löschen.
- Developer-Build des Add-ons (Feinwerte, Debug-Ansichten, GPU-Zeiten): `-DEDAA_DEVELOPER=ON`
  (siehe [`edaa/docs/addon.md`](edaa/docs/addon.md)).

GitHub Actions baut bei jedem Push und Pull Request beide Fassungen (`.github/workflows/build.yml`). Ein
veröffentlichtes Release baut alles neu und hängt beide exes und beide EDAA-Zips an
(`.github/workflows/release.yml`). Die EDAA-Version steht in `edaa/VERSION`.

## Lizenz

ED-Overdrive steht unter der [MIT-Lizenz](LICENSE). EDAA (MIT, Siegfried Origin) und alle mitgelieferten Teile
behalten ihre eigenen Lizenzen, siehe [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md). NVIDIA, DLSS und
GeForce RTX sind Marken der NVIDIA Corporation, Intel und XeSS der Intel Corporation, AMD, Radeon, FidelityFX
und FSR der Advanced Micro Devices, Inc., Elite Dangerous von Frontier Developments. ED-Overdrive ist kein
offizielles Produkt dieser Firmen.
