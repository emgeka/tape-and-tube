{ SPDX-License-Identifier: GPL-3.0-only }
function ProbeMediaPair(Directory: String; var Version: String): Boolean;
var Code: Integer; Report: String;
begin
  Result := False;
  if not FileExists(AddBackslash(Directory) + 'ffmpeg.exe') or
     not FileExists(AddBackslash(Directory) + 'ffprobe.exe') then Exit;
  if not MediaProbeReady then begin ExtractTemporaryFile('media-probe.exe'); MediaProbeReady := True; end;
  Report := ExpandConstant('{tmp}\media-probe-result.ini');
  DeleteFile(Report);
  if not Exec(ExpandConstant('{tmp}\media-probe.exe'), '"' + Directory + '" "' + Report + '"',
      '', SW_HIDE, ewWaitUntilTerminated, Code) then Exit;
  if Code <> 0 then begin Log('FFmpeg pair rejected, probe code: ' + IntToStr(Code)); Exit; end;
  Version := GetIniString('tools', 'version', '', Report);
  Result := Version <> '';
end;

function AddMediaCandidate(Directory: String): Integer;
var I: Integer; Version: String;
begin
  Result := -1;
  Directory := Trim(Directory);
  if (Length(Directory) >= 2) and (Directory[1] = '"') and (Directory[Length(Directory)] = '"') then
    Directory := Copy(Directory, 2, Length(Directory) - 2);
  if Directory = '' then Exit;
  Directory := RemoveBackslashUnlessRoot(ExpandFileName(Directory));
  for I := 0 to MediaDirectories.Count - 1 do
    if CompareText(MediaDirectories[I], Directory) = 0 then begin Result := I; Exit; end;
  if not ProbeMediaPair(Directory, Version) then Exit;
  Result := MediaDirectories.Add(Directory);
  MediaVersions.Add(Version);
  if Pos('-essentials_build', Version) > 0 then
    Version := Copy(Version, 1, Pos('-essentials_build', Version) - 1);
  MediaList.Items.Add(Version + ' | ' + Directory);
end;

procedure DiscoverMedia;
var Destination, Previous, Saved, Directory, PathValue, EnvFfmpeg, EnvFfprobe: String;
    I, Separator, Preferred: Integer;
begin
  if MediaScanning then Exit;
  MediaScanning := True;
  try
    Previous := '';
    if MediaList.ItemIndex >= 0 then Previous := MediaDirectories[MediaList.ItemIndex];
    MediaDirectories.Clear; MediaVersions.Clear; MediaList.Items.Clear;
    Destination := WizardDirValue; LastMediaDestination := Destination;
    Saved := GetIniString('tools', 'ffmpeg', '', AddBackslash(Destination) + 'runtime\media-selection.ini');
    Preferred := -1;
    if Saved <> '' then Preferred := AddMediaCandidate(ExtractFileDir(Saved));
    EnvFfmpeg := GetEnv('TAPETUBE_FFMPEG'); if EnvFfmpeg = '' then EnvFfmpeg := GetEnv('NTSCRT_FFMPEG');
    EnvFfprobe := GetEnv('TAPETUBE_FFPROBE'); if EnvFfprobe = '' then EnvFfprobe := GetEnv('NTSCRT_FFPROBE');
    if (EnvFfmpeg <> '') and (EnvFfprobe <> '') and
       (CompareText(ExtractFileDir(EnvFfmpeg), ExtractFileDir(EnvFfprobe)) = 0) then begin
      I := AddMediaCandidate(ExtractFileDir(EnvFfmpeg)); if I >= 0 then Preferred := I;
    end;
    if Destination <> '' then AddMediaCandidate(AddBackslash(Destination) + 'runtime\media');
    PathValue := GetEnv('PATH');
    while PathValue <> '' do begin
      Separator := Pos(';', PathValue);
      if Separator = 0 then begin Directory := PathValue; PathValue := ''; end else begin
        Directory := Copy(PathValue, 1, Separator - 1); Delete(PathValue, 1, Separator);
      end;
      AddMediaCandidate(Directory);
    end;
    if Previous <> '' then begin
      I := AddMediaCandidate(Previous); if I >= 0 then Preferred := I;
    end;
    if (Preferred < 0) and (MediaDirectories.Count > 0) then Preferred := 0;
    MediaList.ItemIndex := Preferred;
  finally
    MediaScanning := False;
  end;
end;

function SelectedMediaDirectory: String;
begin
  Result := '';
  if (MediaList.ItemIndex >= 0) and (MediaList.ItemIndex < MediaDirectories.Count) then
    Result := MediaDirectories[MediaList.ItemIndex];
end;

function HasMediaSelection: Boolean;
begin
  Result := InstallDownloadedMedia or (MediaExisting.Checked and (SelectedMediaDirectory <> ''));
end;

function SelectedMediaPath(Tool: String): String;
begin
  if InstallDownloadedMedia then Result := ExpandConstant('{app}\runtime\media\') + Tool + '.exe'
  else Result := AddBackslash(SelectedMediaDirectory) + Tool + '.exe';
end;

function ValidateMediaSelection: Boolean;
var Version: String;
begin
  Result := True;
  if MediaExisting.Checked then
    Result := (SelectedMediaDirectory <> '') and ProbeMediaPair(SelectedMediaDirectory, Version);
end;
