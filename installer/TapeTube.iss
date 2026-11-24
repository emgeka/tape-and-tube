; SPDX-License-Identifier: GPL-3.0-only
#ifndef PackageDir
  #error PackageDir must refer to a validated package
#endif
#include FFmpegIncludeFile
#include DirectXIncludeFile
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
SetupIconFile=..\src\Ntscrt.Windows\TapeTube.ico
WizardSmallImageFile=..\src\Ntscrt.Windows\TapeTube.png
WizardImageFile=installer-welcome.png
DisableProgramGroupPage=yes
DisableWelcomePage=no
LicenseFile=..\LICENSE
InfoBeforeFile=installer-info.txt
OutputBaseFilename=TapeTube-{#ReleaseVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=115,150
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: english; MessagesFile: compiler:Default.isl
Name: german; MessagesFile: compiler:Languages\German.isl; InfoBeforeFile: installer-info-de.txt

[Files]
Source: "{#PackageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "components.local.json,media-tools.json,media-selection.ini"
Source: "{#ProbeFile}"; Flags: dontcopy
Source: "{#MediaProbeFile}"; Flags: dontcopy
Source: "{#SourceArchive}"; DestDir: "{app}\sources"; Flags: ignoreversion
Source: "{#CompilerLicense}"; DestDir: "{app}\licenses\installer"; DestName: "Inno-Setup-LICENSE.txt"; Flags: ignoreversion
Source: "installed-info.txt"; DestDir: "{app}"; DestName: "INSTALLATION.txt"; Flags: ignoreversion
Source: "media-selection.ini.txt"; DestDir: "{app}\runtime"; DestName: "media-selection.ini"; Flags: ignoreversion; Check: HasMediaSelection
Source: "{code:MediaFfmpeg}"; DestDir: "{app}\runtime\media"; Flags: external ignoreversion; Check: InstallDownloadedMedia
Source: "{code:MediaFfprobe}"; DestDir: "{app}\runtime\media"; Flags: external ignoreversion; Check: InstallDownloadedMedia
Source: "{tmp}\ffmpeg-download\{#FFmpegRoot}\LICENSE"; DestDir: "{app}\licenses\ffmpeg"; Flags: external ignoreversion; Check: InstallDownloadedMedia
Source: "{tmp}\ffmpeg-download\{#FFmpegRoot}\README.txt"; DestDir: "{app}\licenses\ffmpeg"; Flags: external ignoreversion; Check: InstallDownloadedMedia
Source: "{tmp}\ffmpeg-download\{#FFmpegRoot}\doc\*"; DestDir: "{app}\licenses\ffmpeg\doc"; Flags: external recursesubdirs createallsubdirs ignoreversion; Check: InstallDownloadedMedia
Source: "ffmpeg-download.json"; DestDir: "{app}\licenses\ffmpeg"; Flags: ignoreversion; Check: InstallDownloadedMedia

[Tasks]
Name: desktopicon; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[INI]
Filename: "{app}\runtime\media-selection.ini"; Section: "tools"; Key: "ffmpeg"; String: "{code:SelectedMediaPath|ffmpeg}"; Flags: uninsdeleteentry; Check: HasMediaSelection
Filename: "{app}\runtime\media-selection.ini"; Section: "tools"; Key: "ffprobe"; String: "{code:SelectedMediaPath|ffprobe}"; Flags: uninsdeleteentry; Check: HasMediaSelection

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
english.PrereqDescription=Required effect components and optional video tools, all in one place.
german.PrereqDescription=Pflichtkomponenten für Effekte und optionale Videowerkzeuge an einem Ort.
english.Present=Available
german.Present=Vorhanden
english.Missing=Missing
german.Missing=Fehlt
english.MediaStatus=FFmpeg / ffprobe
german.MediaStatus=FFmpeg / ffprobe
english.MediaFound=Available in this app or PATH
german.MediaFound=Vorhanden in dieser App oder PATH
english.MediaAbsent=Not available (optional for images)
german.MediaAbsent=Nicht vorhanden (für Bilder optional)
english.MediaPrepared=Verified and ready to copy
german.MediaPrepared=Geprüft und zum Kopieren bereit
english.MediaPlanned=Download selected
german.MediaPlanned=Download ausgewählt
english.MediaSelected=Available · Version
german.MediaSelected=Vorhanden · Version
english.MediaDownloadNow=Prepare FFmpeg
german.MediaDownloadNow=FFmpeg vorbereiten
english.ProbeFailed=Component check could not run. Installation cannot continue. See the setup log.
german.ProbeFailed=Die Komponentenprüfung konnte nicht ausgeführt werden. Die Installation wird angehalten. Siehe Installationsprotokoll.
english.PrereqHelp=C++ and DirectX are required for effects. C++ opens Microsoft's download; DirectX (about 96 MB) is downloaded, extracted and starts DXSETUP automatically. Confirm Microsoft's license/admin prompts yourself. Use Check again after installing C++. .NET is included.
german.PrereqHelp=C++ und DirectX sind für Effekte erforderlich. C++ öffnet Microsofts Download; DirectX (ca. 96 MB) wird geladen, entpackt und DXSETUP automatisch gestartet. Microsofts Lizenz-/Administratorabfragen selbst bestätigen. Nach C++ erneut prüfen. .NET ist enthalten.
english.DXInstall=Install DirectX
german.DXInstall=DirectX installieren
english.DXDownloading=Preparing DirectX
german.DXDownloading=DirectX vorbereiten
english.DXDownloadHelp=Downloading the original Microsoft package, verifying it and extracting to a temporary folder.
german.DXDownloadHelp=Originalpaket von Microsoft herunterladen, prüfen und in einen temporären Ordner entpacken.
english.DXExtractFailed=The Microsoft package could not be extracted or its files could not be verified.
german.DXExtractFailed=Das Microsoft-Paket konnte nicht entpackt oder seine Dateien konnten nicht geprüft werden.
english.DXFailed=DirectX could not be prepared. Check the connection and try Install DirectX again. No unverified files will be started.
german.DXFailed=DirectX konnte nicht vorbereitet werden. Verbindung prüfen und DirectX installieren erneut wählen. Ungeprüfte Dateien werden nicht gestartet.
english.DXStarting=DirectX setup is running. Complete the Microsoft dialog; the component check will then run again.
german.DXStarting=DirectX-Setup läuft. Den Microsoft-Dialog abschließen; danach wird die Komponentenprüfung erneut ausgeführt.
english.DXLaunchFailed=DXSETUP could not be started or the administrator prompt was canceled. You can retry Install DirectX.
german.DXLaunchFailed=DXSETUP konnte nicht gestartet werden oder die Administratorabfrage wurde abgebrochen. DirectX installieren kann erneut gewählt werden.
english.DXStillMissing=The required DirectX component is still missing. The Microsoft setup may have been canceled, failed or require a restart. Retry, or restart Windows and then run Tape & Tube setup again. Microsoft logs: Windows\DirectX.log and Windows\DXError.log.
german.DXStillMissing=Die benötigte DirectX-Komponente fehlt weiterhin. Microsofts Setup wurde möglicherweise abgebrochen, ist fehlgeschlagen oder benötigt einen Neustart. Erneut versuchen oder Windows neu starten und danach das Tape-&-Tube-Setup erneut öffnen. Microsoft-Protokolle: Windows\DirectX.log und Windows\DXError.log.
english.CheckAgain=Check again
german.CheckAgain=Erneut prüfen
english.PrereqMissing=Required Microsoft components are still missing. Use the download buttons, install the components and check again. For unattended setup, install prerequisites beforehand.
german.PrereqMissing=Benötigte Microsoft-Komponenten fehlen noch. Bitte über die Download-Schaltflächen installieren und erneut prüfen. Für eine unbeaufsichtigte Installation die Voraussetzungen vorher installieren.
english.MediaTitle=Optional video tools
german.MediaTitle=Optionale Videowerkzeuge
english.MediaDescription=Images work without FFmpeg. Video requires ffmpeg.exe and ffprobe.exe.
german.MediaDescription=Bilder funktionieren ohne FFmpeg. Videos benötigen ffmpeg.exe und ffprobe.exe.
english.MediaHelp=When download is selected, Next downloads and verifies FFmpeg {#FFmpegVersion} here (about 34 MiB, Gyan Doshi / gyan.dev, GPL v3). Setup copies the prepared tools into the app folder during installation. Original notices and source links are in licenses/ffmpeg.
german.MediaHelp=Bei gewähltem Download startet Weiter hier die Vorbereitung von FFmpeg {#FFmpegVersion} (ca. 34 MiB, Gyan Doshi / gyan.dev, GPL v3). Setup prüft den Download und kopiert die Dateien bei der Installation in den App-Ordner. Lizenz und Quellen stehen in licenses/ffmpeg.
english.MediaDownload=Download FFmpeg for Tape && Tube
german.MediaDownload=FFmpeg für Tape && Tube herunterladen
english.MediaExisting=Use an existing installation (version and folder below)
german.MediaExisting=Vorhandene Installation verwenden (Version und Ordner unten)
english.MediaBrowse=Add folder...
german.MediaBrowse=Ordner hinzufügen...
english.MediaCount=Available · %d installations
german.MediaCount=Vorhanden · %d Installationen
english.MediaSkip=Skip FFmpeg (images only unless tools are already available)
german.MediaSkip=FFmpeg überspringen (nur Bilder, sofern keine Werkzeuge vorhanden sind)
english.MediaDownloading=Downloading and verifying FFmpeg
german.MediaDownloading=FFmpeg herunterladen und prüfen
english.MediaDownloadHelp=Only the selected, checksum-verified upstream build will be installed.
german.MediaDownloadHelp=Installiert wird ausschließlich der ausgewählte Upstream-Build mit geprüfter Prüfsumme.
english.MediaFailed=FFmpeg could not be prepared. Check your connection and retry, or choose your own tools / skip FFmpeg on the component page. No unverified download will be installed.
german.MediaFailed=FFmpeg konnte nicht vorbereitet werden. Verbindung prüfen und erneut versuchen oder auf der Komponentenseite eigene Werkzeuge / Überspringen wählen. Ungeprüfte Downloads werden nicht installiert.
english.MediaIncomplete=The extracted FFmpeg package is incomplete.
german.MediaIncomplete=Das entpackte FFmpeg-Paket ist unvollständig.
english.MediaNotPrepared=Return to the component page to prepare FFmpeg, choose an existing installation or skip the video tools.
german.MediaNotPrepared=Zur Komponentenseite zurückkehren und FFmpeg vorbereiten, eine vorhandene Installation wählen oder die Videowerkzeuge überspringen.
english.MediaFolder=Folder containing ffmpeg.exe and ffprobe.exe (optional):
german.MediaFolder=Ordner mit ffmpeg.exe und ffprobe.exe (optional):
english.MediaInvalid=Select a working ffmpeg.exe/ffprobe.exe pair from the same folder and version. Add a folder if needed. Unresponsive or mismatched tools cannot be selected.
german.MediaInvalid=Ein funktionsfähiges Paar aus ffmpeg.exe und ffprobe.exe mit gleichem Ordner und gleicher Version auswählen. Bei Bedarf einen Ordner hinzufügen. Nicht reagierende oder unterschiedliche Werkzeuge können nicht ausgewählt werden.

[Code]
var
  PrereqPage: TWizardPage;
  StatusLabel: TNewStaticText;
  DXStatusLabel, MediaStatusLabel: TNewStaticText;
  MediaDownload, MediaExisting, MediaSkip: TNewRadioButton;
  MediaBrowse: TNewButton;
  DownloadPage: TDownloadWizardPage;
  MediaReady: Boolean;
  ProbeReady: Boolean;
  PrereqMask: Integer;
  DirectXBusy: Boolean;
  DirectXButton, VCButton, RecheckButton, MediaButton: TNewButton;
  MediaDirectories, MediaVersions: TStringList;
  MediaList: TNewComboBox;
  MediaProbeReady, MediaScanning: Boolean;
  LastMediaDestination: String;

#include "DirectX.iss"

function InstallDownloadedMedia: Boolean;
begin
  Result := MediaDownload.Checked and MediaReady;
end;

function DownloadedRoot: String;
begin
  Result := ExpandConstant('{tmp}\ffmpeg-download\{#FFmpegRoot}');
end;

function MediaFfmpeg(Param: String): String;
begin
  Result := DownloadedRoot + '\bin\ffmpeg.exe';
end;

function MediaFfprobe(Param: String): String;
begin
  Result := DownloadedRoot + '\bin\ffprobe.exe';
end;

function PrepareMedia: String;
begin
  Result := '';
  if not MediaDownload.Checked or MediaReady then Exit;
  DownloadPage.Clear;
  DownloadPage.Add('{#FFmpegUrl}', '{#FFmpegArchive}', '{#FFmpegSHA256}');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
      ExtractArchive(ExpandConstant('{tmp}\{#FFmpegArchive}'), ExpandConstant('{tmp}\ffmpeg-download'), '', True, nil);
      if not FileExists(DownloadedRoot + '\bin\ffmpeg.exe') or
         not FileExists(DownloadedRoot + '\bin\ffprobe.exe') or
         not FileExists(DownloadedRoot + '\LICENSE') or
         not FileExists(DownloadedRoot + '\README.txt') or
         not FileExists(DownloadedRoot + '\doc\ffmpeg.html') then
        RaiseException(CustomMessage('MediaIncomplete'));
      MediaReady := True;
      Log('FFmpeg {#FFmpegVersion}: pinned archive SHA-256 verified and extracted');
    except
      Log('FFmpeg preparation: ' + GetExceptionMessage);
      Result := CustomMessage('MediaFailed') + #13#10 + GetExceptionMessage;
    end;
  finally
    DownloadPage.Hide;
  end;
end;

#include "MediaSelection.iss"

procedure RefreshMediaStatus;
var Status: String;
begin
  if MediaDirectories.Count > 0 then Status := Format(CustomMessage('MediaCount'), [MediaDirectories.Count]) else Status := CustomMessage('MediaAbsent');
  if MediaExisting.Checked then begin
    if SelectedMediaDirectory <> '' then
      Status := CustomMessage('MediaSelected') + ': ' + MediaVersions[MediaList.ItemIndex]
    else Status := CustomMessage('MediaInvalid');
  end;
  if MediaDownload.Checked then begin
    if MediaReady then Status := CustomMessage('MediaPrepared') else
      Status := Status + ' (' + CustomMessage('MediaPlanned') + ')';
  end;
  MediaStatusLabel.Caption := CustomMessage('MediaStatus') + #13#10 + Status;
  MediaButton.Enabled := MediaDownload.Checked and not MediaReady and not DirectXBusy;
end;

procedure MediaChoiceClick(Sender: TObject);
begin
  if MediaScanning then Exit;
  MediaList.Enabled := MediaExisting.Checked;
  MediaBrowse.Enabled := True;
  MediaList.Hint := SelectedMediaDirectory;
  if MediaList.ItemIndex >= 0 then MediaList.Hint := MediaVersions[MediaList.ItemIndex] + #13#10 + MediaList.Hint;
  RefreshMediaStatus;
end;

function CheckPrerequisites: Boolean;
var Code: Integer; VC, DX: String;
begin
  Result := False;
  PrereqMask := -1;
  if WizardForm.CurPageID = PrereqPage.ID then WizardForm.NextButton.Enabled := False;
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
  StatusLabel.Caption := 'Visual C++ v14 x64: ' + VC;
  DXStatusLabel.Caption := 'DirectX D3DX9_43 x64: ' + DX;
  Log('Prerequisite probe mask: ' + IntToStr(Code));
  PrereqMask := Code;
  VCButton.Enabled := ((Code and 1) <> 0) and not DirectXBusy;
  DirectXButton.Enabled := ((Code and 2) <> 0) and not DirectXBusy;
  Result := Code = 0;
  if WizardForm.CurPageID = PrereqPage.ID then WizardForm.NextButton.Enabled := Result and not DirectXBusy;
end;

procedure RecheckClick(Sender: TObject);
begin
  CheckPrerequisites;
  DiscoverMedia;
  RefreshMediaStatus;
end;

procedure DownloadVCClick(Sender: TObject);
var ErrorCode: Integer;
begin
  ShellExecAsOriginalUser('open', 'https://aka.ms/vc14/vc_redist.x64.exe', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure DownloadDXClick(Sender: TObject);
var ErrorCode: Integer; Error: String; BackEnabled, CancelEnabled, SetupLaunched: Boolean;
begin
  if DirectXBusy or WizardSilent then Exit;
  CheckPrerequisites;
  if (PrereqMask < 0) or ((PrereqMask and 2) = 0) then Exit;
  DirectXBusy := True;
  SetupLaunched := False;
  BackEnabled := WizardForm.BackButton.Enabled;
  CancelEnabled := WizardForm.CancelButton.Enabled;
  DirectXButton.Enabled := False; VCButton.Enabled := False; RecheckButton.Enabled := False;
  PrereqPage.Surface.Enabled := False;
  WizardForm.NextButton.Enabled := False; WizardForm.BackButton.Enabled := False;
  try
    Error := PrepareDirectX;
    if Error <> '' then begin
      MsgBox(Error, mbInformation, MB_OK); Exit;
    end;
    StatusLabel.Caption := CustomMessage('DXStarting');
    WizardForm.CancelButton.Enabled := False;
    { Do not pass /silent: Microsoft's license and elevation remain user-controlled. }
    if not ShellExec('runas', DirectXDirectory + '\DXSETUP.exe', '', DirectXDirectory,
        SW_SHOWNORMAL, ewWaitUntilTerminated, ErrorCode) then begin
      Log('DirectX: DXSETUP launch error ' + IntToStr(ErrorCode));
      MsgBox(CustomMessage('DXLaunchFailed') + #13#10 + SysErrorMessage(ErrorCode), mbInformation, MB_OK);
    end else begin
      SetupLaunched := True;
      Log('DirectX: interactive DXSETUP closed; rechecking actual runtime');
    end;
  finally
    CheckPrerequisites;
    if SetupLaunched and (PrereqMask >= 0) and ((PrereqMask and 2) <> 0) then
      MsgBox(CustomMessage('DXStillMissing'), mbInformation, MB_OK);
    DirectXBusy := False;
    PrereqPage.Surface.Enabled := True;
    DirectXButton.Enabled := (PrereqMask >= 0) and ((PrereqMask and 2) <> 0);
    VCButton.Enabled := (PrereqMask >= 0) and ((PrereqMask and 1) <> 0);
    RecheckButton.Enabled := True;
    WizardForm.NextButton.Enabled := PrereqMask = 0; WizardForm.BackButton.Enabled := BackEnabled;
    WizardForm.CancelButton.Enabled := CancelEnabled;
    RefreshMediaStatus;
  end;
end;

function PrepareSelectedMedia: Boolean;
var Error: String; BackEnabled: Boolean;
begin
  Result := True;
  if not MediaDownload.Checked or MediaReady then Exit;
  Result := False;
  if DirectXBusy then Exit;
  DirectXBusy := True;
  BackEnabled := WizardForm.BackButton.Enabled;
  PrereqPage.Surface.Enabled := False;
  WizardForm.NextButton.Enabled := False; WizardForm.BackButton.Enabled := False;
  try
    Error := PrepareMedia;
    if Error <> '' then MsgBox(Error, mbInformation, MB_OK);
    Result := Error = '';
  finally
    DirectXBusy := False;
    PrereqPage.Surface.Enabled := True;
    WizardForm.BackButton.Enabled := BackEnabled;
    CheckPrerequisites;
    MediaChoiceClick(nil);
  end;
end;

procedure DownloadMediaClick(Sender: TObject);
begin
  PrepareSelectedMedia;
end;

procedure BrowseMediaClick(Sender: TObject);
var Directory: String; Index: Integer;
begin
  Directory := SelectedMediaDirectory;
  if BrowseForFolder(CustomMessage('MediaFolder'), Directory, False) then begin
    Index := AddMediaCandidate(Directory);
    if Index < 0 then MsgBox(CustomMessage('MediaInvalid'), mbInformation, MB_OK) else begin
      MediaExisting.Checked := True; MediaList.ItemIndex := Index;
    end;
  end;
  MediaChoiceClick(nil);
end;

procedure InitializeWizard;
var Help: TNewStaticText; ActionLeft, LabelWidth: Integer;
begin
  if ActiveLanguage = 'german' then
    WizardForm.LicenseMemo.Lines.Insert(0,
      'Eigener Anwendungscode und Host-Anbindung stehen unter der GNU GPL Version 3 (GPL-3.0-only).' + #13#10 +
      'Fremdkomponenten behalten ihre eigenen Lizenzen. Es folgt der englische Originaltext der GNU GPL Version 3.' + #13#10 + #13#10);
  PrereqPage := CreateCustomPage(wpSelectDir, CustomMessage('PrereqTitle'), CustomMessage('PrereqDescription'));
  ActionLeft := PrereqPage.SurfaceWidth - ScaleX(170);
  LabelWidth := ActionLeft - ScaleX(12);
  StatusLabel := TNewStaticText.Create(PrereqPage);
  StatusLabel.Parent := PrereqPage.Surface;
  StatusLabel.SetBounds(0, ScaleY(7), LabelWidth, ScaleY(24));
  DXStatusLabel := TNewStaticText.Create(PrereqPage);
  DXStatusLabel.Parent := PrereqPage.Surface;
  DXStatusLabel.SetBounds(0, ScaleY(43), LabelWidth, ScaleY(24));
  Help := TNewStaticText.Create(PrereqPage);
  Help.Parent := PrereqPage.Surface;
  Help.AutoSize := False; Help.WordWrap := True;
  Help.SetBounds(0, ScaleY(76), LabelWidth, ScaleY(92));
  Help.Caption := CustomMessage('PrereqHelp');
  VCButton := TNewButton.Create(PrereqPage); VCButton.Parent := PrereqPage.Surface;
  VCButton.SetBounds(ActionLeft, 0, ScaleX(170), ScaleY(28));
  VCButton.Caption := 'Visual C++ x64'; VCButton.OnClick := @DownloadVCClick;
  DirectXButton := TNewButton.Create(PrereqPage); DirectXButton.Parent := PrereqPage.Surface;
  DirectXButton.SetBounds(ActionLeft, ScaleY(36), ScaleX(170), ScaleY(28));
  DirectXButton.Caption := CustomMessage('DXInstall'); DirectXButton.OnClick := @DownloadDXClick;
  RecheckButton := TNewButton.Create(PrereqPage); RecheckButton.Parent := PrereqPage.Surface;
  RecheckButton.SetBounds(ActionLeft, ScaleY(72), ScaleX(170), ScaleY(26));
  RecheckButton.Caption := CustomMessage('CheckAgain'); RecheckButton.OnClick := @RecheckClick;
  DirectXDownloadPage := CreateDownloadPage(CustomMessage('DXDownloading'), CustomMessage('DXDownloadHelp'), nil);
  DirectXDownloadPage.ShowBaseNameInsteadOfUrl := True;
  MediaStatusLabel := TNewStaticText.Create(PrereqPage); MediaStatusLabel.Parent := PrereqPage.Surface;
  MediaStatusLabel.AutoSize := False; MediaStatusLabel.WordWrap := True;
  MediaStatusLabel.SetBounds(0, ScaleY(178), LabelWidth, ScaleY(48));
  MediaButton := TNewButton.Create(PrereqPage); MediaButton.Parent := PrereqPage.Surface;
  MediaButton.SetBounds(ActionLeft, ScaleY(178), ScaleX(170), ScaleY(26));
  MediaButton.Caption := CustomMessage('MediaDownloadNow'); MediaButton.OnClick := @DownloadMediaClick;
  MediaDownload := TNewRadioButton.Create(PrereqPage); MediaDownload.Parent := PrereqPage.Surface;
  MediaDownload.SetBounds(0, ScaleY(230), PrereqPage.SurfaceWidth, ScaleY(22));
  MediaDownload.Caption := CustomMessage('MediaDownload'); MediaDownload.OnClick := @MediaChoiceClick;
  MediaExisting := TNewRadioButton.Create(PrereqPage); MediaExisting.Parent := PrereqPage.Surface;
  MediaExisting.SetBounds(0, ScaleY(258), PrereqPage.SurfaceWidth, ScaleY(22));
  MediaExisting.Caption := CustomMessage('MediaExisting'); MediaExisting.OnClick := @MediaChoiceClick;
  MediaSkip := TNewRadioButton.Create(PrereqPage); MediaSkip.Parent := PrereqPage.Surface;
  MediaSkip.SetBounds(0, ScaleY(314), PrereqPage.SurfaceWidth, ScaleY(22));
  MediaSkip.Caption := CustomMessage('MediaSkip'); MediaSkip.OnClick := @MediaChoiceClick;
  MediaDirectories := TStringList.Create; MediaVersions := TStringList.Create;
  MediaList := TNewComboBox.Create(PrereqPage); MediaList.Parent := PrereqPage.Surface;
  MediaList.Style := csDropDownList; MediaList.ShowHint := True;
  MediaList.SetBounds(ScaleX(18), ScaleY(286), PrereqPage.SurfaceWidth - ScaleX(173), ScaleY(24));
  MediaList.OnChange := @MediaChoiceClick;
  MediaBrowse := TNewButton.Create(PrereqPage); MediaBrowse.Parent := PrereqPage.Surface;
  MediaBrowse.SetBounds(PrereqPage.SurfaceWidth - ScaleX(145), ScaleY(285), ScaleX(145), ScaleY(26));
  MediaBrowse.Caption := CustomMessage('MediaBrowse'); MediaBrowse.OnClick := @BrowseMediaClick;
  { Interactive defaults to download; unattended setup never downloads without /MEDIA=download. }
  DiscoverMedia;
  MediaDownload.Checked := not WizardSilent and (MediaDirectories.Count = 0);
  MediaExisting.Checked := not WizardSilent and (MediaDirectories.Count > 0);
  MediaSkip.Checked := WizardSilent;
  if ExpandConstant('{param:MEDIA|}') = 'download' then MediaDownload.Checked := True;
  if ExpandConstant('{param:MEDIA|}') = 'existing' then MediaExisting.Checked := True;
  if ExpandConstant('{param:MEDIA|}') = 'skip' then MediaSkip.Checked := True;
  if ExpandConstant('{param:MEDIAFOLDER|}') <> '' then
    MediaList.ItemIndex := AddMediaCandidate(ExpandConstant('{param:MEDIAFOLDER|}'));
  MediaChoiceClick(nil);
  DownloadPage := CreateDownloadPage(CustomMessage('MediaDownloading'), CustomMessage('MediaDownloadHelp'), nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
  Help := TNewStaticText.Create(PrereqPage); Help.Parent := PrereqPage.Surface;
  Help.AutoSize := False; Help.WordWrap := True;
  Help.SetBounds(0, ScaleY(350), PrereqPage.SurfaceWidth, PrereqPage.SurfaceHeight - ScaleY(350));
  Help.Caption := CustomMessage('MediaDescription') + #13#10 + CustomMessage('MediaHelp');
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = PrereqPage.ID then begin
    CheckPrerequisites;
    if CompareText(LastMediaDestination, WizardDirValue) <> 0 then DiscoverMedia;
    RefreshMediaStatus;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID <> PrereqPage.ID) or WizardSilent then Exit;
  if not CheckPrerequisites then begin
    MsgBox(CustomMessage('PrereqMissing'), mbInformation, MB_OK); Result := False; Exit;
  end;
  if not ValidateMediaSelection then begin
    MsgBox(CustomMessage('MediaInvalid'), mbInformation, MB_OK); Result := False; Exit;
  end;
  Result := PrepareSelectedMedia;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not CheckPrerequisites then begin Result := CustomMessage('PrereqMissing'); Exit; end;
  { Repeat validation for both modes. Only unattended setup prepares media here. }
  if not ValidateMediaSelection then begin
      Result := CustomMessage('MediaInvalid'); Exit;
  end;
  if WizardSilent then Result := PrepareMedia
  else if MediaDownload.Checked and not MediaReady then Result := CustomMessage('MediaNotPrepared');
end;
