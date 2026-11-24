#ifndef AppVersion
  #define AppVersion "1.4.3"
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
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=AstroDay.exe
AppMutex=AstroDay.SingleInstance

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start AstroDay when I sign in to Windows"; GroupDescription: "Startup:"
Name: "launchapp"; Description: "Start AstroDay after installation"; GroupDescription: "After installation:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\assets\AstroDay.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\AstroDay.ico"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\AstroDay.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"" --background"; Flags: uninsdeletevalue; Tasks: startup
Root: HKCU; Subkey: "Software\AstroDay"; ValueType: string; ValueName: "LibraryDirectory"; ValueData: "{code:LibraryDirectory}"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Parameters: "--configure-only --library={code:LibraryDirectoryArgument}"; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExeName}"; Description: "Starting AstroDay"; Flags: nowait postinstall skipifsilent; Tasks: launchapp

[Code]
var
  LibraryPage: TInputDirWizardPage;
  PreviousVersion: String;

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
    'Choose your AstroDay library',
    'Where should downloaded images and the library.json catalogue be stored?',
    'AstroDay preserves this folder during upgrades and uninstalling. Existing images are copied, never deleted, when changing folders.',
    False,
    'AstroDay');
  LibraryPage.Add('Image library folder:');
  if not RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1', 'DisplayVersion', PreviousVersion) then
    PreviousVersion := '';
  DefaultLibrary := GetStoredLibraryDirectory;
  if DefaultLibrary = '' then
    DefaultLibrary := ExpandConstant('{localappdata}\AstroDay\Images');
  LibraryPage.Values[0] := DefaultLibrary;
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

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  InstallationType: String;
begin
  if PreviousVersion <> '' then
    InstallationType := 'Update existing AstroDay ' + PreviousVersion + ' to {#AppVersion}.'
  else
    InstallationType := 'Install AstroDay {#AppVersion}.';
  Result := InstallationType + NewLine + NewLine;
  Result := Result + MemoDirInfo + NewLine + NewLine;
  Result := Result + 'AstroDay library:' + NewLine;
  Result := Result + Space + LibraryPage.Values[0] + NewLine;
  Result := Result + Space + 'Existing library content will be preserved and never removed by setup.' + NewLine + NewLine;
  Result := Result + MemoGroupInfo + NewLine + MemoTasksInfo;
end;
