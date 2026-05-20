using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.PcscReader;
using DeviceHub.Service.Api.Endpoints;
using DeviceHub.Service.Api.Models;
using DeviceHub.Service.Api.WebSocket;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Environment.ContentRootPath = AppContext.BaseDirectory;
builder.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
    optional: false, reloadOnChange: true);

builder.Services.AddWindowsService();
builder.Services.AddSystemd();

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

builder.Services.AddPcscService(builder.Configuration);
builder.Services.AddHostedService<PingService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

var registry = app.Services.GetRequiredService<DriverRegistry>();

try
{
    var pcscService = app.Services.GetRequiredService<IPcscService>();
    registry.Register("PcscReader", pcscService);
    pcscService.CardStatusChanged += (_, args) =>
    {
        _ = WebSocketHandler.SendEventAsync("pcsc", "card_status_changed", args);
    };
}
catch (InvalidOperationException)
{
    // PCSC driver 未启用，跳过注册
}

app.MapStatusEndpoints()
   .MapConfigEndpoints()
   .MapLogsEndpoints()
   .MapDriversEndpoints()
   .MapServiceEndpoints()
   .MapHealthEndpoints()
   .MapHardwarePcscEndpoints()
   .MapWebSocketHandler();

app.Run();
