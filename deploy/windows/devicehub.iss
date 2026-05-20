#define MyAppName "DeviceHub"
#define MyAppPublisher "abcsxl"
#define MyAppURL "https://github.com/abcsxl/DeviceHub"
#define MyAppExeName "DeviceHub.Service.Api.exe"

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\..\src\DeviceHub.Service.Api\bin\Release\net10.0\win-x64\publish"
#endif

[LangOptions]
chinesesimplified.DialogFontName=Microsoft YaHei
chinesesimplified.WelcomeFontName=Microsoft YaHei
chinesesimplified.TitleFontName=Microsoft YaHei

[Setup]
AppId={{B8F4A23A-8F3C-4A7C-9C5E-1D2E3F4A5B6C}
WizardStyle=modern
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\..\output
OutputBaseFilename=DeviceHub-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=admin

[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full"; Description: "Full installation (all hardware drivers)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "pcsc"; Description: "PCSC 读卡器支持"; Types: full custom; Flags: disablenouninstallwarning
Name: "main"; Description: "核心服务"; Types: full custom; Flags: fixed disablenouninstallwarning

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Run]
Filename: "sc"; Parameters: "create DeviceHub binPath= ""{app}\{#MyAppExeName}"" start= auto"; Flags: runhidden; StatusMsg: "正在注册服务..."
Filename: "net"; Parameters: "start DeviceHub"; Flags: runhidden; StatusMsg: "正在启动服务..."

[UninstallRun]
Filename: "net"; Parameters: "stop DeviceHub"; Flags: runhidden
Filename: "sc"; Parameters: "delete DeviceHub"; Flags: runhidden

[Code]
var
  HttpPortPage: TInputQueryWizardPage;
  SelectedPort: Integer;
  ExistingPort: Integer;
  IsUpgrade: Boolean;

function IsPortInUse(Port: Integer): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('cmd.exe', '/c "netstat -ano | findstr :' + IntToStr(Port) + ' >nul"', '',
                 SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := (ResultCode = 0);
end;

function TryParseInt(const S: String; out Value: Integer): Boolean;
var
  i, Len: Integer;
  C: Char;
  Negative: Boolean;
begin
  Result := False;
  Value := 0;
  Len := Length(S);
  if Len = 0 then Exit;

  i := 1;
  Negative := False;
  if S[1] = '-' then
  begin
    Negative := True;
    i := 2;
  end
  else if S[1] = '+' then
  begin
    i := 2;
  end;

  if i > Len then Exit;

  while i <= Len do
  begin
    C := S[i];
    if (C < '0') or (C > '9') then Exit;
    Value := Value * 10 + (Ord(C) - Ord('0'));
    i := i + 1;
  end;

  if Negative then Value := -Value;
  Result := True;
end;

function IsValidPort(Port: Integer): Boolean;
begin
  Result := (Port >= 1) and (Port <= 65535);
end;

function ReadExistingPort(const ConfigPath: String): Integer;
var
  Lines: TArrayOfString;
  i: Integer;
  Line: String;
  StartPos: Integer;
  PortStr: String;
  Port: Integer;
begin
  Result := 5000;
  if not FileExists(ConfigPath) then Exit;
  if not LoadStringsFromFile(ConfigPath, Lines) then Exit;

  for i := 0 to GetArrayLength(Lines) - 1 do
  begin
    Line := Lines[i];
    if Pos('"HttpPort":', Line) > 0 then
    begin
      StartPos := Pos(':', Line);
      if StartPos > 0 then
      begin
        PortStr := Trim(Copy(Line, StartPos + 1, Length(Line)));
        if TryParseInt(PortStr, Port) and IsValidPort(Port) then
          Result := Port;
      end;
      Break;
    end;
  end;
end;

function GetOldInstallPath: String;
var
  RegKey: String;
  Path: String;
begin
  Result := '';
  RegKey := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{B8F4A23A-8F3C-4A7C-9C5E-1D2E3F4A5B6C}_is1';
  if RegQueryStringValue(HKLM, RegKey, 'InstallLocation', Path) then
    Result := Path
  else if RegQueryStringValue(HKCU, RegKey, 'InstallLocation', Path) then
    Result := Path;
end;

procedure InitializeWizard;
var
  OldPath: String;
  OldConfigPath: String;
begin
  IsUpgrade := False;
  SelectedPort := 0;
  ExistingPort := 0;
  
  OldPath := GetOldInstallPath;
  if OldPath <> '' then
  begin
    OldConfigPath := OldPath + '\appsettings.json';
    if FileExists(OldConfigPath) then
    begin
      IsUpgrade := True;
      ExistingPort := ReadExistingPort(OldConfigPath);
      SelectedPort := ExistingPort;
    end;
  end;

  if SelectedPort = 0 then
  begin
    ExistingPort := 5000;
    SelectedPort := 5000;
  end;

  HttpPortPage := CreateInputQueryPage(wpSelectComponents,
    '配置 HTTP 端口', '请选择服务监听的 HTTP 端口',
    '默认端口 5000 已被其他程序占用，请指定一个可用端口：');
  HttpPortPage.Add('HTTP 端口:', False);
  HttpPortPage.Values[0] := IntToStr(SelectedPort);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = HttpPortPage.ID then
  begin
    // 覆盖安装且使用原端口时跳过检查（安装时会先停止旧服务）
    if IsUpgrade and (SelectedPort = ExistingPort) then
      Result := True
    else
      Result := not IsPortInUse(SelectedPort);
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  PortStr: String;
  Port: Integer;
begin
  Result := True;
  if CurPageID = HttpPortPage.ID then
  begin
    PortStr := HttpPortPage.Values[0];
    if not TryParseInt(PortStr, Port) or not IsValidPort(Port) then
    begin
      MsgBox('请输入有效的端口号（1-65535）', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    // 覆盖安装且使用原端口时跳过占用检查
    if not (IsUpgrade and (Port = ExistingPort)) and IsPortInUse(Port) then
    begin
      MsgBox('端口 ' + PortStr + ' 已被占用，请选择其他端口', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    SelectedPort := Port;
  end;
end;

procedure SetDriverEnabled(var Lines: TArrayOfString; const SectionName, EnabledStr: String);
var
  i: Integer;
begin
  for i := 0 to GetArrayLength(Lines) - 1 do
  begin
    if Pos('"' + SectionName + '":', Lines[i]) > 0 then
    begin
      if i + 1 < GetArrayLength(Lines) then
      begin
        StringChangeEx(Lines[i + 1], '"Enabled": true', '"Enabled": ' + EnabledStr, False);
        StringChangeEx(Lines[i + 1], '"Enabled": false', '"Enabled": ' + EnabledStr, False);
      end;
      Break;
    end;
  end;
end;

procedure SetHttpPort(var Lines: TArrayOfString; const Port: String);
var
  i: Integer;
begin
  for i := 0 to GetArrayLength(Lines) - 1 do
  begin
    if Pos('"HttpPort":', Lines[i]) > 0 then
    begin
      StringChangeEx(Lines[i], '"HttpPort": ' + IntToStr(ExistingPort), '"HttpPort": ' + Port, False);
      Break;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ConfigPath: String;
  Lines: TArrayOfString;
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    Exec('cmd.exe', '/c "net stop DeviceHub >nul 2>&1"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('cmd.exe', '/c "sc delete DeviceHub >nul 2>&1"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;

  if CurStep = ssPostInstall then
  begin
    ConfigPath := ExpandConstant('{app}\appsettings.json');
    if FileExists(ConfigPath) then
    begin
      if LoadStringsFromFile(ConfigPath, Lines) then
      begin
        if IsComponentSelected('pcsc') then
          SetDriverEnabled(Lines, 'Pcsc', 'true')
        else
          SetDriverEnabled(Lines, 'Pcsc', 'false');

        SetHttpPort(Lines, IntToStr(SelectedPort));

        SaveStringsToFile(ConfigPath, Lines, False);
      end;
    end;
  end;

  if CurStep = ssDone then
  begin
    Sleep(3000);
    if Exec('cmd.exe', '/c "sc query DeviceHub | findstr RUNNING >nul"', '',
            SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if ResultCode <> 0 then
        MsgBox('服务启动失败，请重启计算机以启动 DeviceHub 服务。', mbInformation, MB_OK);
    end;
  end;
end;
