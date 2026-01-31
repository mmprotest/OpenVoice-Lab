[Setup]
AppName=OpenVoiceLab
AppVersion=0.1.0
DefaultDirName={localappdata}\Programs\OpenVoiceLab
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir={#SourcePath}\..\artifacts\installer
OutputBaseFilename=OpenVoiceLab-Setup

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; Flags: unchecked

[Files]
Source: "{#SourcePath}\..\artifacts\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourcePath}\..\artifacts\worker\OpenVoiceLab.Worker\*"; DestDir: "{app}\worker"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\OpenVoiceLab"; Filename: "{app}\OpenVoiceLab.App.exe"
Name: "{userdesktop}\OpenVoiceLab"; Filename: "{app}\OpenVoiceLab.App.exe"; Tasks: desktopicon


[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  response: Integer;
  dataDir: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    dataDir := ExpandConstant('{localappdata}\OpenVoiceLab');
    response := MsgBox(
      'Remove OpenVoiceLab user data (voices, models, logs) from ' + dataDir + '?',
      mbConfirmation,
      MB_YESNO
    );
    if response = IDYES then
    begin
      DelTree(dataDir, True, True, True);
    end;
  end;
end;
