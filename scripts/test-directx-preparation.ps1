# SPDX-License-Identifier: GPL-3.0-only
param([switch]$RejectHash)
. "$PSScriptRoot/environment.ps1"
$ReleaseDirectory=Join-Path $ProjectRoot ((Get-Content -Raw (Join-Path $ProjectRoot 'runtime/current-release.txt')).Trim())
$Mode=if($RejectHash){'rejected-hash'}else{'download-extract'}
$TestRoot=Join-Path $ProjectRoot ('artifacts/directx-tests/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $TestRoot | Out-Null
$Include=Get-Content -Raw -LiteralPath (Join-Path $ReleaseDirectory 'installer-build/directx-download.iss.inc')
$Pin=Get-Content -Raw -LiteralPath (Join-Path $ProjectRoot 'installer/directx-download.json') | ConvertFrom-Json
if($RejectHash){$Include=$Include.Replace($Pin.sha256,('0'*64))}
$Include | Set-Content -LiteralPath (Join-Path $TestRoot 'pin.iss.inc') -Encoding ascii
# Includes the production routine; no DXSETUP launch, app payload or uninstall entry.
$Harness=@'
#include "pin.iss.inc"
[Setup]
AppName=TapeTube DirectX preparation test
AppVersion=1.0
AppId=TapeTube-DirectX-Preparation-Test
DefaultDirName={tmp}\directx-preparation-test
CreateAppDir=no
Uninstallable=no
PrivilegesRequired=lowest
DisableWelcomePage=yes
OutputBaseFilename=directx-preparation-test
[CustomMessages]
DXExtractFailed=DirectX extraction or verification failed.
DXFailed=DirectX preparation failed.
[Code]
#include "__HELPER__"
procedure InitializeWizard;
begin
  DirectXDownloadPage := CreateDownloadPage('DirectX preparation test', 'No system installation will be started.', nil);
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := PrepareDirectX;
  if Result = '' then
    if not SaveStringToFile(ExpandConstant('{param:REPORT}'), 'prepared-without-system-installation', False) then
      Result := 'Could not write test evidence';
end;
'@
$Harness.Replace('__HELPER__',(Join-Path $ProjectRoot 'installer/DirectX.iss')) |
    Set-Content -LiteralPath (Join-Path $TestRoot 'test.iss') -Encoding utf8
& (Join-Path $ProjectRoot '.tools/innosetup/ISCC.exe') /Q ('/O'+$TestRoot) (Join-Path $TestRoot 'test.iss')
Assert-NativeExit 'DirectX-Prüfprogramm bauen'
$Log=Join-Path $TestRoot 'prepare.log'
$Evidence=Join-Path $TestRoot 'prepared.txt'
$Run=Start-Process -FilePath (Join-Path $TestRoot 'directx-preparation-test.exe') -ArgumentList @(
  '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/REPORT="'+$Evidence+'"'),('/LOG="'+$Log+'"')) -WindowStyle Hidden -PassThru -Wait
$Text=Get-Content -Raw -LiteralPath $Log
if($RejectHash){
    if($Run.ExitCode -ne 7 -or (Test-Path -LiteralPath $Evidence) -or
        $Text -notmatch 'Verification error: File hash is incorrect' -or $Text -match 'automatic extraction completed') {
        throw 'DirectX-Prüfsummenfehler wurde nicht korrekt abgefangen.'
    }
} else {
    if($Run.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $Evidence) -or
        $Text -notmatch 'automatic extraction completed; DXSETUP SHA-256 verified') {
        throw 'DirectX-Download/Extraktion nicht bestätigt. Siehe artifacts/directx-tests.'
    }
}
[ordered]@{passed=$true;mode=$Mode;archiveSha256=$Pin.sha256;setupSha256=$Pin.setupSha256;
  productionPreparationRoutine=$true;automaticExtractionVerified=(-not $RejectHash);hashRejectionVerified=[bool]$RejectHash;
  systemInstallerLaunched=$false;missingPrerequisiteInstallTested=$false} |
  ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ReleaseDirectory "directx-validation-$Mode.json") -Encoding utf8
Get-Content -LiteralPath (Join-Path $ReleaseDirectory "directx-validation-$Mode.json")
