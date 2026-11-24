# SPDX-License-Identifier: GPL-3.0-only
param([switch]$Offline, [switch]$CpuOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../scripts/environment.ps1')
$env:PATH = "$(Join-Path $ProjectRoot '.tools/mingw64/bin');$(Join-Path $ProjectRoot '.tools/w64devkit/bin');$env:PATH"
$env:CARGO_TARGET_X86_64_PC_WINDOWS_GNU_LINKER = Join-Path $ProjectRoot '.tools/mingw64/bin/gcc.exe'
$env:CARGO_TARGET_DIR = Join-Path $ProjectRoot 'artifacts/core-prototype/target'
$env:CMAKE_GENERATOR = 'Ninja'
$env:CC = Join-Path $ProjectRoot '.tools/mingw64/bin/gcc.exe'
$env:CXX = Join-Path $ProjectRoot '.tools/mingw64/bin/g++.exe'
$env:CMAKE_POLICY_VERSION_MINIMUM = '3.5'
if(-not $CpuOnly) {
    foreach($tool in @('cmake','ninja')) {
        if(-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "Shared GPU core build requires $tool on PATH (or in .tools/w64devkit/bin). See prototypes/processing-core/README.md." }
    }
}
$manifest = Join-Path $PSScriptRoot 'Cargo.toml'
$buildArgs = @('build','--release','--locked','--manifest-path',$manifest)
if ($Offline) { $buildArgs += '--offline' }
if ($CpuOnly) { $buildArgs += @('-p','tapetube-digital-host') }
& $Cargo @buildArgs
Assert-NativeExit 'Prototype build'
