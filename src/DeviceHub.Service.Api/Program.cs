using System.Net;
using System.Net.NetworkInformation;
using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.PcscReader;
using DeviceHub.Service.Api.Endpoints;
using DeviceHub.Service.Api.Extensions;
using DeviceHub.Service.Api.Models;
using DeviceHub.Service.Api.WebSocket;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddWindowsService();
builder.Services.AddSystemd();

var configPort = builder.Configuration.GetValue<int>("Server:HttpPort", 5000);
var cliPort = builder.Configuration.GetValue<int?>("port");
var targetPort = cliPort ?? configPort;
var actualPort = FindAvailablePort(targetPort, 10);

if (actualPort != targetPort)
{
    Console.WriteLine($"端口 {targetPort} 已被占用，使用端口 {actualPort}");
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(actualPort);
});

static int FindAvailablePort(int startPort, int maxRetries)
{
    for (var i = 0; i < maxRetries; i++)
    {
        var port = startPort + i;
        if (!IsPortInUse(port))
            return port;
    }
    throw new InvalidOperationException($"端口 {startPort}-{startPort + maxRetries - 1} 均被占用，无法启动服务");
}

static bool IsPortInUse(int port)
{
    return IPGlobalProperties.GetIPGlobalProperties()
        .GetActiveTcpListeners()
        .Any(ep => ep.Port == port);
}

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var logProvider = new InMemoryLogProvider(builder.Configuration);
builder.Services.AddSingleton(logProvider);
builder.Logging.AddProvider(logProvider);

builder.Services.AddSingleton<DriverRegistry>();
builder.Services.AddSingleton<ServiceState>();
builder.Services.AddSingleton<WebSocketHandler>();

builder.Services.AddPcscService(builder.Configuration);
builder.Services.AddHostedService<PingService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddAppLocalization();

var app = builder.Build();

var serviceState = app.Services.GetRequiredService<ServiceState>();
serviceState.HttpPort = actualPort;

app.UseCors();
app.UseAppLocalization();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

var registry = app.Services.GetRequiredService<DriverRegistry>();
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
var wsHandler = app.Services.GetRequiredService<WebSocketHandler>();

try
{
    var pcscService = app.Services.GetRequiredService<IPcscService>();
    registry.Register("PcscReader", pcscService);
    pcscService.CardStatusChanged += (_, args) =>
    {
        _ = wsHandler.SendEventAsync("pcsc", "card_status_changed", args);
    };
    startupLogger.LogInformation("PCSC 驱动程序已注册");
}
catch (InvalidOperationException)
{
    startupLogger.LogInformation("PCSC 驱动程序未启用，跳过注册");
}

app.MapStatusEndpoints()
   .MapConfigEndpoints()
   .MapLogsEndpoints()
   .MapDriversEndpoints()
   .MapServiceEndpoints()
   .MapHealthEndpoints()
   .MapHardwarePcscEndpoints();

wsHandler.MapRoutes(app);

app.Run();
