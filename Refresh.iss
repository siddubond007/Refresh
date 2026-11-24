#define MyAppName "Refresh"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Refresh QA Tools"
#define MyAppExeName "Refresh.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-4789-ABCD-1234567890AB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Refresh
DefaultGroupName=Refresh
OutputDir=publish
OutputBaseFilename=Refresh_Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "publish-app\Refresh.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Refresh"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Refresh"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Refresh"; Flags: postinstall nowait skipifsilent
