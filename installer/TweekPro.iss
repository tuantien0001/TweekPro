; Inno Setup 6.3+ script for Tweek Pro. Build with publish.ps1 (or: ISCC.exe installer\TweekPro.iss).
; Installs the Release output for all users under Program Files, creates Start Menu / optional Desktop shortcuts,
; checks for .NET Framework 4.8 and leaves user data (%LOCALAPPDATA%\TweekPro, including the recovery vault) untouched on uninstall.

#ifndef AppVersion
  #define AppVersion "0.7.1"
#endif
#ifndef SourceDir
  #define SourceDir "..\bin\Release\net48"
#endif
#define AppName "Tweek Pro"
#define AppPublisher "Tweek Pro"
#define AppURL "https://github.com/tuantien0001/TweekPro"
#define AppExeName "TweekPro-" + AppVersion + ".exe"

[Setup]
AppId={{7E4C2A9B-5B1D-4C1E-9F2A-3D8B6C1F0A47}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=
OutputDir=..\dist
OutputBaseFilename=TweekPro-{#AppVersion}-Setup
SetupIconFile=..\TweekPro.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=6.1sp1
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=auto

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "test-results.txt,TweekPro-preview.png,preview-error.txt,scan-smoke.txt,mono_crash*,*.pdb,*.xml"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; postinstall entries run de-elevated by default; the exe manifest requires administrator, so launch it with Setup's elevated token.
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[Code]
// .NET Framework 4.8 ships with Windows 10 1903+ and Windows 11; older systems get a clear message and the download page.
function DotNet48Installed(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not DotNet48Installed() then
  begin
    if MsgBox('Tweek Pro requires .NET Framework 4.8, which is not installed on this computer.' + #13#10 + #13#10 +
              'Open the Microsoft download page now? Setup will exit; run it again after installing .NET Framework 4.8.',
              mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    Result := False;
  end;
end;
