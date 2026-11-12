param([switch]$Offline)
. "$PSScriptRoot/environment.ps1"
Push-Location $ProjectRoot
try {
    $CargoArguments = @('build', '--release', '--locked', '--manifest-path', 'native/Cargo.toml')
    if ($Offline) { $CargoArguments += '--offline' }
    & $Cargo @CargoArguments
    Assert-NativeExit 'ntsc-rs bauen'
    New-Item -ItemType Directory -Force runtime | Out-Null
    Copy-Item -LiteralPath '.tools/librashader/librashader.dll' -Destination 'runtime/librashader.dll' -Force
    Copy-Item -LiteralPath 'native/target/release/ntscrt_ntsc.dll' -Destination 'runtime/ntscrt_ntsc.dll' -Force
    if (-not $Offline) {
        & $Dotnet restore 'src/Ntscrt.Windows/Ntscrt.Windows.csproj'
        Assert-NativeExit '.NET-Pakete laden'
    }
    # Keep a running version and its unsaved settings intact while building an update.
    $BuildDirectory = 'artifacts/builds/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
    & $Dotnet build 'src/Ntscrt.Windows/Ntscrt.Windows.csproj' -c Release --no-restore --output $BuildDirectory
    Assert-NativeExit 'Windows-App bauen'
    Set-Content -LiteralPath 'runtime/current-app.txt' -Value $BuildDirectory -Encoding utf8
} finally { Pop-Location }
