# SPDX-License-Identifier: GPL-3.0-only
param([string]$ReleaseDirectory, [ValidateSet('skip','download','existing')][string]$MediaMode='skip', [string]$MediaFolder)
. "$PSScriptRoot/environment.ps1"
if ($MediaFolder) { $MediaFolder=(Resolve-Path -LiteralPath $MediaFolder).Path }
if (-not $ReleaseDirectory) { $ReleaseDirectory=Join-Path $ProjectRoot ((Get-Content -Raw (Join-Path $ProjectRoot 'runtime/current-release.txt')).Trim()) }
$ReleaseDirectory=(Resolve-Path -LiteralPath $ReleaseDirectory).Path
$Metadata=Get-Content -Raw -LiteralPath (Join-Path $ReleaseDirectory 'installer-manifest.json') | ConvertFrom-Json
$Package=Get-Content -Raw -LiteralPath (Join-Path $ReleaseDirectory 'release-manifest.json') | ConvertFrom-Json
$RegistryKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{23CF420C-2372-49AC-AB78-2452D82F14D0}_is1'
if (Test-Path -LiteralPath $RegistryKey) { throw 'Vorhandene Installation: Test abgebrochen, um nichts zu überschreiben.' }
$TestRoot=Join-Path $ProjectRoot ('artifacts/installer-tests/' + [Guid]::NewGuid().ToString('N'))
$InstallDirectory=Join-Path $TestRoot 'Installed App'
New-Item -ItemType Directory -Force $TestRoot | Out-Null
$Installer=Join-Path $ReleaseDirectory $Metadata.file
if ((Get-FileHash -LiteralPath $Installer -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Metadata.sha256) { throw 'Installer-Prüfsumme stimmt nicht.' }
function Install([string]$LogName, [bool]$UseSavedSelection=$false) {
    $Arguments=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',('/MEDIA='+$MediaMode),('/DIR="'+$InstallDirectory+'"'),('/LOG="'+(Join-Path $TestRoot $LogName)+'"'))
    if ($MediaFolder -and -not $UseSavedSelection) { $Arguments += '/MEDIAFOLDER="'+(Resolve-Path -LiteralPath $MediaFolder).Path+'"' }
    $Process=Start-Process -FilePath $Installer -ArgumentList $Arguments -WindowStyle Hidden -PassThru -Wait
    if ($Process.ExitCode -ne 0) { throw "Installertest fehlgeschlagen: $($Process.ExitCode)" }
}
Install 'install.log'
$Checks=0
$PackageDirectory=Join-Path $ReleaseDirectory ($Package.package -replace '\.zip$', '')
foreach ($File in Get-ChildItem -LiteralPath $PackageDirectory -Recurse -File) {
    $Relative=$File.FullName.Substring($PackageDirectory.Length+1)
    if ((Get-FileHash -LiteralPath (Join-Path $InstallDirectory $Relative) -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $File.FullName -Algorithm SHA256).Hash) { throw "Installierte Datei weicht ab: $Relative" }
    $Checks++
}
$Source=Join-Path $InstallDirectory ('sources/'+$Package.source)
if ((Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath (Join-Path $ReleaseDirectory $Package.source) -Algorithm SHA256).Hash) { throw 'Passender Quellcode fehlt.' }
if (-not (Test-Path -LiteralPath $RegistryKey)) { throw 'Deinstallationseintrag fehlt.' }
$UserFile=Join-Path $InstallDirectory 'keep-user-file.txt'
Set-Content -LiteralPath $UserFile -Value 'Installer test: preserve user-owned files.' -Encoding utf8
$Override=Join-Path $InstallDirectory 'components.local.json'
Set-Content -LiteralPath $Override -Value '{}' -Encoding ascii
Install 'upgrade.log' ($MediaMode -eq 'existing')
if ((Get-Content -Raw $UserFile).Trim() -ne 'Installer test: preserve user-owned files.' -or (Get-Content -Raw $Override).Trim() -ne '{}') { throw 'Benutzerdateien beim erneuten Installieren verändert.' }
$Run=Start-Process -FilePath (Join-Path $InstallDirectory 'TapeTube.exe') -ArgumentList '--smoke' -WindowStyle Hidden -PassThru -Wait
if ($Run.ExitCode -ne 0) { throw 'Installierte App besteht den Effekttest nicht.' }
$Smoke=Get-Content -Raw -LiteralPath (Join-Path $InstallDirectory 'artifacts/smoke.json') | ConvertFrom-Json
$MediaChecks=@()
if ($MediaMode -ne 'skip') {
    foreach ($Tool in @('ffmpeg','ffprobe')) {
        $ToolPath=if($MediaMode -eq 'existing'){Join-Path $MediaFolder "$Tool.exe"}else{Join-Path $InstallDirectory "runtime/media/$Tool.exe"}
        if (-not (Test-Path -LiteralPath $ToolPath)) { throw "Medienwerkzeug fehlt: $Tool" }
        $Selection=Get-Content -LiteralPath (Join-Path $InstallDirectory 'runtime/media-selection.ini')
        if (-not ($Selection | Where-Object { $_ -eq "$Tool=$((Resolve-Path -LiteralPath $ToolPath).Path)" })) { throw "Medienauswahl fehlt: $Tool" }
        if ($MediaMode -eq 'existing' -and (Test-Path -LiteralPath (Join-Path $InstallDirectory "runtime/media/$Tool.exe"))) { throw 'Externe Werkzeuge wurden unerwartet kopiert.' }
    }
    if ($MediaMode -eq 'download') {
        foreach($Notice in @('LICENSE','README.txt','doc/ffmpeg.html','ffmpeg-download.json')) {
            if (-not (Test-Path -LiteralPath (Join-Path $InstallDirectory "licenses/ffmpeg/$Notice"))) { throw "FFmpeg-Hinweis fehlt: $Notice" }
        }
        if ((Get-Content -Raw (Join-Path $TestRoot 'install.log')) -notmatch 'pinned archive SHA-256 verified') { throw 'Download-Prüfung fehlt im Setup-Protokoll.' }
        $ToolVersion=& (Join-Path $InstallDirectory 'runtime/media/ffmpeg.exe') -version
        Assert-NativeExit 'Installiertes FFmpeg starten'
        if ($ToolVersion[0] -notmatch ([regex]::Escape($Metadata.ffmpegOptionalDownload.version))) { throw 'Falsche FFmpeg-Version installiert.' }
    }
    foreach($Suite in @(@{arg='--video-smoke';report='video-smoke.json'},@{arg='--export-smoke';report='export-smoke.json'})) {
        $Run=Start-Process -FilePath (Join-Path $InstallDirectory 'TapeTube.exe') -ArgumentList $Suite.arg -WindowStyle Hidden -PassThru -Wait
        if($Run.ExitCode -ne 0) { throw "Installierte Medienprüfung fehlgeschlagen: $($Suite.arg)" }
        $SuiteResult=Get-Content -Raw -LiteralPath (Join-Path $InstallDirectory "artifacts/$($Suite.report)") | ConvertFrom-Json
        if($Suite.arg -eq '--video-smoke' -and $MediaMode -eq 'existing' -and
           $SuiteResult.ffmpeg -ne (Resolve-Path -LiteralPath (Join-Path $MediaFolder 'ffmpeg.exe')).Path) { throw 'App verwendet nicht das gewählte externe FFmpeg.' }
        $MediaChecks+=@{suite=$Suite.arg;passed=$SuiteResult.passed;count=$SuiteResult.count}
    }
    if($MediaMode -eq 'existing') {
        $SelectionFile=Join-Path $InstallDirectory 'runtime/media-selection.ini'
        $SavedSelection=[IO.File]::ReadAllBytes($SelectionFile)
        $MissingTool=Join-Path $MediaFolder 'missing-selected-ffmpeg.exe'
        try {
            "[tools]`r`nffmpeg=$MissingTool`r`nffprobe=$(Join-Path $MediaFolder 'ffprobe.exe')" |
                Set-Content -LiteralPath $SelectionFile -Encoding Unicode
            $Run=Start-Process -FilePath (Join-Path $InstallDirectory 'TapeTube.exe') -ArgumentList '--video-smoke' -WindowStyle Hidden -PassThru -Wait
            $ErrorText=Get-Content -Raw -LiteralPath (Join-Path $InstallDirectory 'artifacts/video-smoke-error.txt')
            if($Run.ExitCode -eq 0 -or -not $ErrorText.Contains($MissingTool)) { throw 'Fehlende Auswahl wurde still durch PATH ersetzt.' }
        } finally { [IO.File]::WriteAllBytes($SelectionFile,$SavedSelection) }
    }
} elseif (Test-Path -LiteralPath (Join-Path $InstallDirectory 'runtime/media/ffmpeg.exe')) { throw 'FFmpeg trotz Überspringen installiert.' }
$Uninstaller=Join-Path $InstallDirectory 'unins000.exe'
$Run=Start-Process -FilePath $Uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $TestRoot 'uninstall.log')+'"')) -WindowStyle Hidden -PassThru -Wait
if ($Run.ExitCode -ne 0) { throw 'Deinstallation fehlgeschlagen.' }
if (Test-Path -LiteralPath (Join-Path $InstallDirectory 'TapeTube.exe')) { throw 'App nach Deinstallation noch vorhanden.' }
if (Test-Path -LiteralPath $RegistryKey) { throw 'Deinstallationseintrag wurde nicht entfernt.' }
if (-not (Test-Path -LiteralPath $UserFile) -or -not (Test-Path -LiteralPath $Override)) { throw 'Benutzerdateien bei Deinstallation entfernt.' }
if (Test-Path -LiteralPath (Join-Path $InstallDirectory 'runtime/media/ffmpeg.exe')) { throw 'Installiertes FFmpeg bei Deinstallation nicht entfernt.' }
if ($MediaMode -eq 'existing' -and -not (Test-Path -LiteralPath (Join-Path $MediaFolder 'ffmpeg.exe'))) { throw 'Eigene FFmpeg-Quelle wurde verändert.' }
[ordered]@{passed=$true;installer=$Metadata.file;sha256=$Metadata.sha256;installedFilesVerified=$Checks;sourceCompanionVerified=$true;
  perUserInstall=$true;repeatInstallVerified=$true;effectChecks=$Smoke.checks;uninstallVerified=$true;userFilesPreserved=$true;
  mediaMode=$MediaMode;savedExternalSelectionOnUpgrade=($MediaMode -eq 'existing');missingSelectedToolRejected=($MediaMode -eq 'existing');mediaChecks=$MediaChecks;startMenuTested=$false;optionalFfmpegSelectionTested=($MediaMode -eq 'existing');ffmpegDownloadTested=($MediaMode -eq 'download');missingPrerequisitesTested=$false;cleanMachineTest=$false} |
  ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $ReleaseDirectory "installer-validation-$MediaMode.json") -Encoding utf8
Get-Content -LiteralPath (Join-Path $ReleaseDirectory "installer-validation-$MediaMode.json")
