#ifndef SourceDir
  #error SourceDir is required
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename "EmberlineDefense-Setup"
#endif
#ifndef AppVersion
  #define AppVersion "0.12.5"
#endif
#ifndef BuildNumber
  #define BuildNumber "1"
#endif
#ifndef SetupIcon
  #error SetupIcon is required
#endif

[Setup]
AppId={{4FA8D424-E4F8-4283-87D6-A1CA52DAED3C}
AppName=Emberline Defense
AppVersion={#AppVersion}
AppVerName=Emberline Defense {#AppVersion} (Build {#BuildNumber})
AppPublisher=Emberline
AppCopyright=Copyright (C) 2026 Emberline
DefaultDirName={localappdata}\Programs\Emberline Defense
DefaultGroupName=Emberline Defense
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile={#SetupIcon}
UninstallDisplayIcon={app}\EmberlineDefense.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
CloseApplications=force
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousLanguage=no
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany=Emberline
VersionInfoDescription=Emberline Defense Windows Installer
VersionInfoProductName=Emberline Defense
VersionInfoProductVersion={#AppVersion}
#ifdef SignToolName
SignTool={#SignToolName}
SignedUninstaller=yes
#else
SignedUninstaller=no
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*DoNotShip*,*DontShipItWithYourGame*,*.pdb,*.mdb,*.dbg,*.map,*.log"

[Icons]
Name: "{group}\Emberline Defense"; Filename: "{app}\EmberlineDefense.exe"
Name: "{autodesktop}\Emberline Defense"; Filename: "{app}\EmberlineDefense.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\EmberlineDefense.exe"; Description: "{cm:LaunchProgram,Emberline Defense}"; Flags: nowait postinstall skipifsilent
