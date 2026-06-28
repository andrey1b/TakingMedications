#define MyAppName "TakingMedications"
#define MyAppDisplayName "Приём лекарств (Medication Tracker)"
#define MyAppVersion "1.3.6"
#define MyAppPublisher "andrey1b"
#define MyAppURL "https://github.com/andrey1b/TakingMedications"
#define MyAppExeName "TakingMedications.exe"
#define MyAppSourceDir "TakingMedications\bin\Release\net9.0-windows\win-x64\publish"

[Setup]
AppId={{A3B5C7D9-E1F2-4A6B-8C0D-1E2F3A4B5C6D}
AppName={#MyAppDisplayName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppDisplayName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppDisplayName}
DisableProgramGroupPage=yes
SetupIconFile=TakingMedications\Medicines.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=Distributions
OutputBaseFilename=TakingMedications_Setup_v{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#MyAppSourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSourceDir}\medications_default.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "TakingMedications\Medicines.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppDisplayName}";   Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Medicines.ico"
Name: "{autodesktop}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Medicines.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; \
  Description: "{cm:LaunchProgram,{#StringChange(MyAppDisplayName, '&', '&&')}}"; \
  Flags: nowait postinstall skipifsilent
