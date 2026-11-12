# SPDX-License-Identifier: GPL-3.0-only
param([string]$ReleaseDirectory)
. "$PSScriptRoot/environment.ps1"
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
function Install([string]$LogName) {
    $Process=Start-Process -FilePath $Installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',('/DIR="'+$InstallDirectory+'"'),('/LOG="'+(Join-Path $TestRoot $LogName)+'"')) -WindowStyle Hidden -PassThru -Wait
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
Install 'upgrade.log'
if ((Get-Content -Raw $UserFile).Trim() -ne 'Installer test: preserve user-owned files.' -or (Get-Content -Raw $Override).Trim() -ne '{}') { throw 'Benutzerdateien beim erneuten Installieren verändert.' }
$Run=Start-Process -FilePath (Join-Path $InstallDirectory 'TapeTube.exe') -ArgumentList '--smoke' -WindowStyle Hidden -PassThru -Wait
if ($Run.ExitCode -ne 0) { throw 'Installierte App besteht den Effekttest nicht.' }
$Smoke=Get-Content -Raw -LiteralPath (Join-Path $InstallDirectory 'artifacts/smoke.json') | ConvertFrom-Json
$Uninstaller=Join-Path $InstallDirectory 'unins000.exe'
$Run=Start-Process -FilePath $Uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $TestRoot 'uninstall.log')+'"')) -WindowStyle Hidden -PassThru -Wait
if ($Run.ExitCode -ne 0) { throw 'Deinstallation fehlgeschlagen.' }
if (Test-Path -LiteralPath (Join-Path $InstallDirectory 'TapeTube.exe')) { throw 'App nach Deinstallation noch vorhanden.' }
if (Test-Path -LiteralPath $RegistryKey) { throw 'Deinstallationseintrag wurde nicht entfernt.' }
if (-not (Test-Path -LiteralPath $UserFile) -or -not (Test-Path -LiteralPath $Override)) { throw 'Benutzerdateien bei Deinstallation entfernt.' }
[ordered]@{passed=$true;installer=$Metadata.file;sha256=$Metadata.sha256;installedFilesVerified=$Checks;sourceCompanionVerified=$true;
  perUserInstall=$true;repeatInstallVerified=$true;effectChecks=$Smoke.checks;uninstallVerified=$true;userFilesPreserved=$true;
  startMenuTested=$false;optionalFfmpegSelectionTested=$false;missingPrerequisitesTested=$false;cleanMachineTest=$false} |
  ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $ReleaseDirectory 'installer-validation.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'installer-validation.json')
