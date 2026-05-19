using DeviceHub.Devices.PcscReader;
using DeviceHub.Service.Api.Endpoints;
using DeviceHub.Service.Api.Models;
using DeviceHub.Service.Api.WebSocket;

var builder = WebApplication.CreateSlimBuilder(args);

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

app.MapStatusEndpoints()
   .MapConfigEndpoints()
   .MapLogsEndpoints()
   .MapDriversEndpoints()
   .MapServiceEndpoints()
   .MapHealthEndpoints()
   .MapHardwarePcscEndpoints()
   .MapWebSocketHandler();

app.Run();
