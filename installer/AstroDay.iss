#ifndef AppVersion
  #define AppVersion "1.2.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
#endif

#define AppName "AstroDay"
#define AppPublisher "DMA"
#define AppExeName "AstroDay.exe"

[Setup]
AppId={{B4B7A6E0-4F35-4A50-9A35-DA0FD7E97D7B}
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
UninstallDisplayIcon={app}\{#AppExeName}
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start AstroDay when I sign in to Windows"; GroupDescription: "Startup:"
Name: "starttray"; Description: "Launch AstroDay minimized to the system tray after installation"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"" --background"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExeName}"; Parameters: "--background"; Description: "Launching AstroDay in the system tray"; Flags: nowait postinstall skipifsilent; Tasks: starttray
