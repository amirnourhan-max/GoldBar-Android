#define MyAppName "Gold Bar"
#define MyAppVersion "1.4.1"
#define MyAppPublisher "Amirnourhan"
#define MyAppExeName "GoldBar.exe"
#define PublishDir "..\..\build\windows-installed"

[Setup]
AppId={{2A189B8A-6A0E-4ACB-A6A1-73C8FBC19031}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\GoldBar
DefaultGroupName=Gold Bar
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\..\build\installer
OutputBaseFilename=GoldBar-Setup-v1.4.1
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Gold Bar"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Gold Bar"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Gold Bar"; Flags: nowait postinstall skipifsilent
