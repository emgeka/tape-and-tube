# SPDX-License-Identifier: GPL-3.0-only
param([Parameter(Mandatory)][string]$Destination, [switch]$FromOriginalArchive)
$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$Core = Join-Path $ProjectRoot 'prototypes/processing-core'
$Pin = (Get-Content -LiteralPath (Join-Path $Core 'upstream-sources.json') -Raw | ConvertFrom-Json).ntscrt
$Vendor = Join-Path $Core "vendor/$($Pin.directory)"
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
if (-not $FromOriginalArchive) {
    if (-not (Test-Path -LiteralPath $Vendor -PathType Container)) {
        throw "Missing checked-in NTSCRT sources: $Vendor"
    }
    Copy-Item -LiteralPath $Vendor -Destination $Destination -Recurse
    return
}

$ArchivePath = Join-Path $ProjectRoot "artifacts/core-prototype/downloads/$($Pin.directory).zip"
if ((Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Pin.sha256) {
    throw 'NTSCRT archive checksum mismatch.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
try {
    $Root = [Regex]::Escape($Pin.directory)
    $Allow = "^$Root/(LICENSE|licenses/.+|port/licenses/.+|port/librashader-wgpu-push-constants\.patch|port/(codec|engine)/(Cargo\.toml|src/.+)|presets/(Screen Loop/)?[^/]+\.json)$"
    foreach ($Entry in $Archive.Entries) {
        if (-not $Entry.Name -or $Entry.FullName -cnotmatch $Allow) { continue }
        $Target = Join-Path $Destination $Entry.FullName
        New-Item -ItemType Directory -Path (Split-Path $Target -Parent) -Force | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($Entry, $Target, $true)
    }
    foreach ($Required in @('LICENSE','port/engine/src/lib.rs','port/codec/src/lib.rs','port/librashader-wgpu-push-constants.patch')) {
        if (-not (Test-Path -LiteralPath (Join-Path $Destination "$($Pin.directory)/$Required") -PathType Leaf)) {
            throw "Missing NTSCRT source: $Required"
        }
    }
} finally { $Archive.Dispose() }
