# SPDX-License-Identifier: GPL-3.0-only
param([switch]$Offline, [switch]$UseCurrentPackage)
. "$PSScriptRoot/environment.ps1"
$Compiler = Join-Path $ProjectRoot '.tools/innosetup/ISCC.exe'
if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Installer-Compiler fehlt. Siehe docs/INSTALLER.md.' }
if (-not $UseCurrentPackage) { & "$PSScriptRoot/package.ps1" -Offline:$Offline }
$ReleaseDirectory = Join-Path $ProjectRoot ((Get-Content -Raw -LiteralPath (Join-Path $ProjectRoot 'runtime/current-release.txt')).Trim())
$Manifest = Get-Content -Raw -LiteralPath (Join-Path $ReleaseDirectory 'release-manifest.json') | ConvertFrom-Json
$PackageDirectory = Join-Path $ReleaseDirectory ($Manifest.package -replace '\.zip$', '')
$BuildDirectory = Join-Path $ReleaseDirectory 'installer-build'
New-Item -ItemType Directory -Force $BuildDirectory | Out-Null
$Probe = Join-Path $BuildDirectory 'prerequisite-probe.exe'
& (Join-Path $ProjectRoot '.tools/mingw64/bin/gcc.exe') '-Os' '-s' '-static' '-Wall' '-Wextra' (Join-Path $ProjectRoot 'installer/prerequisite-probe.c') '-o' $Probe
Assert-NativeExit '64-Bit-Komponentenprüfung bauen'
& $Compiler '/Q' ('/DPackageDir=' + $PackageDirectory) ('/DReleaseVersion=' + $Manifest.version) ('/DProbeFile=' + $Probe) `
    ('/DSourceArchive=' + (Join-Path $ReleaseDirectory $Manifest.source)) ('/DCompilerLicense=' + (Join-Path (Split-Path $Compiler) 'license.txt')) `
    ('/O' + $ReleaseDirectory) (Join-Path $ProjectRoot 'installer/TapeTube.iss')
Assert-NativeExit 'Installer bauen'
$Installer = Join-Path $ReleaseDirectory "TapeTube-$($Manifest.version)-win-x64-setup.exe"
$InstallerHash = (Get-FileHash -LiteralPath $Installer -Algorithm SHA256).Hash.ToLowerInvariant()
Add-Content -LiteralPath (Join-Path $ReleaseDirectory 'SHA256SUMS.txt') -Encoding ascii -Value "$InstallerHash  $(Split-Path $Installer -Leaf)"
[ordered]@{file=Split-Path $Installer -Leaf;sha256=$InstallerHash;version=$Manifest.version;compiler='Inno Setup 6.7.4';
  perUser=$true;prerequisites='Native x64 load checks; official Microsoft download links; no automatic runtime installation';
  ffmpegBundled=$false;ownSourceIncluded=$true;languages=@('de','en');signed=$false;published=$false;cleanMachineTest=$false} |
  ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $ReleaseDirectory 'installer-manifest.json') -Encoding utf8
Set-Content -LiteralPath (Join-Path $ProjectRoot 'runtime/current-installer.txt') -Value ('artifacts/releases/'+(Split-Path $ReleaseDirectory -Leaf)+'/'+(Split-Path $Installer -Leaf)) -Encoding utf8
Write-Output "Installer: $Installer"
