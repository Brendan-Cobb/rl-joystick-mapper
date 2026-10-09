#define AppVersion "0.1.0"
[Setup]
AppName=RL Remapper
AppVersion={#AppVersion}
DefaultDirName={autopf}\RL Remapper
DefaultGroupName=RL Remapper
OutputBaseFilename=RLRemapper-Setup
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\RlRemapper.exe

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
Source: "Install-Drivers.ps1"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\RL Remapper"; Filename: "{app}\RlRemapper.exe"

[Run]
Filename: "{app}\RlRemapper.exe"; Description: "Launch RL Remapper"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
begin
  if CurStep = ssPostInstall then begin
    if (not FileExists(ExpandConstant('{app}\SDL2.dll'))) or
       (not FileExists(ExpandConstant('{app}\ViGEmClient.dll'))) then
      RaiseException('Native SDL2.dll and ViGEmClient.dll are missing from the package.');
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\Install-Drivers.ps1') + '" -InstallDir "' + ExpandConstant('{app}') + '"',
      '', SW_SHOW, ewWaitUntilTerminated, Code) then
      RaiseException('Could not launch driver dependency installer.');
    if Code <> 0 then
      RaiseException('Driver installation failed. Check network, signing and installer prompts; rerun setup after resolving the issue.');
  end;
end;
