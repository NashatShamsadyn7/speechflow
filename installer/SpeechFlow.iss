; Inno Setup script. Build with:  iscc /DAppVersion=3.0.0 installer\SpeechFlow.iss
; Expects the published single-file app in publish\SpeechFlow.exe (see .github/workflows/release.yml).

#ifndef AppVersion
  #define AppVersion "3.0.0"
#endif

[Setup]
AppId={{6F2B8C1E-3D4A-4B7F-9E2C-5A1D8F3B7C40}
AppName=SpeechFlow
AppVersion={#AppVersion}
AppPublisher=Nashat Shamsadyn
AppPublisherURL=https://iosbb0.web.app
DefaultDirName={localappdata}\Programs\SpeechFlow
DisableProgramGroupPage=yes
; Per-user install: no admin prompt.
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=SpeechFlow-Setup-{#AppVersion}
SetupIconFile=..\src\SpeechFlow\Assets\icon.ico
UninstallDisplayIcon={app}\SpeechFlow.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "Start SpeechFlow when Windows starts"; GroupDescription: "Startup:"

[Files]
Source: "..\publish\SpeechFlow.exe"; DestDir: "{app}"; Flags: ignoreversion
; Whisper's native libraries must sit next to the exe (see SpeechFlow.csproj).
Source: "..\publish\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\SpeechFlow"; Filename: "{app}\SpeechFlow.exe"
Name: "{autodesktop}\SpeechFlow"; Filename: "{app}\SpeechFlow.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SpeechFlow"; \
  ValueData: """{app}\SpeechFlow.exe"" --startup"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\SpeechFlow.exe"; Description: "{cm:LaunchProgram,SpeechFlow}"; Flags: nowait postinstall skipifsilent
; The in-app updater runs this installer with /SILENT /RELAUNCH and expects the app to come back.
Filename: "{app}\SpeechFlow.exe"; Flags: nowait; Check: IsRelaunch

[Code]
function IsRelaunch: Boolean;
begin
  Result := Pos('/RELAUNCH', UpperCase(GetCmdTail)) > 0;
end;

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/im SpeechFlow.exe /f"; Flags: runhidden; RunOnceId: "StopSpeechFlow"
