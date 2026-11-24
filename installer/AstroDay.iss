#ifndef AppVersion
  #define AppVersion "1.5.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
#endif

#define AppName "AstroDay"
#define AppPublisher "DMA"
#define AppExeName "AstroDay.exe"
#define AppId "{{B4B7A6E0-4F35-4A50-9A35-DA0FD7E97D7B}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
OutputDir=..
OutputBaseFilename={#AppName}-Setup-v{#AppVersion}
UninstallDisplayIcon={app}\AstroDay.ico
SetupIconFile=..\assets\AstroDay.ico
WizardImageFile=..\assets\InstallerSidebar.bmp
WizardSmallImageFile=..\assets\InstallerSmall.bmp
WizardStyle=modern
CloseApplications=force
CloseApplicationsFilter=AstroDay.exe
DisableReadyPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\assets\AstroDay.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\AstroDay.ico"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\AstroDay.ico"; Check: CreateDesktopShortcut

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"" --background"; Flags: uninsdeletevalue; Check: StartAtSignIn
Root: HKCU; Subkey: "Software\AstroDay"; ValueType: string; ValueName: "LibraryDirectory"; ValueData: "{code:LibraryDirectory}"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Parameters: "--configure-only --library={code:LibraryDirectoryArgument}"; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExeName}"; Description: "Start AstroDay when the installer closes"; Flags: postinstall nowait skipifsilent

[Code]
var
  LibraryPage: TInputDirWizardPage;
  DesktopShortcutCheck: TNewCheckBox;
  StartupCheck: TNewCheckBox;
  PreviousVersion: String;

function CreateDesktopShortcut: Boolean;
begin
  Result := (DesktopShortcutCheck = nil) or DesktopShortcutCheck.Checked;
end;

function StartAtSignIn: Boolean;
begin
  Result := (StartupCheck = nil) or StartupCheck.Checked;
end;

function GetStoredLibraryDirectory: String;
var
  SettingsFile: String;
  Content, AfterKey, PathText: AnsiString;
  KeyPos, StartQuote, EndQuote: Integer;
