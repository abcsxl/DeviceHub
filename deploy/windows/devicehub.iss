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

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  Exec('cmd.exe', '/c "net stop DeviceHub >nul 2>&1"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('cmd.exe', '/c "sc delete DeviceHub >nul 2>&1"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

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

procedure InitializeWizard;
begin
  SelectedPort := 5000;
  HttpPortPage := CreateInputQueryPage(wpSelectComponents,
    '配置 HTTP 端口', '请选择服务监听的 HTTP 端口',
    '默认端口 5000 已被其他程序占用，请指定一个可用端口：');
  HttpPortPage.Add('HTTP 端口:', False);
  HttpPortPage.Values[0] := '5000';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = HttpPortPage.ID then
    Result := not IsPortInUse(5000);
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
    if IsPortInUse(Port) then
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
      StringChangeEx(Lines[i], '"HttpPort": 5000', '"HttpPort": ' + Port, False);
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

        if not IsPortInUse(5000) then
          SetHttpPort(Lines, '5000')
        else
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
