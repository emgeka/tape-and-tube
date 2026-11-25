# SPDX-License-Identifier: GPL-3.0-only
param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$Core = Join-Path $ProjectRoot 'prototypes/processing-core'
$Pin = (Get-Content -LiteralPath (Join-Path $Core 'upstream-sources.json') -Raw | ConvertFrom-Json).ntscrt
$Vendor = Join-Path $Core "vendor/$($Pin.directory)"
$Presets = Join-Path $Destination 'presets/NTSCRT'
$ScreenLoopFolder = Join-Path $Presets 'Screen Loop'
$Looks = @(Get-ChildItem -LiteralPath (Join-Path $Vendor 'presets') -File -Filter '*.json')
$ScreenLoops = @(Get-ChildItem -LiteralPath (Join-Path $Vendor 'presets/Screen Loop') -File -Filter '*.json')
if ($Looks.Count -eq 0 -or $ScreenLoops.Count -eq 0) { throw 'The checked-in NTSCRT preset collections are incomplete.' }

New-Item -ItemType Directory -Path $Presets,$ScreenLoopFolder -Force | Out-Null
$Looks | Copy-Item -Destination $Presets
$ScreenLoops | Copy-Item -Destination $ScreenLoopFolder
Copy-Item -LiteralPath (Join-Path $Vendor 'LICENSE') -Destination (Join-Path $Presets 'LICENSE.txt')
@(
    "$($Pin.directory) presets by Finn McKenty."
    "Revision: $($Pin.revision)"
    "Source: $($Pin.repository)/tree/$($Pin.revision)/presets"
    "License: $($Pin.license), see LICENSE.txt."
) | Set-Content -LiteralPath (Join-Path $Presets 'README.txt') -Encoding utf8
Write-Host "$($Looks.Count) NTSCRT looks and $($ScreenLoops.Count) Screen Loop presets."
