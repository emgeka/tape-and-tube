# SPDX-License-Identifier: GPL-3.0-only
param([string]$Backend = 'dx12', [switch]$SkipBuild, [switch]$Offline, [string]$ResultDirectory = '')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../scripts/environment.ps1')
$env:CARGO_TARGET_DIR = Join-Path $ProjectRoot 'artifacts/core-prototype/target'
$env:NTSCRT_BACKEND = $Backend
if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'prepare.ps1') -Offline:$Offline
    & (Join-Path $PSScriptRoot 'build.ps1') -Offline:$Offline
}
& $Cargo test --release --locked --offline --manifest-path (Join-Path $PSScriptRoot 'Cargo.toml') -p tapetube-digital-host
Assert-NativeExit 'Digital host tests'
$consumerOutput = Join-Path $ProjectRoot 'artifacts/core-prototype/abi-smoke'
& $Dotnet build (Join-Path $PSScriptRoot 'abi-smoke/AbiSmoke.csproj') -c Release -o $consumerOutput --nologo
Assert-NativeExit 'External ABI test consumer build'
& $Dotnet (Join-Path $consumerOutput 'TapeTube.DigitalAbiSmoke.dll') (Join-Path $env:CARGO_TARGET_DIR 'release/tapetube_digital_host.dll')
Assert-NativeExit 'External ABI tests'
$testArgs = @((Join-Path $PSScriptRoot 'test.mjs'))
if ($ResultDirectory) { $testArgs += @('--output', [IO.Path]::GetFullPath($ResultDirectory)) }
& node @testArgs
Assert-NativeExit 'Complete processing prototype tests'
