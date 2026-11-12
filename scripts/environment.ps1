$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $ProjectRoot '.tools/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $ProjectRoot '.tools/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:RUSTUP_HOME = Join-Path $ProjectRoot '.tools/rustup'
$env:CARGO_HOME = Join-Path $ProjectRoot '.tools/cargo'
$env:PATH = "$(Join-Path $ProjectRoot '.tools/mingw64/bin');$(Join-Path $ProjectRoot '.tools/cargo/bin');$env:PATH"
$Dotnet = Join-Path $ProjectRoot '.tools/dotnet/dotnet.exe'
$Cargo = Join-Path $ProjectRoot '.tools/cargo/bin/cargo.exe'

function Assert-NativeExit([string]$Operation) {
    if ($LASTEXITCODE -ne 0) { throw "$Operation fehlgeschlagen (Exitcode $LASTEXITCODE)." }
}

function Get-AppExecutable {
    $LocationFile = Join-Path $ProjectRoot 'runtime/current-app.txt'
    $RelativeDirectory = if (Test-Path -LiteralPath $LocationFile) { (Get-Content -LiteralPath $LocationFile -Raw).Trim() } else { 'artifacts/app' }
    $TapeTubeExecutable = Join-Path $ProjectRoot "$RelativeDirectory/TapeTube.exe"
    if (Test-Path -LiteralPath $TapeTubeExecutable) { return $TapeTubeExecutable }
    return Join-Path $ProjectRoot "$RelativeDirectory/Ntscrt.Windows.exe"
}
