#define BuildFile FileOpen(AddBackslash(SourcePath) + "version.txt")
#define BuildNumber Trim(FileRead(BuildFile))
#expr FileClose(BuildFile)

#if Len(BuildNumber) == 0
  #error build\version.txt is missing or empty
#endif

#define AppName "Project-Agil"
#define AppVersion "b" + BuildNumber
#define AppPublisher "Project-Agil"
#define AppExeName "Project-Agil.exe"
#define AppUrl "https://github.com/no1qq/Project-Agil"

[Setup]
AppId={{7A3C51E8-9D42-4B6F-A1C3-8E5D2F0B94A7}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName}
VersionInfoVersion={#BuildNumber}.0.0.0
VersionInfoProductTextVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=Project-Agil-Setup
SetupIconFile=..\src\ProjectAgil\Assets\app-dark.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Full installation with source code"
Name: "compact"; Description: "Application only"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "app"; Description: "Project-Agil application"; Types: full compact custom; Flags: fixed
Name: "source"; Description: "Full source code"; Types: full

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts"
Name: "startup"; Description: "Start Project-Agil when Windows starts"; GroupDescription: "Startup"; Flags: unchecked

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: app
Source: "..\src\*"; DestDir: "{app}\source\src"; Excludes: "\bin\*,\obj\*,bin,obj"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: source
Source: "..\build\*"; DestDir: "{app}\source\build"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: source
Source: "..\tests\*"; DestDir: "{app}\source\tests"; Excludes: "\bin\*,\obj\*,bin,obj"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: source
Source: "..\Project-Agil.sln"; DestDir: "{app}\source"; Flags: ignoreversion; Components: source
Source: "..\README.md"; DestDir: "{app}\source"; Flags: ignoreversion; Components: source
Source: "..\CLAUDE.md"; DestDir: "{app}\source"; Flags: ignoreversion skipifsourcedoesntexist; Components: source

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\windowsdesktop-runtime.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing the .NET 8 Desktop Runtime..."; Check: RuntimeMissing; Flags: waituntilterminated
Filename: "schtasks.exe"; Parameters: "/Create /TN ""Project-Agil Autostart"" /TR ""\""{app}\{#AppExeName}\"""" /SC ONLOGON /RL HIGHEST /F"; Tasks: startup; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""Project-Agil Autostart"" /F"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveAutostart"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\source"

[Code]
var
  DownloadPage: TDownloadWizardPage;

function DesktopRuntimeInstalled(): Boolean;
var
  FindRec: TFindRec;
  Base: String;
begin
  Result := False;
  Base := ExpandConstant('{commonpf64}') + '\dotnet\shared\Microsoft.WindowsDesktop.App';

  if not DirExists(Base) then
    Exit;

  if FindFirst(Base + '\*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          if (Copy(FindRec.Name, 1, 2) = '8.') or (Copy(FindRec.Name, 1, 2) = '9.') or (Copy(FindRec.Name, 1, 3) = '10.') then
            Result := True;
        end;
      until (Result) or (not FindNext(FindRec));
    finally
      FindClose(FindRec);
    end;
  end;
end;

function RuntimeMissing(): Boolean;
begin
  Result := not DesktopRuntimeInstalled();
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(
    'Downloading required components',
    'Project-Agil needs the .NET 8 Desktop Runtime. Setup is fetching it from Microsoft.',
    nil);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if (CurPageID = wpReady) and RuntimeMissing() then
  begin
    DownloadPage.Clear;
    DownloadPage.Add(
      'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe',
      'windowsdesktop-runtime.exe',
      '');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
      except
        SuppressibleMsgBox(
          'The .NET 8 Desktop Runtime could not be downloaded.' + #13#10 + #13#10 +
          GetExceptionMessage + #13#10 + #13#10 +
          'Install it manually from https://dotnet.microsoft.com/download/dotnet/8.0 and run this setup again.',
          mbCriticalError, MB_OK, IDOK);
        Result := False;
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;

var
  RemoveUserData: Boolean;

function UserDataFolder(): String;
begin
  Result := ExpandConstant('{userappdata}\Project-Agil');
end;

procedure AskAboutUserData();
var
  Form: TSetupForm;
  Intro: TNewStaticText;
  Warning: TNewStaticText;
  Box: TNewCheckBox;
  ContinueButton: TNewButton;
begin
  RemoveUserData := False;

  if not DirExists(UserDataFolder()) then
    Exit;

  Form := CreateCustomForm(ScaleX(430), ScaleY(230), False, True);
  try
    Form.Caption := 'Uninstall Project-Agil';

    Intro := TNewStaticText.Create(Form);
    Intro.Parent := Form;
    Intro.Left := ScaleX(14);
    Intro.Top := ScaleY(14);
    Intro.Width := Form.ClientWidth - ScaleX(28);
    Intro.Height := ScaleY(46);
    Intro.AutoSize := False;
    Intro.WordWrap := True;
    Intro.Caption := 'Your settings, saved setups and undo points are kept in' + #13#10 + UserDataFolder();

    Warning := TNewStaticText.Create(Form);
    Warning.Parent := Form;
    Warning.Left := ScaleX(14);
    Warning.Top := Intro.Top + Intro.Height + ScaleY(10);
    Warning.Width := Form.ClientWidth - ScaleX(28);
    Warning.Height := ScaleY(72);
    Warning.AutoSize := False;
    Warning.WordWrap := True;
    Warning.Caption :=
      'Keeping the folder means a later install picks up where you left off, and the undo points can still put your network settings back.' + #13#10 + #13#10 +
      'Deleting it means anything already applied to Windows stays applied and can never be undone.';

    Box := TNewCheckBox.Create(Form);
    Box.Parent := Form;
    Box.Left := ScaleX(14);
    Box.Top := Warning.Top + Warning.Height + ScaleY(10);
    Box.Width := Form.ClientWidth - ScaleX(28);
    Box.Height := ScaleY(17);
    Box.Checked := False;
    Box.Caption := 'Delete this folder and everything in it';

    ContinueButton := TNewButton.Create(Form);
    ContinueButton.Parent := Form;
    ContinueButton.Caption := 'Continue';
    ContinueButton.Width := ScaleX(90);
    ContinueButton.Height := ScaleY(23);
    ContinueButton.Left := Form.ClientWidth - ScaleX(90 + 14);
    ContinueButton.Top := Form.ClientHeight - ScaleY(23 + 14);
    ContinueButton.ModalResult := mrOk;
    ContinueButton.Default := True;

    Form.ActiveControl := ContinueButton;

    if Form.ShowModal() = mrOk then
      RemoveUserData := Box.Checked;
  finally
    Form.Free;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    AskAboutUserData();

  if (CurUninstallStep = usPostUninstall) and RemoveUserData then
    DelTree(UserDataFolder(), True, True, True);
end;
