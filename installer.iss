; Script generated for FileFormat AI Studio
#ifndef AppVersion
  #define AppVersion "0.10.0"
#endif

#define MyAppName "FileFormat AI Studio"
#define MyAppPublisher "Openize Pty Ltd"
#define MyAppURL "https://github.com/OpenizePtyLtd/fileformat-studio"
#define MyAppExeName "FileFormatAIStudio.exe"
#ifndef SourceDir
  #define SourceDir "FileFormatAIStudio\FileFormatAIStudio\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
#endif

[Setup]
; App Identity
AppId={{D37B4761-B9D7-4BFA-84BF-E81DCAE1D394}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases

; Installation Directory & Architecture
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Output
OutputDir=publish_installer
OutputBaseFilename=FileFormatAIStudio-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; Uninstaller configuration
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

