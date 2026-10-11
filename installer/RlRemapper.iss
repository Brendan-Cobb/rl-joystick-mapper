#define AppVersion "0.1.8"
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
Source: "driver-packages.json"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "Drivers\*.exe"; DestDir: "{tmp}\Drivers"; Flags: deleteafterinstall

[Icons]
Name: "{group}\RL Remapper"; Filename: "{app}\RlRemapper.exe"

[Run]
Filename: "{app}\RlRemapper.exe"; Description: "Launch RL Remapper"; Flags: nowait postinstall skipifsilent; Check: DriversReady

[Code]
var
  DriverSetupSucceeded: Boolean;
  DriverRestartRequired: Boolean;

function DriversReady: Boolean;
begin
  Result := DriverSetupSucceeded and not DriverRestartRequired;
end;

function NeedRestart: Boolean;
begin
  Result := DriverRestartRequired;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
  LogDir, LogPath: String;
begin
  if CurStep = ssPostInstall then begin
    DriverSetupSucceeded := False;
    if (not FileExists(ExpandConstant('{app}\SDL2.dll'))) or
       (not FileExists(ExpandConstant('{app}\ViGEmClient.dll'))) then
      RaiseException('Native SDL2.dll and ViGEmClient.dll are missing from the package.');
    LogDir := ExpandConstant('{commonappdata}\RL Remapper');
    if not ForceDirectories(LogDir) then
      RaiseException('Could not create driver setup log directory: ' + LogDir);
    LogPath := LogDir + '\setup-drivers.log';
#if VER >= EncodeVer(7, 0, 0)
    if not ExecWithNativeSysDir(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
#else
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
#endif
      '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\Install-Drivers.ps1') + '" -InstallDir "' + ExpandConstant('{app}') + '" -LogPath "' + LogPath + '"',
      '', SW_SHOW, ewWaitUntilTerminated, Code) then
      RaiseException('Could not launch driver dependency installer.');
    DriverRestartRequired := (Code = 3010) or (Code = 1641);
    if (Code <> 0) and not DriverRestartRequired then
      RaiseException('Driver setup failed (exit code ' + IntToStr(Code) + '). See ' + LogPath +
        ' for the underlying error. Resolve it and rerun setup; the app files alone do not mean the drivers are ready.');
    DriverSetupSucceeded := True;
  end;
end;
