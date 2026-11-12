param([switch]$Demo)
$ErrorActionPreference = 'Stop'
$LocationFile = Join-Path $PSScriptRoot 'runtime/current-app.txt'
$RelativeDirectory = if (Test-Path -LiteralPath $LocationFile) { (Get-Content -LiteralPath $LocationFile -Raw).Trim() } else { 'artifacts/app' }
$Executable = Join-Path $PSScriptRoot "$RelativeDirectory/TapeTube.exe"
if (-not (Test-Path -LiteralPath $Executable)) { $Executable = Join-Path $PSScriptRoot "$RelativeDirectory/Ntscrt.Windows.exe" }
if (-not (Test-Path -LiteralPath $Executable)) { throw 'Bitte zuerst scripts/setup.ps1 und scripts/build.ps1 ausführen.' }
if ($Demo) { Start-Process -FilePath $Executable -ArgumentList '--demo' }
else { Start-Process -FilePath $Executable }
