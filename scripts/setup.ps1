# Downloads development tools and upstream runtime assets into this project only.
. "$PSScriptRoot/environment.ps1"
if (-not [Environment]::Is64BitOperatingSystem) { throw 'Windows x64 ist erforderlich.' }
$Tools = Join-Path $ProjectRoot '.tools'
New-Item -ItemType Directory -Force $Tools | Out-Null
function Download([string]$Url, [string]$Destination, [string]$Sha256 = '') {
    if (-not (Test-Path -LiteralPath $Destination)) { Invoke-WebRequest $Url -OutFile $Destination }
    if ($Sha256 -and (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash -ne $Sha256) {
        throw "Prüfsumme stimmt nicht: $Destination"
    }
}
if (-not (Test-Path -LiteralPath $Dotnet)) {
    $Installer = Join-Path $Tools 'dotnet-install.ps1'
    Download 'https://dot.net/v1/dotnet-install.ps1' $Installer
    & $Installer -Version '10.0.403' -InstallDir (Join-Path $Tools 'dotnet') -NoPath
    if (-not (Test-Path -LiteralPath $Dotnet)) { throw '.NET SDK wurde nicht eingerichtet.' }
}
if (-not (Test-Path -LiteralPath $Cargo)) {
    $Installer = Join-Path $Tools 'rustup-init.exe'
    Download 'https://win.rustup.rs/x86_64' $Installer
    & $Installer -y --no-modify-path --default-host x86_64-pc-windows-gnu --default-toolchain 1.99.1 --profile minimal
    Assert-NativeExit 'Rust-Setup'
}
if (-not (Test-Path -LiteralPath (Join-Path $Tools 'mingw64/bin/gcc.exe'))) {
    $Archive = Join-Path $Tools 'mingw.7z'
    Download 'https://github.com/niXman/mingw-builds-binaries/releases/download/16.2.0-rt_v14-rev1/x86_64-16.2.0-release-posix-seh-msvcrt-rt_v14-rev1.7z' $Archive 'A3CFB25037981EA1CD2F4E5452FA45C700B20FFFC311E5D78DE90DEDD142B73A'
    & tar -xf $Archive -C $Tools
    Assert-NativeExit 'Compiler entpacken'
}
$LibraArchive = Join-Path $Tools 'librashader.zip'
Download 'https://github.com/SnowflakePowered/librashader/releases/download/librashader-v0.12.0/librashader-x86_64-windows-v0.12.0-optimized.zip' $LibraArchive '521FE0F364BFA705883F9E99FB1733A3A24F0F403BCC5591B6B78F6CFF289183'
if (-not (Test-Path -LiteralPath (Join-Path $Tools 'librashader/librashader.dll'))) {
    Expand-Archive -LiteralPath $LibraArchive -DestinationPath (Join-Path $Tools 'librashader')
}
$ShaderArchive = Join-Path $Tools 'shaders.zip'
Download 'https://github.com/libretro/slang-shaders/archive/cb01f2f238700eed7ec1c859be1737a9fbcdcb7c.zip' $ShaderArchive 'E219396F38DEEC9A762FB6EF1C5D690FCD36E90E64E66A1C782FBB717F92820E'
$Assets = Join-Path $ProjectRoot 'assets'
New-Item -ItemType Directory -Force $Assets | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $Assets 'slang-shaders-cb01f2f238700eed7ec1c859be1737a9fbcdcb7c'))) {
    Expand-Archive -LiteralPath $ShaderArchive -DestinationPath $Assets
}
Write-Host 'Entwicklungswerkzeuge und Original-Shader sind eingerichtet.'
