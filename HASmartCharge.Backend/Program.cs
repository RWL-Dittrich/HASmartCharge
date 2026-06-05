using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.HomeAssistant.Auth;
using HASmartCharge.Backend.HomeAssistant.Auth.Interfaces;
using HASmartCharge.Backend.HomeAssistant.BackgroundServices;
using HASmartCharge.Backend.HomeAssistant.Configuration;
using HASmartCharge.Backend.HomeAssistant.Services;
using HASmartCharge.Backend.HomeAssistant.Services.Interfaces;
using HASmartCharge.Backend.OCPP.Application;
using HASmartCharge.Backend.OCPP.Domain;
using HASmartCharge.Backend.OCPP.Infrastructure;
using HASmartCharge.Backend.OCPP.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

// Home Assistant auth options
builder.Services.Configure<HomeAssistantAuthOptions>(
    builder.Configuration.GetSection(HomeAssistantAuthOptions.SectionName));

// Database
builder.Services.AddSqlite<ApplicationDbContext>(builder.Configuration.GetConnectionString("DefaultConnection"));

// Home Assistant authentication
builder.Services.AddSingleton<IAuthStateStore, InMemoryAuthStateStore>();
builder.Services.AddSingleton<IHomeAssistantAuthService, HomeAssistantAuthService>();
builder.Services.AddSingleton<IHomeAssistantConnectionManager, HomeAssistantConnectionManager>();
builder.Services.AddHostedService<AuthStateCleanupService>();
builder.Services.AddHostedService<TokenRefreshService>();
builder.Services.AddScoped<IHomeAssistantApiService, HomeAssistantApiService>();

// OCPP: transport, routing, sessions
builder.Services.AddSingleton<WebSocketMessageService>();
builder.Services.AddSingleton<ISessionManager, SessionManager>();
builder.Services.AddSingleton<IOcppMessageRouter, OcppMessageRouter>();
builder.Services.AddSingleton<OcppConnectionOrchestrator>();

// OCPP: telemetry sink (live in-memory charger status)
builder.Services.AddSingleton<ChargerStatusTracker>();
builder.Services.AddSingleton<IChargerTelemetrySink>(sp => sp.GetRequiredService<ChargerStatusTracker>());

// OCPP: outbound command surface (config push, availability, unlock)
builder.Services.AddSingleton<ICommandSender, SessionCommandSender>();
builder.Services.AddSingleton<ChargerConfigurationService>();
builder.Services.AddSingleton<IChargerControl, ChargerControl>();

var app = builder.Build();
var startupLogger = app.Logger;

// HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseWebSockets();
app.UseAuthorization();
app.MapControllers();

// Startup initialization
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Idempotent: applies any pending migrations, creates the DB if absent, no-ops when current.
    await dbContext.Database.MigrateAsync();

    var connectionManager = scope.ServiceProvider.GetRequiredService<IHomeAssistantConnectionManager>();
    await connectionManager.InitializeAsync();

    try
    {
        var apiService = scope.ServiceProvider.GetRequiredService<IHomeAssistantApiService>();
        var devices = await apiService.GetDevicesAsync();
        startupLogger.LogInformation("Home Assistant devices found: {DeviceCount}", devices.Count);
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex, "Could not connect to Home Assistant during startup initialization.");
    }
}

app.Run();
