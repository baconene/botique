; Boutique POS installer (Inno Setup 6). Build with ..\build-installer.ps1
; Bundles: .NET Framework 4.8 (only installed if missing), VC++ 2015-2022 runtime,
; MySQL Connector/ODBC, and a private MySQL 8.4 server for "On this computer" mode.

#define AppName      "Boutique POS"
#define AppVersion   "1.5.0"
#define AppPublisher "Boutique POS"
#define AppExe       "BoutiquePOS.exe"
#ifndef Redist
  ; build-installer.ps1 passes /DRedist=<folder with the downloaded prerequisites>
  #define Redist     "redist"
#endif

[Setup]
AppId={{8F3C2B1A-6D4E-4B7A-9C21-5E0F7A1B2C3D}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\Boutique POS
DefaultGroupName=Boutique POS
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=BoutiquePOS-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
MinVersion=10.0
WizardStyle=modern
SetupIconFile=..\src\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
CloseApplications=yes
; Add your licence agreement here before selling:
; LicenseFile=EULA.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Dirs]
; Shared data folder: every Windows user on the PC can read/write the shop's data.
Name: "{commonappdata}\BoutiquePOS"; Permissions: users-modify
Name: "{commonappdata}\BoutiquePOS\Backups"; Permissions: users-modify

[Files]
Source: "..\bin\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "setup-local-mysql.ps1"; DestDir: "{app}\setup"; Flags: ignoreversion
Source: "remove-local-mysql.ps1"; DestDir: "{app}\setup"; Flags: ignoreversion
Source: "{#Redist}\ndp48-x86-x64-allos-enu.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsDotNet
Source: "{#Redist}\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsVCRedist
Source: "{#Redist}\mysql-connector-odbc.msi"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsOdbc
Source: "{#Redist}\mysql\*"; DestDir: "{app}\mysql"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: WantsLocalDb

[Icons]
Name: "{autoprograms}\Boutique POS"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\Boutique POS"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\ndp48-x86-x64-allos-enu.exe"; Parameters: "/q /norestart"; StatusMsg: "Installing Microsoft .NET Framework 4.8 (this can take several minutes)..."; Check: NeedsDotNet; Flags: waituntilterminated
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Microsoft Visual C++ runtime..."; Check: NeedsVCRedist; Flags: waituntilterminated
Filename: "msiexec.exe"; Parameters: "/i ""{tmp}\mysql-connector-odbc.msi"" /qn /norestart"; StatusMsg: "Installing MySQL ODBC driver..."; Check: NeedsOdbc; Flags: waituntilterminated
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\setup\setup-local-mysql.ps1"" -AppDir ""{app}"" -DataDir ""{commonappdata}\BoutiquePOS"""; StatusMsg: "Setting up the local database (first time can take a minute)..."; Check: WantsLocalDb; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExe}"; Description: "Start Boutique POS now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\setup\remove-local-mysql.ps1"" -AppDir ""{app}"""; Flags: runhidden waituntilterminated; RunOnceId: "RemoveLocalMySQL"

[Code]
var
  DataPage: TInputOptionWizardPage;

procedure InitializeWizard;
begin
  DataPage := CreateInputOptionPage(wpSelectTasks,
    'Where should your data be stored?',
    'Choose where Boutique POS keeps inventory and sales.',
    'You can change this later in the app under Settings & Scanner > Change database connection.',
    True, False);
  DataPage.Add('On this computer - install a private MySQL database (recommended for a single shop)');
  DataPage.Add('Cloud server - e.g. our Forge server (enter the server details when the app first starts)');
  DataPage.Add('Another MySQL server I already have (enter details when the app first starts)');
  DataPage.Add('No database server - save to a file on this computer (offline, single register)');
  DataPage.SelectedValueIndex := 0;
end;

function WantsLocalDb: Boolean;
begin
  Result := DataPage.SelectedValueIndex = 0;
end;

function ChoiceName: String;
begin
  case DataPage.SelectedValueIndex of
    0: Result := 'local';
    1: Result := 'cloud';
    2: Result := 'server';
  else
    Result := 'offline';
  end;
end;

function NeedsDotNet: Boolean;
var
  Release: Cardinal;
begin
  // 528040 = .NET Framework 4.8
  Result := not (RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040));
end;

function NeedsVCRedist: Boolean;
var
  Installed, Minor: Cardinal;
begin
  // MySQL 8.4 and Connector/ODBC need the VC++ 2019+ runtime (14.2x or newer).
  Result := not (RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) and (Installed = 1)
             and RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Minor', Minor) and (Minor >= 29));
end;

function HasMySqlOdbc: Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(HKLM64, 'SOFTWARE\ODBC\ODBCINST.INI\ODBC Drivers', Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if (Pos('MySQL ODBC', Names[I]) = 1) and (Pos('Unicode', Names[I]) > 0) then
        Result := True;
end;

function NeedsOdbc: Boolean;
begin
  // Needed for every database option; harmless for offline mode, so customers can switch later.
  Result := not HasMySqlOdbc;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Dir: String;
begin
  if CurStep = ssInstall then
  begin
    Dir := ExpandConstant('{commonappdata}\BoutiquePOS');
    ForceDirectories(Dir);
    // Tells the app's first-run wizard what was chosen here.
    SaveStringToFile(Dir + '\setup-choice.txt', ChoiceName, False);
  end;
end;
