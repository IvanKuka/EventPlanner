; Per-user installer without administrator rights. User data lives in %LOCALAPPDATA%\EventPlanner
; (separate from the program folder) and is kept on uninstall and upgrade.
; Built by build/package-windows.ps1 with /DAppVersion, /DSourceDir and /DOutputDir.

#ifndef AppVersion
  #error AppVersion must be defined
#endif

[Setup]
; Stable application id: never change it, upgrades are matched by this value.
AppId={{6B0C2E5A-3F7D-4C1B-9E2A-8D4F1A7C3B91}
AppName=EventPlanner
AppVerName=EventPlanner {#AppVersion}
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
AppPublisher=EventPlanner
DefaultDirName={localappdata}\Programs\EventPlanner
DefaultGroupName=EventPlanner
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=EventPlanner-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Ask before closing a running instance during upgrade; never close it silently.
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\EventPlanner.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\EventPlanner"; Filename: "{app}\EventPlanner.exe"
Name: "{autodesktop}\EventPlanner"; Filename: "{app}\EventPlanner.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\EventPlanner.exe"; Description: "{cm:LaunchProgram,EventPlanner}"; Flags: nowait postinstall skipifsilent
