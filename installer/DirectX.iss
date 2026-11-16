{ SPDX-License-Identifier: GPL-3.0-only }
{ Included inside [Code]. Preparation does not install system components. }
var
  DirectXDownloadPage: TDownloadWizardPage;

function DirectXDirectory: String;
begin
  Result := ExpandConstant('{tmp}\directx-runtime');
end;

function PrepareDirectX: String;
var Code: Integer; SetupFile: String;
begin
  Result := '';
  DirectXDownloadPage.Clear;
  DirectXDownloadPage.Add('{#DirectXUrl}', '{#DirectXFile}', '{#DirectXSHA256}');
  DirectXDownloadPage.Show;
  try
    try
      DirectXDownloadPage.Download;
      Log('DirectX: Microsoft download SHA-256 verified');
      if not ForceDirectories(DirectXDirectory) then RaiseException(CustomMessage('DXExtractFailed'));
      { /Q /T extracts only; DXSETUP below retains its interactive license UI. }
      if not Exec(ExpandConstant('{tmp}\{#DirectXFile}'), '/Q /T:"' + DirectXDirectory + '"',
          '', SW_HIDE, ewWaitUntilTerminated, Code) then
        RaiseException(CustomMessage('DXExtractFailed') + ' ' + SysErrorMessage(Code));
      if Code <> 0 then RaiseException(CustomMessage('DXExtractFailed') + ' (' + IntToStr(Code) + ')');
      SetupFile := DirectXDirectory + '\DXSETUP.exe';
      if not FileExists(SetupFile) or not FileExists(DirectXDirectory + '\DSETUP.dll') or
         not FileExists(DirectXDirectory + '\dsetup32.dll') or
         not FileExists(DirectXDirectory + '\Jun2010_d3dx9_43_x64.cab') then
        RaiseException(CustomMessage('DXExtractFailed'));
      if CompareText(GetSHA256OfFile(SetupFile), '{#DirectXSetupSHA256}') <> 0 then
        RaiseException(CustomMessage('DXExtractFailed'));
      Log('DirectX: automatic extraction completed; DXSETUP SHA-256 verified');
    except
      Log('DirectX preparation: ' + GetExceptionMessage);
      Result := CustomMessage('DXFailed') + #13#10 + GetExceptionMessage;
    end;
  finally
    DirectXDownloadPage.Hide;
  end;
end;
