# Provides the parts ED-Overdrive ships inside its exe (embedded at build time, see GenFrame.Setup.csproj).
# Runs before every build.
#   EDAA.zip: kept if present (CI and edaa/tools/bundle.ps1 put a fresh one here). Otherwise packed from a
#             local add-on build in edaa/build/Release, or else taken from the newest ED-Overdrive release.
#             Delete it to pick up a newer add-on build.
#   ReShade:  the official add-on installer, pinned by SHA-256 (keep in step with InstallService.BundledReShade).
#   -Vendor amd: EDAA-amd.zip, packed from edaa/build-amd/Release (no release download).
param([string]$Out = $PSScriptRoot, [ValidateSet('nvidia', 'amd')][string]$Vendor = 'nvidia')
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Not Get-FileHash: when pwsh starts Windows PowerShell (as on the GitHub runner), its module path hides the cmdlet.
function Sha256($file) {
    $stream = [IO.File]::OpenRead($file)
    try { -join ([Security.Cryptography.SHA256]::Create().ComputeHash($stream) | ForEach-Object { $_.ToString('x2') }) }
    finally { $stream.Dispose() }
}

function Download($url, $file) {
    Write-Host "ED-Overdrive bundle: downloading $url"
    Invoke-WebRequest -Uri $url -OutFile "$file.part" -UseBasicParsing -UserAgent 'ED-Overdrive-build'
}

$reshade = Join-Path $Out 'ReShade_Setup_Addon.exe'
$reshadeSha = 'afe4c8f13048306307983b8b3d41d5bf00a86820440b0e57dea10950e1176445'
if (-not ((Test-Path $reshade) -and (Sha256 $reshade) -eq $reshadeSha)) {
    Download 'https://reshade.me/downloads/ReShade_Setup_6.8.0_Addon.exe' $reshade
    $hash = Sha256 "$reshade.part"
    if ($hash -ne $reshadeSha) {
        Remove-Item "$reshade.part"
        throw "Unexpected SHA-256 for the ReShade installer: $hash"
    }
    Move-Item "$reshade.part" $reshade -Force
}

$edaa = Join-Path $Out ($(if ($Vendor -eq 'amd') { 'EDAA-amd.zip' } else { 'EDAA.zip' }))
if (Test-Path $edaa) {
    return
}
$release = Join-Path $PSScriptRoot ($(if ($Vendor -eq 'amd') { '..\..\edaa\build-amd\Release' } else { '..\..\edaa\build\Release' }))
if (Test-Path (Join-Path $release 'EDAA.addon64')) {
    & (Join-Path $PSScriptRoot '..\..\edaa\tools\bundle.ps1') -Release $release -Out $edaa -Vendor $Vendor
    return
}
if ($Vendor -eq 'amd') {
    throw 'No EDAA-amd.zip: build the add-on with -DEDAA_VENDOR=amd into edaa/build-amd first (edaa/docs/vendors.md).'
}
$latest = Invoke-RestMethod -Uri 'https://api.github.com/repos/CMDRForcer/ED-Overdrive/releases?per_page=10' -UserAgent 'ED-Overdrive-build'
$asset = $latest | Where-Object { -not $_.draft } | ForEach-Object { $_.assets } |
    Where-Object { $_.name -like 'EDAA-*.zip' } | Select-Object -First 1
if (-not $asset) {
    throw 'No EDAA.zip: build the add-on first (see edaa/README.md) or put a release zip into app/ThirdParty/EDAA.zip.'
}
Download $asset.browser_download_url $edaa
Move-Item "$edaa.part" $edaa -Force