begin
  Result := '';
  if RegQueryStringValue(HKCU, 'Software\AstroDay', 'LibraryDirectory', Result) and (Result <> '') then
    Exit;

  SettingsFile := ExpandConstant('{localappdata}\AstroDay\settings.json');
  if not FileExists(SettingsFile) or not LoadStringFromFile(SettingsFile, Content) then
    Exit;

  KeyPos := Pos('"LibraryDirectory"', Content);
  if KeyPos = 0 then
    Exit;
  AfterKey := Copy(Content, KeyPos + Length('"LibraryDirectory"'), Length(Content));
  StartQuote := Pos('"', AfterKey);
  if StartQuote = 0 then
    Exit;
  AfterKey := Copy(AfterKey, StartQuote + 1, Length(AfterKey));
  EndQuote := Pos('"', AfterKey);
  if EndQuote = 0 then
    Exit;
  PathText := Copy(AfterKey, 1, EndQuote - 1);
  if ((Length(PathText) > 1) and (PathText[2] = ':')) or
     ((Length(PathText) > 1) and (PathText[1] = '\') and (PathText[2] = '\')) then
    for KeyPos := 1 to Length(PathText) do
      if not ((PathText[KeyPos] = '\') and (KeyPos < Length(PathText)) and (PathText[KeyPos + 1] = '\')) then
        Result := Result + PathText[KeyPos];
end;

function LibraryDirectory(Param: String): String;
begin
  if LibraryPage <> nil then
    Result := LibraryPage.Values[0]
  else
    Result := GetStoredLibraryDirectory;
end;

function LibraryDirectoryArgument(Param: String): String;
begin
  Result := '"' + LibraryDirectory('') + '"';
end;

procedure InitializeWizard;
var
  DefaultLibrary: String;
begin
  LibraryPage := CreateInputDirPage(
    wpSelectDir,
    'AstroDay preferences',
    'Choose the image-library location and optional shortcuts.',
    'Your shared-folder library is preserved during upgrades and uninstalling.',
    False,
    'AstroDay');
  LibraryPage.Add('Image library folder:');
  DesktopShortcutCheck := TNewCheckBox.Create(LibraryPage);
  DesktopShortcutCheck.Parent := LibraryPage.Surface;
  DesktopShortcutCheck.Left := ScaleX(0);
  DesktopShortcutCheck.Top := ScaleY(92);
  DesktopShortcutCheck.Width := LibraryPage.SurfaceWidth;
  DesktopShortcutCheck.Caption := 'Create a desktop shortcut';
  DesktopShortcutCheck.Checked := False;
  StartupCheck := TNewCheckBox.Create(LibraryPage);
  StartupCheck.Parent := LibraryPage.Surface;
  StartupCheck.Left := ScaleX(0);
  StartupCheck.Top := ScaleY(116);
  StartupCheck.Width := LibraryPage.SurfaceWidth;
  StartupCheck.Caption := 'Start AstroDay when I sign in to Windows';
  StartupCheck.Checked := True;
  if not RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1', 'DisplayVersion', PreviousVersion) then
    PreviousVersion := '';
  DefaultLibrary := GetStoredLibraryDirectory;
  if DefaultLibrary = '' then
    DefaultLibrary := ExpandConstant('{localappdata}\AstroDay\Images');
  LibraryPage.Values[0] := DefaultLibrary;
end;

procedure StyleInstaller;
var
  Index: Integer;
  Control: TControl;
begin
  WizardForm.Color := $00F5F7F7;
  WizardForm.Font.Color := $00262219;
  for Index := 0 to WizardForm.ComponentCount - 1 do
  begin
    if WizardForm.Components[Index] is TControl then
    begin
      Control := TControl(WizardForm.Components[Index]);
      if Control is TPanel then
      begin
        TPanel(Control).Color := $00F5F7F7;
        TPanel(Control).Font.Color := $00262219;
      end
      else if Control is TLabel then
      begin
        TLabel(Control).Color := $00F5F7F7;
        TLabel(Control).Font.Color := $00262219;
      end
      else if Control is TNewStaticText then
      begin
        TNewStaticText(Control).Font.Color := $00262219;
      end
      else if Control is TCheckBox then
      begin
        TCheckBox(Control).Font.Color := $00262219;
      end
      else if Control is TEdit then
      begin
        TEdit(Control).Color := $00FFFFFF;
        TEdit(Control).Font.Color := $00262219;
      end;
    end;
  end;
  WizardForm.TasksList.Font.Color := $00262219;
  WizardForm.PageNameLabel.Font.Color := $00C7DB9A;
  WizardForm.PageDescriptionLabel.Font.Color := $00F2F5F1;
  WizardForm.NextButton.Font.Color := $00262219;
  WizardForm.BackButton.Font.Color := $00262219;
  WizardForm.CancelButton.Font.Color := $00262219;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  StyleInstaller;
  if CurPageID = LibraryPage.ID then
  begin
    WizardForm.NextButton.Caption := '&Install';
    WizardForm.PageNameLabel.Caption := 'AstroDay preferences';
    WizardForm.PageDescriptionLabel.Caption := 'Choose your library folder and click Install.';
  end
  else
    WizardForm.NextButton.Caption := '&Next >';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := PageID = wpSelectTasks;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  if PreviousVersion <> '' then
    Log('Updating AstroDay ' + PreviousVersion + ' to {#AppVersion}.');

  if Exec(ExpandConstant('{cmd}'), '/C taskkill /F /T /IM AstroDay.exe >nul 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    Log('AstroDay process close attempt completed with exit code ' + IntToStr(ExitCode) + '.')
  else
    Result := 'Setup could not stop AstroDay. Close the app and try again.';
end;
