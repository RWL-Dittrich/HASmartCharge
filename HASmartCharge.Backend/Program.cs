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
using HASmartCharge.Backend.Services;
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
builder.Services.AddScoped<IHomeAssistantControl, HomeAssistantControl>();

// OCPP: temporary raw-frame diagnostic log (Ocpp:RawFrameLog section).
OcppRawLog.Configure(
    builder.Configuration.GetValue("Ocpp:RawFrameLog:Enabled", false),
    builder.Configuration["Ocpp:RawFrameLog:Path"]);

// OCPP: transport, routing, sessions
builder.Services.AddSingleton<WebSocketMessageService>();
builder.Services.AddSingleton<ISessionManager, SessionManager>();
builder.Services.AddSingleton<IOcppMessageRouter, OcppMessageRouter>();
builder.Services.AddSingleton<OcppConnectionOrchestrator>();

// OCPP: telemetry sinks (live in-memory charger status + DB-backed session/cost recording),
// fanned out to both from the single IChargerTelemetrySink the OCPP session layer calls.
builder.Services.AddSingleton<ChargerStatusTracker>();
builder.Services.AddSingleton<ChargeSessionRecorder>();
builder.Services.AddSingleton<IChargerTelemetrySink>(sp => new TelemetryFanout(
    [sp.GetRequiredService<ChargerStatusTracker>(), sp.GetRequiredService<ChargeSessionRecorder>()],
    sp.GetRequiredService<ILogger<TelemetryFanout>>()));

// OCPP: outbound command surface (config push, availability, unlock)
builder.Services.AddSingleton<ICommandSender, SessionCommandSender>();
builder.Services.AddSingleton<IOcppChargerConfigurationProvider, DbOcppChargerConfigurationProvider>();
builder.Services.AddSingleton<ChargerConfigurationService>();
builder.Services.AddSingleton<IChargerControl, ChargerControl>();

// Prices: EPEX fetch + cache
builder.Services.AddScoped<IPriceFetcher, EpexPriceFetcher>();
builder.Services.AddHostedService<PriceFetchService>();

// Plan: cheapest-hour schedule calculator wiring
builder.Services.AddScoped<IPlanScheduleService, PlanScheduleService>();

// Charge control: HA start/stop wrapper, manual override window, and the orchestrator loop
builder.Services.AddScoped<IChargeControlService, ChargeControlService>();
builder.Services.AddSingleton<ManualOverrideState>();
builder.Services.AddHostedService<ChargeOrchestratorService>();

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
