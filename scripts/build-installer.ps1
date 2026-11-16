# SPDX-License-Identifier: GPL-3.0-only
param([switch]$Offline, [switch]$UseCurrentPackage)
. "$PSScriptRoot/environment.ps1"
$Compiler = Join-Path $ProjectRoot '.tools/innosetup/ISCC.exe'
if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Installer-Compiler fehlt. Siehe docs/INSTALLER.md.' }
if (-not $UseCurrentPackage) { & "$PSScriptRoot/package.ps1" -Offline:$Offline }
$ReleaseDirectory = Join-Path $ProjectRoot ((Get-Content -Raw -LiteralPath (Join-Path $ProjectRoot 'runtime/current-release.txt')).Trim())
$Manifest = Get-Content -Raw -LiteralPath (Join-Path $ReleaseDirectory 'release-manifest.json') | ConvertFrom-Json
$CurrentRelease = Get-Content -Raw -LiteralPath (Join-Path $ProjectRoot 'release.json') | ConvertFrom-Json
if ($Manifest.version -ne $CurrentRelease.version) { throw 'Paketversion passt nicht zum aktuellen Quellstand. Frisches Paket bauen.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$SourceZip = [IO.Compression.ZipFile]::OpenRead((Join-Path $ReleaseDirectory $Manifest.source))
try {
    # A reused package must not silently ship stale installer sources.
    foreach ($Relative in @('scripts/build-installer.ps1','release.json','dependencies.json') +
        @(Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'installer') -File | ForEach-Object { 'installer/' + $_.Name })) {
        $Entry = $SourceZip.Entries | Where-Object { $_.FullName.Replace('\','/').EndsWith('/'+$Relative) }
        if (-not $Entry) { throw "Installer-Quelle fehlt im Quellpaket: $Relative. Frisches Paket bauen." }
        $Stream = $Entry.Open(); $Hasher = [Security.Cryptography.SHA256]::Create()
        try { $ArchivedHash = [BitConverter]::ToString($Hasher.ComputeHash($Stream)).Replace('-','').ToLowerInvariant() }
        finally { $Stream.Dispose(); $Hasher.Dispose() }
        if ($ArchivedHash -ne (Get-FileHash -LiteralPath (Join-Path $ProjectRoot $Relative) -Algorithm SHA256).Hash.ToLowerInvariant()) {
            throw "Installer-Quelle hat sich seit dem Paketbau geändert: $Relative. Frisches Paket bauen."
        }
    }
} finally { $SourceZip.Dispose() }
$PackageDirectory = Join-Path $ReleaseDirectory ($Manifest.package -replace '\.zip$', '')
$BuildDirectory = Join-Path $ReleaseDirectory 'installer-build'
New-Item -ItemType Directory -Force $BuildDirectory | Out-Null
$Media = Get-Content -Raw -LiteralPath (Join-Path $ProjectRoot 'installer/ffmpeg-download.json') | ConvertFrom-Json
if ($Media.version -notmatch '^\d+\.\d+\.\d+$' -or $Media.root -ne "ffmpeg-$($Media.version)-essentials_build" -or
    $Media.archive -ne "$($Media.root).7z" -or $Media.sha256 -notmatch '^[0-9a-f]{64}$' -or
    $Media.url -ne "https://github.com/GyanD/codexffmpeg/releases/download/$($Media.version)/$($Media.archive)") {
    throw 'Ungültiger festgelegter FFmpeg-Download.'
}
$MediaInclude = Join-Path $BuildDirectory 'ffmpeg-download.iss.inc'
@("#define FFmpegVersion `"$($Media.version)`"", "#define FFmpegRoot `"$($Media.root)`"",
  "#define FFmpegUrl `"$($Media.url)`"", "#define FFmpegArchive `"$($Media.archive)`"",
  "#define FFmpegSHA256 `"$($Media.sha256)`"") | Set-Content -LiteralPath $MediaInclude -Encoding ascii
$DirectX = Get-Content -Raw -LiteralPath (Join-Path $ProjectRoot 'installer/directx-download.json') | ConvertFrom-Json
if ($DirectX.file -ne 'directx_Jun2010_redist.exe' -or $DirectX.url -notmatch '^https://download\.microsoft\.com/download/[a-zA-Z0-9/-]+/directx_Jun2010_redist\.exe$' -or
    $DirectX.sha256 -notmatch '^[0-9a-f]{64}$' -or $DirectX.setupSha256 -notmatch '^[0-9a-f]{64}$') {
    throw 'Ungültiger festgelegter Microsoft-DirectX-Download.'
}
$DirectXInclude = Join-Path $BuildDirectory 'directx-download.iss.inc'
@("#define DirectXUrl `"$($DirectX.url)`"", "#define DirectXFile `"$($DirectX.file)`"",
  "#define DirectXSHA256 `"$($DirectX.sha256)`"", "#define DirectXSetupSHA256 `"$($DirectX.setupSha256)`"") |
  Set-Content -LiteralPath $DirectXInclude -Encoding ascii
$Probe = Join-Path $BuildDirectory 'prerequisite-probe.exe'
& (Join-Path $ProjectRoot '.tools/mingw64/bin/gcc.exe') '-Os' '-s' '-static' '-Wall' '-Wextra' (Join-Path $ProjectRoot 'installer/prerequisite-probe.c') '-o' $Probe
Assert-NativeExit '64-Bit-Komponentenprüfung bauen'
$MediaProbe = Join-Path $BuildDirectory 'media-probe.exe'
& (Join-Path $ProjectRoot '.tools/mingw64/bin/gcc.exe') '-Os' '-s' '-static' '-municode' '-Wall' '-Wextra' (Join-Path $ProjectRoot 'installer/media-probe.c') '-o' $MediaProbe
Assert-NativeExit 'Medienwerkzeug-Prüfung bauen'
& $Compiler '/Q' ('/DPackageDir=' + $PackageDirectory) ('/DReleaseVersion=' + $Manifest.version) ('/DProbeFile=' + $Probe) `
    ('/DFFmpegIncludeFile=' + $MediaInclude) `
    ('/DDirectXIncludeFile=' + $DirectXInclude) `
    ('/DMediaProbeFile=' + $MediaProbe) `
    ('/DSourceArchive=' + (Join-Path $ReleaseDirectory $Manifest.source)) ('/DCompilerLicense=' + (Join-Path (Split-Path $Compiler) 'license.txt')) `
    ('/O' + $ReleaseDirectory) (Join-Path $ProjectRoot 'installer/TapeTube.iss')
Assert-NativeExit 'Installer bauen'
$Installer = Join-Path $ReleaseDirectory "TapeTube-$($Manifest.version)-win-x64-setup.exe"
$InstallerHash = (Get-FileHash -LiteralPath $Installer -Algorithm SHA256).Hash.ToLowerInvariant()
Add-Content -LiteralPath (Join-Path $ReleaseDirectory 'SHA256SUMS.txt') -Encoding ascii -Value "$InstallerHash  $(Split-Path $Installer -Leaf)"
[ordered]@{file=Split-Path $Installer -Leaf;sha256=$InstallerHash;version=$Manifest.version;compiler='Inno Setup 6.7.4';
  perUser=$true;prerequisites='Native x64 load checks; official VC link; optional pinned Microsoft DirectX download, automatic extraction, interactive elevated DXSETUP and recheck';directxOptionalDownload=$DirectX;
  ffmpegBundled=$false;ffmpegOptionalDownload=$Media;ownSourceIncluded=$true;languages=@('de','en');signed=$false;published=$false;cleanMachineTest=$false} |
  ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $ReleaseDirectory 'installer-manifest.json') -Encoding utf8
Set-Content -LiteralPath (Join-Path $ProjectRoot 'runtime/current-installer.txt') -Value ('artifacts/releases/'+(Split-Path $ReleaseDirectory -Leaf)+'/'+(Split-Path $Installer -Leaf)) -Encoding utf8
Write-Output "Installer: $Installer"
