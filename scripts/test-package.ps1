# SPDX-License-Identifier: GPL-3.0-only
param([string]$QaRoot)
. "$PSScriptRoot/environment.ps1"
$RelativeRelease = (Get-Content -LiteralPath (Join-Path $ProjectRoot 'runtime/current-release.txt') -Raw).Trim()
$ReleaseDirectory = Join-Path $ProjectRoot $RelativeRelease
$Manifest = Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'release-manifest.json') -Raw | ConvertFrom-Json
if (-not $QaRoot) { $QaRoot = Join-Path $ProjectRoot 'artifacts/isolated-package-tests' }
$QaDirectory = Join-Path $QaRoot ('TapeTube-QA-' + [Guid]::NewGuid().ToString('N'))
Write-Host "Prüfverzeichnis: $QaDirectory"
New-Item -ItemType Directory -Force $QaDirectory | Out-Null
New-Item -ItemType Directory -Force (Join-Path $ProjectRoot 'artifacts/packaging') | Out-Null
Set-Content -LiteralPath (Join-Path $ProjectRoot 'artifacts/packaging/last-qa-path.txt') -Value $QaDirectory -Encoding utf8
Expand-Archive -LiteralPath (Join-Path $ReleaseDirectory $Manifest.package) -DestinationPath $QaDirectory
$Package = Get-ChildItem -LiteralPath $QaDirectory -Directory | Select-Object -First 1
$Executable = Join-Path $Package.FullName 'TapeTube.exe'
foreach ($Required in @('TapeTube.exe','coreclr.dll','hostfxr.dll','ntscrt_ntsc.dll','librashader.dll','release.json','components.json','LICENSE')) {
    if (-not (Test-Path -LiteralPath (Join-Path $Package.FullName $Required))) { throw "Portable Datei fehlt: $Required" }
}
$RuntimeConfig = Get-Content -LiteralPath (Join-Path $Package.FullName 'TapeTube.runtimeconfig.json') -Raw | ConvertFrom-Json
if ($RuntimeConfig.runtimeOptions.framework -or $RuntimeConfig.runtimeOptions.frameworks) { throw 'Die Ausgabe benötigt noch eine externe .NET-Laufzeit.' }
if (-not $RuntimeConfig.runtimeOptions.includedFrameworks) { throw 'Keine enthaltene .NET-Laufzeit ausgewiesen.' }
if (Test-Path -LiteralPath (Join-Path $Package.FullName 'runtime/media-tools.json')) { throw 'Persönliche Medienkonfiguration wurde mitgepackt.' }
$LicenseRoot = Join-Path $Package.FullName 'licenses'
foreach ($Required in @('README.md','DEPENDENCY_LICENSE_REVIEW.md','SHADER_LICENSE_REVIEW.md','SHADER_LICENSE_REQUEST.md','shader-license-audit.json','shader-license-evidence.json','librashader-release-evidence.json','upstream/sources.json','librashader/LICENSE.md','librashader/LICENSE-GPL.md','librashader/librashader.h','rust/hifijson-0.5.0/PACKAGE-METADATA.toml','nuget/microsoft.windowsdesktop.app.runtime.win-x64-10.0.14/WindowsDesktop-THIRD-PARTY-NOTICES.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $LicenseRoot $Required))) { throw "Lizenzbeleg fehlt im Paket: $Required" }
}
$Inventory = Get-Content -Raw -LiteralPath (Join-Path $LicenseRoot 'dependency-inventory.json') | ConvertFrom-Json
foreach ($Entry in $Inventory.entries) {
    $Directory = if ($Entry.ecosystem -eq 'rust') { "rust/$($Entry.name)-$($Entry.version)" } else { 'nuget/' + $Entry.name.Replace('/','-') }
    foreach ($Notice in $Entry.notices) {
        if (-not (Test-Path -LiteralPath (Join-Path $LicenseRoot "$Directory/$Notice"))) { throw "Inventarisierter Lizenztext fehlt: $Directory/$Notice" }
    }
}
$ShaderAudit = Get-Content -Raw -LiteralPath (Join-Path $LicenseRoot 'shader-license-audit.json') | ConvertFrom-Json
if ($ShaderAudit.presets.Count -ne 7 -or -not $ShaderAudit.releasePolicy.documentedScopeUncertaintiesAccepted) { throw 'Preset-Auswahl oder vereinbarter Lizenzmaßstab fehlt.' }
foreach ($Entry in $ShaderAudit.files) {
    $Actual = Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Package.FullName "assets/slang-shaders-selected/$($Entry.path)")
    if ($Actual.Hash.ToLowerInvariant() -ne $Entry.sha256) { throw "Shader-Beleg passt nicht zum Paket: $($Entry.path)" }
}
$LibraEvidence = Get-Content -Raw -LiteralPath (Join-Path $LicenseRoot 'librashader-release-evidence.json') | ConvertFrom-Json
if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Package.FullName 'librashader.dll')).Hash.ToLowerInvariant() -ne $LibraEvidence.dllSha256) { throw 'Native Bibliothek weicht vom Herkunftsnachweis ab.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$Source = [IO.Compression.ZipFile]::OpenRead((Join-Path $ReleaseDirectory $Manifest.source))
try {
    $Entries = @($Source.Entries | ForEach-Object { $_.FullName.Replace('\','/') })
    foreach ($Required in @('/native/src/lib.rs','/native/Cargo.lock','/src/Ntscrt.Windows/packages.lock.json','/LICENSE','/licenses/upstream/sources.json','/docs/shader-license-evidence.json','/docs/librashader-release-evidence.json')) {
        if (-not ($Entries | Where-Object { $_.EndsWith($Required) })) { throw "Quellcode-Datei fehlt: $Required" }
    }
    if ($Entries | Where-Object { $_ -match '/(\.tools|bin|obj|artifacts)/|components\.local\.json|media-tools\.json|current-app\.txt' }) { throw 'Persönliche oder generierte Dateien in Quellcode-ZIP gefunden.' }
    foreach ($Entry in $Source.Entries) {
        if ($Entry.FullName -match '\.(cs|rs|ps1|mjs|json|md)$') {
            $Reader = [IO.StreamReader]::new($Entry.Open())
            try { $Text = $Reader.ReadToEnd() } finally { $Reader.Dispose() }
            if ($Text.Contains($ProjectRoot) -or $Text.Contains($ProjectRoot.Replace('\','\\'))) { throw "Lokaler Projektpfad in Quellcode-ZIP: $($Entry.FullName)" }
        }
    }
} finally { $Source.Dispose() }

$SavedEnvironment = @{}
foreach ($Name in @('DOTNET_ROOT','DOTNET_MULTILEVEL_LOOKUP','PATH','TAPETUBE_FFMPEG','TAPETUBE_FFPROBE')) {
    $SavedEnvironment[$Name] = [Environment]::GetEnvironmentVariable($Name,'Process')
}
$Ffmpeg = (Get-Command ffmpeg.exe -ErrorAction Stop).Source
$Ffprobe = (Get-Command ffprobe.exe -ErrorAction Stop).Source
$Results = @()
function Run-PortableTest([string]$Argument,[string]$Report) {
    Write-Host "Portable Prüfung: $Argument"
    $Process = Start-Process -FilePath $Executable -ArgumentList $Argument -WorkingDirectory $QaDirectory -WindowStyle Hidden -PassThru
    if (-not $Process.WaitForExit(180000)) { $Process.Kill($true); throw "Portable Prüfung überschreitet drei Minuten: $Argument" }
    if ($Process.ExitCode -ne 0) {
        Get-ChildItem -LiteralPath (Join-Path $Package.FullName 'artifacts') -Filter '*error.txt' -File | ForEach-Object { Get-Content -LiteralPath $_.FullName }
        throw "Portable Prüfung fehlgeschlagen: $Argument"
    }
    return Get-Content -LiteralPath (Join-Path $Package.FullName "artifacts/$Report") -Raw | ConvertFrom-Json
}
try {
    $env:DOTNET_ROOT = Join-Path $QaDirectory 'no-installed-dotnet'
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    $env:PATH = Join-Path $env:SystemRoot 'System32'
    $env:TAPETUBE_FFMPEG = $Ffmpeg; $env:TAPETUBE_FFPROBE = $Ffprobe
    $Results += Run-PortableTest '--smoke' 'smoke.json'
    $Results += Run-PortableTest '--video-smoke' 'video-smoke.json'
    $Results += Run-PortableTest '--export-smoke' 'export-smoke.json'
    $Results += Run-PortableTest '--output-smoke' 'output-smoke.json'
    $Results += Run-PortableTest '--language-smoke' 'language-smoke.json'
    $Results += Run-PortableTest '--update-smoke' 'update-smoke.json'

    $Custom = Join-Path $Package.FullName 'custom-components'
    New-Item -ItemType Directory -Force $Custom | Out-Null
    Copy-Item -LiteralPath (Join-Path $Package.FullName 'ntscrt_ntsc.dll'),(Join-Path $Package.FullName 'librashader.dll') -Destination $Custom
    Copy-Item -LiteralPath (Join-Path $Package.FullName 'assets/slang-shaders-selected') -Destination (Join-Path $Custom 'shaders') -Recurse
    @{ntscLibrary='custom-components/ntscrt_ntsc.dll';librashaderLibrary='custom-components/librashader.dll';shaderRoot='custom-components/shaders';shaderPresets=@('crt/crt-easymode.slangp')} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Package.FullName 'components.local.json') -Encoding utf8
    $Override = Run-PortableTest '--smoke' 'smoke.json'
    if ($Override.ntscLibrary -notmatch 'custom-components' -or $Override.librashaderLibrary -notmatch 'custom-components' -or $Override.shader -notmatch 'custom-components') {
        throw 'Eigene Komponentenpfade wurden nicht verwendet.'
    }
    $Report = [ordered]@{passed=$true;package=$Manifest.package;version=$Manifest.version;
        includedRuntime=$true;independentDirectory=$true;sourceArchiveChecked=$true;personalConfigurationExcluded=$true;
        ffmpegExternal=$true;cleanMachineTest=$false;componentOverridesVerified=$true;
        licenseCollectionVerified=$true;shaderPresets=$ShaderAudit.presets.Count;shaderFilesVerified=$ShaderAudit.files.Count;
        libraryProvenanceVerified=$true;nativeTransitiveNoticeReviewComplete=$false;
        suites=@($Results | ForEach-Object { @{passed=$_.passed;checks=if ($_.checks -is [array]) {$_.count} elseif ($_.checks) {$_.checks} else {$_.count}} });
        isolatedTestDirectory='separate validation directory; private path is kept outside release artifacts'}
    $Report | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $ReleaseDirectory 'package-validation.json') -Encoding utf8
    $Report | ConvertTo-Json -Depth 7
} finally {
    foreach ($Name in $SavedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($Name,$SavedEnvironment[$Name],'Process') }
}
