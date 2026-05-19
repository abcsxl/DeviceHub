#define MyAppName "DeviceHub"
#define MyAppPublisher "anomalyco"
#define MyAppURL "https://github.com/anomalyco/DeviceHub"
#define MyAppExeName "DeviceHub.Service.Api.exe"

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\..\src\DeviceHub.Service.Api\bin\Release\net10.0\win-x64\publish"
#endif

[Setup]
AppId={{B8F4A23A-8F3C-4A7C-9C5E-1D2E3F4A5B6C}
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
Filename: "{app}\{#MyAppExeName}"; Parameters: "--install"; Flags: runhidden; StatusMsg: "正在注册 Windows 服务..."
Filename: "net"; Parameters: "start DeviceHub"; Flags: runhidden; StatusMsg: "正在启动服务..."

[UninstallRun]
Filename: "net"; Parameters: "stop DeviceHub"; Flags: runhidden
Filename: "{app}\{#MyAppExeName}"; Parameters: "--uninstall"; Flags: runhidden

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ConfigPath: String;
  ConfigJson: String;
begin
  if CurStep = ssPostInstall then
  begin
    ConfigPath := ExpandConstant('{app}\appsettings.json');
    if FileExists(ConfigPath) then
    begin
      ConfigJson := '';
      if IsComponentSelected('pcsc') then
        ConfigJson := '{"Drivers":{"Pcsc":{"Enabled":true}}}'
      else
        ConfigJson := '{"Drivers":{"Pcsc":{"Enabled":false}}}';
      SaveStringToFile(ConfigPath, ConfigJson, False);
    end;
  end;
end;
