# SPDX-License-Identifier: GPL-3.0-only
param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$protoRoot = $PSScriptRoot
$projectRoot = Split-Path (Split-Path $protoRoot -Parent) -Parent
$sources = Get-Content -LiteralPath (Join-Path $protoRoot 'upstream-sources.json') -Raw | ConvertFrom-Json
$downloadRoot = Join-Path $projectRoot 'artifacts/core-prototype/downloads'
$upstreamRoot = Join-Path $protoRoot 'upstream'
New-Item -ItemType Directory -Force -Path $downloadRoot,$upstreamRoot | Out-Null
foreach ($name in @('ntscrt','librashader')) {
    $source = $sources.$name
    $archive = Join-Path $downloadRoot ($source.directory + '.zip')
    # Earlier local preparation used a short name for the same librashader archive.
    $earlierShaderArchive = Join-Path $downloadRoot 'librashader-76462c03.zip'
    if ($name -eq 'librashader' -and -not (Test-Path -LiteralPath $archive) -and (Test-Path -LiteralPath $earlierShaderArchive)) {
        $archive = $earlierShaderArchive
    }
    if (-not (Test-Path -LiteralPath $archive)) {
        if ($Offline) { throw "Source archive is missing: $archive" }
        Invoke-WebRequest -Uri $source.archive -OutFile $archive
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $source.sha256) {
        throw "Source checksum mismatch: $name"
    }
    $destination = Join-Path $upstreamRoot $source.directory
    if (-not (Test-Path -LiteralPath $destination)) { Expand-Archive -LiteralPath $archive -DestinationPath $upstreamRoot }
}
$shaderRoot = Join-Path $upstreamRoot $sources.librashader.directory
$patch = Join-Path $upstreamRoot $sources.librashader.patch
& git -C $shaderRoot apply --reverse --check $patch 2>$null
if ($LASTEXITCODE -ne 0) {
    & git -C $shaderRoot apply --check $patch
    if ($LASTEXITCODE -ne 0) { throw 'The pinned librashader patch cannot be applied cleanly.' }
    & git -C $shaderRoot apply $patch
    if ($LASTEXITCODE -ne 0) { throw 'Applying the pinned librashader patch failed.' }
}
$engineRoot = Join-Path $upstreamRoot $sources.ntscrt.directory
$hostPatch = Join-Path $protoRoot 'engine-adapter/host-lifecycle.patch'
& git -C $engineRoot apply --reverse --check $hostPatch 2>$null
if ($LASTEXITCODE -ne 0) {
    & git -C $engineRoot apply --check $hostPatch
    if ($LASTEXITCODE -ne 0) { throw 'The engine host-lifecycle patch cannot be applied cleanly.' }
    & git -C $engineRoot apply $hostPatch
    if ($LASTEXITCODE -ne 0) { throw 'Applying the engine host-lifecycle patch failed.' }
}
Write-Output 'Pinned sources and documented adapter patches are ready.'
