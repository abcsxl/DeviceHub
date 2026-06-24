using DeviceHub.Cards.TransitCard.Extensions;
using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.PcscReader.Extensions;
using DeviceHub.Devices.Printer.Extensions;
using DeviceHub.Devices.IdCard.Extensions;
using DeviceHub.DriverLoader;
using DeviceHub.Service.Api.Endpoints;
using DeviceHub.Service.Api.Extensions;
using DeviceHub.Service.Api.Models;
using DeviceHub.Service.Api.WebSocket;
using System.Net.NetworkInformation;

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
    Console.WriteLine($"Port {targetPort} is in use, using port {actualPort}");
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
    throw new InvalidOperationException($"Ports {startPort}-{startPort + maxRetries - 1} are all in use, cannot start service");
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
builder.Services.AddTransitCardService(builder.Configuration);
builder.Services.AddPrinterService(builder.Configuration);
builder.Services.AddIdCardService(builder.Configuration);
builder.Services.LoadExternalDrivers(builder.Configuration);
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
    await pcscService.InitAsync();
    registry.Register("Pcsc", pcscService);
    pcscService.CardStatusChanged += (_, args) =>
    {
        _ = wsHandler.SendEventAsync("pcsc", "card_status_changed", args)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    startupLogger.LogError(t.Exception, "Failed to send WebSocket event");
            }, TaskScheduler.Default);
    };
    startupLogger.LogInformation("PCSC driver registered");
}
catch (InvalidOperationException)
{
    startupLogger.LogInformation("PCSC driver not enabled, skipping registration");
}

try
{
    var printerService = app.Services.GetRequiredService<IPrinterService>();
    await printerService.InitAsync();
    registry.Register("Printer", printerService);
    startupLogger.LogInformation("Printer driver registered");
}
catch (InvalidOperationException)
{
    startupLogger.LogInformation("Printer driver not enabled, skipping registration");
}

try
{
    var idCardService = app.Services.GetRequiredService<IIdCardService>();
    await idCardService.InitAsync();
    registry.Register("IdCard", idCardService);
    startupLogger.LogInformation("IdCard driver registered");
}
catch (InvalidOperationException)
{
    startupLogger.LogInformation("IdCard driver not enabled, skipping registration");
}

app.MapStatusEndpoint()
   .MapConfigEndpoint()
   .MapLogsEndpoint()
   .MapDriversEndpoint()
   .MapServiceEndpoint()
   .MapHealthEndpoint();

foreach (var registrar in app.Services.GetServices<IHardwareEndpointRegistrar>())
{
    startupLogger.LogInformation("Mapping endpoints for {Registrar}", registrar.GetType().Name);
    registrar.MapEndpoints(app);
}

wsHandler.MapRoutes(app);

var env = app.Services.GetRequiredService<IWebHostEnvironment>();
var configPath = Path.Combine(env.ContentRootPath, "appsettings.json");
await ConfigEndpoint.InitializeDefaults(configPath);

app.Run();
