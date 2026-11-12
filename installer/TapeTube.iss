; SPDX-License-Identifier: GPL-3.0-only
#ifndef PackageDir
  #error PackageDir must refer to a validated package
#endif
#ifndef ReleaseVersion
  #define ReleaseVersion "0.1.0-beta.1"
#endif

[Setup]
AppId={{23CF420C-2372-49AC-AB78-2452D82F14D0}
AppName=Tape & Tube
AppVersion={#ReleaseVersion}
AppVerName=Tape & Tube {#ReleaseVersion}
AppPublisher=Tape & Tube contributors
AppComments=Windows adaptation of NTSCRT by Finn McKenty
DefaultDirName={localappdata}\Programs\TapeTube
DefaultGroupName=Tape & Tube
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayIcon={app}\TapeTube.exe
DisableProgramGroupPage=yes
DisableWelcomePage=no
LicenseFile=..\LICENSE
InfoBeforeFile=installer-info.txt
OutputBaseFilename=TapeTube-{#ReleaseVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: english; MessagesFile: compiler:Default.isl
Name: german; MessagesFile: compiler:Languages\German.isl

[Files]
Source: "{#PackageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "components.local.json,media-tools.json"
Source: "{#ProbeFile}"; Flags: dontcopy
Source: "{#SourceArchive}"; DestDir: "{app}\sources"; Flags: ignoreversion
Source: "{#CompilerLicense}"; DestDir: "{app}\licenses\installer"; DestName: "Inno-Setup-LICENSE.txt"; Flags: ignoreversion
Source: "installed-info.txt"; DestDir: "{app}"; DestName: "INSTALLATION.txt"; Flags: ignoreversion
Source: "{code:MediaFfmpeg}"; DestDir: "{app}\runtime\media"; Flags: external skipifsourcedoesntexist ignoreversion
Source: "{code:MediaFfprobe}"; DestDir: "{app}\runtime\media"; Flags: external skipifsourcedoesntexist ignoreversion

[Tasks]
Name: desktopicon; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Icons]
Name: "{group}\Tape & Tube"; Filename: "{app}\TapeTube.exe"; WorkingDir: "{app}"
Name: "{group}\{cm:Notices}"; Filename: "{app}\licenses\README.md"
Name: "{autodesktop}\Tape & Tube"; Filename: "{app}\TapeTube.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\TapeTube.exe"; Description: "{cm:LaunchProgram,Tape && Tube}"; Flags: nowait postinstall skipifsilent unchecked

[CustomMessages]
english.Notices=Licenses and sources
german.Notices=Lizenzen und Quellen
english.PrereqTitle=Check required components
german.PrereqTitle=Benötigte Komponenten prüfen
english.PrereqDescription=Microsoft runtime components are required for the effects.
german.PrereqDescription=Für die Effekte werden zusätzliche Microsoft-Laufzeitdateien benötigt.
english.Present=Available
german.Present=Vorhanden
english.Missing=Missing
german.Missing=Fehlt
english.ProbeFailed=Component check could not run. Installation cannot continue. See the setup log.
german.ProbeFailed=Die Komponentenprüfung konnte nicht ausgeführt werden. Die Installation wird angehalten. Siehe Installationsprotokoll.
english.PrereqHelp=Use the buttons to open the official Microsoft downloads. Install missing components, then click Check again. DirectX: extract the download and run DXSETUP.exe. These installers may request administrator rights. Tape & Tube itself includes .NET.
german.PrereqHelp=Die Schaltflächen öffnen die offiziellen Microsoft-Downloads. Fehlende Komponenten installieren, danach Erneut prüfen wählen. DirectX: Download entpacken und DXSETUP.exe starten. Diese Installer können Administratorrechte anfordern. .NET ist in Tape & Tube enthalten.
english.CheckAgain=Check again
german.CheckAgain=Erneut prüfen
english.PrereqMissing=Required Microsoft components are still missing. Use the download buttons, install the components and check again. For unattended setup, install prerequisites beforehand.
german.PrereqMissing=Benötigte Microsoft-Komponenten fehlen noch. Bitte über die Download-Schaltflächen installieren und erneut prüfen. Für eine unbeaufsichtigte Installation die Voraussetzungen vorher installieren.
english.MediaTitle=Optional video tools
german.MediaTitle=Optionale Videowerkzeuge
english.MediaDescription=Images work without FFmpeg. Video requires ffmpeg.exe and ffprobe.exe.
german.MediaDescription=Bilder funktionieren ohne FFmpeg. Videos benötigen ffmpeg.exe und ffprobe.exe.
english.MediaHelp=Optional: choose a folder containing both tools to copy them into this installation. Leave the field empty to use an existing PATH installation or add them later in runtime\media. FFmpeg is not downloaded or bundled by this installer. Your selected files retain their own license.
german.MediaHelp=Optional: Einen Ordner mit beiden Werkzeugen auswählen, um sie in diese Installation zu kopieren. Leer lassen, um eine bestehende PATH-Installation zu verwenden oder die Dateien später unter runtime\media abzulegen. FFmpeg wird von diesem Installer nicht heruntergeladen oder mitgeliefert. Die ausgewählten Dateien behalten ihre eigene Lizenz.
english.MediaFolder=Folder containing ffmpeg.exe and ffprobe.exe (optional):
german.MediaFolder=Ordner mit ffmpeg.exe und ffprobe.exe (optional):
english.MediaInvalid=The chosen folder must contain both ffmpeg.exe and ffprobe.exe, and must be different from the destination runtime\media folder.
german.MediaInvalid=Der ausgewählte Ordner muss ffmpeg.exe und ffprobe.exe enthalten und sich vom Zielordner runtime\media unterscheiden.

[Code]
var
  PrereqPage: TWizardPage;
  StatusLabel: TNewStaticText;
  MediaPage: TWizardPage;
  MediaEdit: TNewEdit;
  ProbeReady: Boolean;

function MediaFfmpeg(Param: String): String;
begin
  if MediaEdit.Text = '' then Result := ExpandConstant('{tmp}\no-selected-ffmpeg.exe')
  else Result := AddBackslash(MediaEdit.Text) + 'ffmpeg.exe';
end;

function MediaFfprobe(Param: String): String;
begin
  if MediaEdit.Text = '' then Result := ExpandConstant('{tmp}\no-selected-ffprobe.exe')
  else Result := AddBackslash(MediaEdit.Text) + 'ffprobe.exe';
end;

function CheckPrerequisites: Boolean;
var Code: Integer; VC, DX: String;
begin
  Result := False;
  if not ProbeReady then begin
    ExtractTemporaryFile('prerequisite-probe.exe');
    ProbeReady := True;
  end;
  if not Exec(ExpandConstant('{tmp}\prerequisite-probe.exe'), '', '', SW_HIDE, ewWaitUntilTerminated, Code) then begin
    StatusLabel.Caption := CustomMessage('ProbeFailed');
    Exit;
  end;
  if (Code < 0) or (Code > 3) then begin
    StatusLabel.Caption := CustomMessage('ProbeFailed');
    Exit;
  end;
  if (Code and 1) = 0 then VC := CustomMessage('Present') else VC := CustomMessage('Missing');
  if (Code and 2) = 0 then DX := CustomMessage('Present') else DX := CustomMessage('Missing');
  StatusLabel.Caption := 'Visual C++ v14 x64: ' + VC + #13#10 + 'DirectX D3DX9_43 x64: ' + DX;
  Log('Prerequisite probe mask: ' + IntToStr(Code));
  Result := Code = 0;
end;

procedure RecheckClick(Sender: TObject);
begin
  CheckPrerequisites;
end;

procedure DownloadVCClick(Sender: TObject);
var ErrorCode: Integer;
begin
  ShellExecAsOriginalUser('open', 'https://aka.ms/vc14/vc_redist.x64.exe', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure DownloadDXClick(Sender: TObject);
var ErrorCode: Integer;
begin
  ShellExecAsOriginalUser('open', 'https://www.microsoft.com/download/details.aspx?id=8109', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure BrowseMediaClick(Sender: TObject);
var Directory: String;
begin
  Directory := MediaEdit.Text;
  if BrowseForFolder(CustomMessage('MediaFolder'), Directory, False) then MediaEdit.Text := Directory;
end;

procedure InitializeWizard;
var Help: TNewStaticText; Button: TNewButton;
begin
  PrereqPage := CreateCustomPage(wpSelectDir, CustomMessage('PrereqTitle'), CustomMessage('PrereqDescription'));
  StatusLabel := TNewStaticText.Create(PrereqPage);
  StatusLabel.Parent := PrereqPage.Surface;
  StatusLabel.SetBounds(0, 0, PrereqPage.SurfaceWidth, ScaleY(42));
  Help := TNewStaticText.Create(PrereqPage);
  Help.Parent := PrereqPage.Surface;
  Help.AutoSize := False; Help.WordWrap := True;
  Help.SetBounds(0, ScaleY(55), PrereqPage.SurfaceWidth, ScaleY(104));
  Help.Caption := CustomMessage('PrereqHelp');
  Button := TNewButton.Create(PrereqPage); Button.Parent := PrereqPage.Surface;
  Button.SetBounds(0, ScaleY(170), ScaleX(155), ScaleY(28));
  Button.Caption := 'Visual C++ x64'; Button.OnClick := @DownloadVCClick;
  Button := TNewButton.Create(PrereqPage); Button.Parent := PrereqPage.Surface;
  Button.SetBounds(ScaleX(168), ScaleY(170), ScaleX(155), ScaleY(28));
  Button.Caption := 'DirectX'; Button.OnClick := @DownloadDXClick;
  Button := TNewButton.Create(PrereqPage); Button.Parent := PrereqPage.Surface;
  Button.SetBounds(0, ScaleY(212), ScaleX(155), ScaleY(28));
  Button.Caption := CustomMessage('CheckAgain'); Button.OnClick := @RecheckClick;
  MediaPage := CreateCustomPage(PrereqPage.ID, CustomMessage('MediaTitle'), CustomMessage('MediaDescription'));
  Help := TNewStaticText.Create(MediaPage); Help.Parent := MediaPage.Surface;
  Help.AutoSize := False; Help.WordWrap := True;
  Help.SetBounds(0, 0, MediaPage.SurfaceWidth, ScaleY(112)); Help.Caption := CustomMessage('MediaHelp');
  Help := TNewStaticText.Create(MediaPage); Help.Parent := MediaPage.Surface;
  Help.SetBounds(0, ScaleY(125), MediaPage.SurfaceWidth, ScaleY(35)); Help.Caption := CustomMessage('MediaFolder');
  MediaEdit := TNewEdit.Create(MediaPage); MediaEdit.Parent := MediaPage.Surface;
  MediaEdit.SetBounds(0, ScaleY(165), MediaPage.SurfaceWidth - ScaleX(105), ScaleY(24));
  Button := TNewButton.Create(MediaPage); Button.Parent := MediaPage.Surface;
  Button.SetBounds(MediaPage.SurfaceWidth - ScaleX(95), ScaleY(164), ScaleX(95), ScaleY(26));
  Button.Caption := SetupMessage(msgButtonBrowse); Button.OnClick := @BrowseMediaClick;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = PrereqPage.ID then CheckPrerequisites;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var Directory: String;
begin
  Result := True;
  if (CurPageID = PrereqPage.ID) and not CheckPrerequisites then begin
    MsgBox(CustomMessage('PrereqMissing'), mbInformation, MB_OK); Result := False;
  end;
  if (CurPageID = MediaPage.ID) and (MediaEdit.Text <> '') then begin
    Directory := AddBackslash(MediaEdit.Text);
    if not FileExists(Directory + 'ffmpeg.exe') or not FileExists(Directory + 'ffprobe.exe') or
       (CompareText(RemoveBackslashUnlessRoot(Directory), ExpandConstant('{app}\runtime\media')) = 0) then begin
      MsgBox(CustomMessage('MediaInvalid'), mbInformation, MB_OK); Result := False;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not CheckPrerequisites then Result := CustomMessage('PrereqMissing');
end;
