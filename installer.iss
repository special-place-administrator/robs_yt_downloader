; Inno Setup Script for Rob's YouTube Downloader
; Requires Inno Setup 6.0 or later: https://jrsoftware.org/isinfo.php

#define MyAppName "Rob's YouTube Downloader"
#define MyAppVersion "1.2.1"
#define MyAppPublisher "Rob's Software"
#define MyAppURL "https://github.com/special-place-administrator/robs_yt_downloader"
#define MyAppExeName "RobsYTDownloader.exe"
#define MyAppAssocName MyAppName + " File"
#define MyAppAssocExt ".ytd"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt

[Setup]
; App information
AppId={{8F9E5A2C-1D3B-4C7E-9A8B-2F4E6D8C1A3B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE.txt
OutputDir=installer_output
OutputBaseFilename=RobsYTDownloader-Setup-v{#MyAppVersion}
SetupIconFile=app_icon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
; Update/Upgrade settings
AppContact={#MyAppURL}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
; Close running app before upgrade
CloseApplications=force
RestartApplications=no
CloseApplicationsFilter={#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main application files
Source: "bin\Release\net8.0-windows\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net8.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net8.0-windows\*.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net8.0-windows\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs

; Configuration template
Source: "oauth_config.json.template"; DestDir: "{app}"; Flags: ignoreversion

; Documentation
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
; Create app data directory for user
Name: "{userappdata}\RobsYTDownloader"; Flags: uninsneveruninstall
Name: "{userappdata}\RobsYTDownloader\tools"; Flags: uninsneveruninstall

[Icons]
; Start Menu shortcuts
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{group}\README"; Filename: "{app}\README.md"

; Desktop icon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Option to launch app after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  PrereqPage: TOutputMsgMemoWizardPage;
  NeedsDotNet: Boolean;
  NeedsNodeJs: Boolean;

// Check if .NET 8.0 Runtime is installed
function IsDotNet8Installed(): Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if Exec('cmd.exe', '/c dotnet --list-runtimes | findstr "Microsoft.WindowsDesktop.App 8."', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
      Result := True;
  end;
end;

// Check if Node.js is installed
function IsNodeJsInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('cmd.exe', '/c node --version', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure InitializeWizard();
var
  PrereqMessage: String;
begin
  // Check prerequisites
  NeedsDotNet := not IsDotNet8Installed();
  NeedsNodeJs := not IsNodeJsInstalled();

  // Create a page showing missing prerequisites
  if NeedsDotNet or NeedsNodeJs then
  begin
    PrereqPage := CreateOutputMsgMemoPage(wpWelcome,
      'Prerequisites Check',
      'The following components are required or recommended',
      'This installer will install the application, but you need to install the following components manually:',
      '');

    PrereqMessage := '';

    if NeedsDotNet then
    begin
      PrereqMessage := PrereqMessage +
        '*** REQUIRED: .NET 8.0 Desktop Runtime ***' + #13#10 +
        'Download from: https://dotnet.microsoft.com/download/dotnet/8.0' + #13#10 +
        'Choose: ".NET Desktop Runtime 8.0.x - Windows x64"' + #13#10 + #13#10;
    end;

    if NeedsNodeJs then
    begin
      PrereqMessage := PrereqMessage +
        '*** RECOMMENDED: Node.js (for 8K video support) ***' + #13#10 +
        'Download from: https://nodejs.org/' + #13#10 +
        'Choose: "LTS version (Recommended For Most Users)"' + #13#10 + #13#10;
    end;

    PrereqMessage := PrereqMessage +
      'You can continue with the installation and install these later, ' +
      'but the application will not run without .NET 8.0 Runtime.' + #13#10 + #13#10 +
      'Node.js is optional but highly recommended for downloading high-quality (4K/8K) videos.';

    PrereqPage.RichEditViewer.Text := PrereqMessage;
  end;
end;

// Show configuration instructions after installation
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Create oauth_config.json from template if it doesn't exist
    if not FileExists(ExpandConstant('{app}\oauth_config.json')) then
    begin
      CopyFile(ExpandConstant('{app}\oauth_config.json.template'),
               ExpandConstant('{app}\oauth_config.json'), False);
    end;

    // Show setup instructions
    MsgBox('Installation complete!' + #13#10#13#10 +
           'When you first launch the application, a setup wizard will guide you through configuring Google OAuth for high-quality video downloads.' + #13#10#13#10 +
           'You can also skip the setup and configure it later in Settings.' + #13#10#13#10 +
           'The app will create required folders in %APPDATA%\RobsYTDownloader on first run.',
           mbInformation, MB_OK);
  end;
end;

[Registry]
; Add app to PATH (optional, for command-line usage)
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; \
    ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; \
    Check: NeedsAddPath('{app}')

[Code]
function NeedsAddPath(Param: string): boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
    'Path', OrigPath)
  then begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + Param + ';', ';' + OrigPath + ';') = 0;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{app}\runtimes"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.json"
