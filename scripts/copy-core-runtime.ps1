# SPDX-License-Identifier: GPL-3.0-only
param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$coreDirectory=Join-Path $Destination 'core'
New-Item -ItemType Directory -Force -Path $coreDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'artifacts/core-prototype/target/release/tapetube-core-probe.exe') -Destination (Join-Path $coreDirectory 'TapeTube.Core.exe')
foreach($runtimeName in @('libstdc++-6.dll','libgcc_s_seh-1.dll','libwinpthread-1.dll')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ".tools/mingw64/bin/$runtimeName") -Destination $coreDirectory
}
foreach($notice in @('gcc','winpthreads','mingw-w64')) {
    $target=Join-Path $Destination "licenses/core-runtime/$notice"
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectRoot ".tools/mingw64/licenses/$notice") -Destination (Split-Path $target -Parent) -Recurse -Force
}
$ntscrt=Join-Path $Destination 'licenses/upstream'
New-Item -ItemType Directory -Force -Path $ntscrt | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses/upstream/NTSCRT-MIT.txt') -Destination $ntscrt
Copy-Item -LiteralPath (Join-Path $projectRoot 'prototypes/processing-core/upstream-sources.json') -Destination (Join-Path $coreDirectory 'upstream-sources.json')
