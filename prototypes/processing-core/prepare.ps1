# SPDX-License-Identifier: GPL-3.0-only
param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$protoRoot = $PSScriptRoot
$projectRoot = Split-Path (Split-Path $protoRoot -Parent) -Parent
$sources = Get-Content -LiteralPath (Join-Path $protoRoot 'upstream-sources.json') -Raw | ConvertFrom-Json
$downloadRoot = Join-Path $projectRoot 'artifacts/core-prototype/downloads'
$upstreamRoot = Join-Path $protoRoot 'upstream'
New-Item -ItemType Directory -Force -Path $downloadRoot,$upstreamRoot | Out-Null
$vendor = Join-Path $protoRoot "vendor/$($sources.ntscrt.directory)"
$engineRoot = Join-Path $upstreamRoot $sources.ntscrt.directory
if (-not (Test-Path -LiteralPath $vendor -PathType Container)) {
    throw "Missing checked-in NTSCRT sources: $vendor"
}
if (-not (Test-Path -LiteralPath $engineRoot)) {
    Copy-Item -LiteralPath $vendor -Destination $upstreamRoot -Recurse
}

$source = $sources.librashader
$archive = Join-Path $downloadRoot ($source.directory + '.zip')
$earlierShaderArchive = Join-Path $downloadRoot 'librashader-76462c03.zip'
if (-not (Test-Path -LiteralPath $archive) -and (Test-Path -LiteralPath $earlierShaderArchive)) {
    $archive = $earlierShaderArchive
}
if (-not (Test-Path -LiteralPath $archive)) {
    if ($Offline) { throw "Source archive is missing: $archive" }
    Invoke-WebRequest -Uri $source.archive -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $source.sha256) {
    throw 'Source checksum mismatch: librashader'
}
$shaderRoot = Join-Path $upstreamRoot $sources.librashader.directory
if (-not (Test-Path -LiteralPath $shaderRoot)) {
    Expand-Archive -LiteralPath $archive -DestinationPath $upstreamRoot
}
function Apply-PreparedPatch([string]$SourceRoot, [string]$PatchPath) {
    & git -C $SourceRoot -c core.longpaths=true apply --reverse --check $PatchPath 2>$null
    if ($LASTEXITCODE -eq 0) { return }
    & git -C $SourceRoot -c core.longpaths=true apply $PatchPath
    if ($LASTEXITCODE -ne 0) { throw "Cannot apply patch: $PatchPath" }
}

# Git must interpret patches relative to the prepared dependency, not the Tape & Tube checkout.
$previousCeiling = $env:GIT_CEILING_DIRECTORIES
try {
    $env:GIT_CEILING_DIRECTORIES = $upstreamRoot
    Apply-PreparedPatch $shaderRoot (Join-Path $upstreamRoot $sources.librashader.patch)
    Apply-PreparedPatch $engineRoot (Join-Path $protoRoot 'engine-adapter/host-lifecycle.patch')
} finally {
    $env:GIT_CEILING_DIRECTORIES = $previousCeiling
}
Write-Output 'Pinned sources and documented adapter patches are ready.'
