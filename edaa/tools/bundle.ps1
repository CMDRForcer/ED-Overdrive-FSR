# Packs a Release build of EDAA into the zip that ED-Overdrive embeds and every release attaches:
# the add-on, the DLSS/XeSS/Streamline/AMD FSR DLLs, version.txt and all license texts.
#   edaa/tools/bundle.ps1 -Release edaa/build/Release -Out app/ThirdParty/EDAA.zip
param(
    [Parameter(Mandatory)][string]$Release,
    [Parameter(Mandatory)][string]$Out,
    [ValidateSet('nvidia', 'amd')][string]$Vendor = 'nvidia', # amd: no NVIDIA DLLs (edaa/docs/vendors.md)
    [string]$Version = (Get-Content (Join-Path $PSScriptRoot '..\VERSION') -TotalCount 1).Trim()
)
$ErrorActionPreference = 'Stop'
$edaa = Resolve-Path (Join-Path $PSScriptRoot '..')
$tp = Join-Path $edaa 'third_party'

$stage = Join-Path ([IO.Path]::GetTempPath()) ("edaa-bundle-" + [guid]::NewGuid())
New-Item -ItemType Directory -Force "$stage\licenses" | Out-Null
try {
    $nvidia = 'nvngx_dlss.dll', 'sl.interposer.dll', 'sl.common.dll', 'sl.dlss_g.dll', 'sl.reflex.dll', 'sl.pcl.dll', 'nvngx_dlssg.dll'
    $files = @('EDAA.addon64', 'libxess.dll',
               'amd_fidelityfx_loader_dx12.dll', 'amd_fidelityfx_upscaler_dx12.dll', 'amd_fidelityfx_framegeneration_dx12.dll')
    if ($Vendor -eq 'nvidia') { $files += $nvidia }
    foreach ($f in $files) {
        Copy-Item (Join-Path $Release $f) $stage
    }
    Copy-Item "$edaa\README.md", "$edaa\LICENSE", "$edaa\THIRD_PARTY_NOTICES.md" $stage
    $licenses = @{
        "$tp\dlss\LICENSE.txt"                           = 'NVIDIA-RTX-SDKs-LICENSE.txt'
        "$tp\reshade\LICENSE.md"                         = 'ReShade-LICENSE.md'
        "$tp\imgui\LICENSE.txt"                          = 'ImGui-LICENSE.txt'
        "$tp\xess\LICENSE.txt"                           = 'Intel-XeSS-LICENSE.txt'
        "$tp\xess\third-party-programs.txt"              = 'Intel-XeSS-third-party-programs.txt'
        "$tp\streamline\license.txt"                     = 'NVIDIA-Streamline-LICENSE.txt'
        "$tp\streamline\external\ngx-sdk\license.txt"    = 'NVIDIA-NGX-SDK-LICENSE.txt'
        "$tp\fidelityfx\Kits\FidelityFX\docs\license.md" = 'AMD-FidelityFX-LICENSE.md'
    }
    if ($Vendor -eq 'amd') {
        foreach ($key in @($licenses.Keys | Where-Object { $_ -match '\\(dlss|streamline)\\' })) { $licenses.Remove($key) }
    }
    foreach ($entry in $licenses.GetEnumerator()) {
        Copy-Item $entry.Key (Join-Path "$stage\licenses" $entry.Value)
    }
    Set-Content -Path "$stage\version.txt" -Value $Version -NoNewline -Encoding ascii

    New-Item -ItemType Directory -Force (Split-Path -Parent ([IO.Path]::GetFullPath($Out))) | Out-Null
    if (Test-Path $Out) { Remove-Item $Out }
    # Not Compress-Archive: Windows PowerShell 5 writes "licenses\x" entry names, zip wants "licenses/x".
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::Open([IO.Path]::GetFullPath($Out), 'Create')
    try {
        foreach ($file in Get-ChildItem $stage -Recurse -File) {
            $name = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $name, 'Optimal')
        }
    }
    finally {
        $zip.Dispose()
    }
    Write-Host "EDAA $Version ($Vendor) packed into $Out"
}
finally {
    Remove-Item -Recurse -Force $stage
}
