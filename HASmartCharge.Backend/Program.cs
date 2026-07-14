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
using HASmartCharge.Backend.Services.Mqtt;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Home Assistant add-on options are written here by the Supervisor; optional so
// standalone runs simply ignore it. Keys map into IConfiguration (e.g. log_level).
builder.Configuration.AddJsonFile("/data/options.json", optional: true, reloadOnChange: false);

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

// OCPP: raw-frame diagnostic log. The add-on options (ocpp_raw_frame_log /
// ocpp_raw_frame_log_path, from /data/options.json) take precedence over the
// Ocpp:RawFrameLog appsettings section used in standalone/dev runs.
OcppRawLog.Configure(
    builder.Configuration.GetValue<bool?>("ocpp_raw_frame_log")
        ?? builder.Configuration.GetValue("Ocpp:RawFrameLog:Enabled", false),
    builder.Configuration["ocpp_raw_frame_log_path"]
        ?? builder.Configuration["Ocpp:RawFrameLog:Path"]);

// OCPP: transport, routing, sessions
builder.Services.AddSingleton<WebSocketMessageService>();
builder.Services.AddSingleton<ISessionManager, SessionManager>();
builder.Services.AddSingleton<IOcppMessageRouter, OcppMessageRouter>();
builder.Services.AddSingleton<OcppConnectionOrchestrator>();

// OCPP: telemetry sinks (live in-memory charger status + DB-backed session/cost recording +
// MQTT wake nudge), fanned out from the single IChargerTelemetrySink the OCPP session layer calls.
builder.Services.AddSingleton<ChargerStatusTracker>();
builder.Services.AddSingleton<ChargeSessionRecorder>();
builder.Services.AddSingleton<MqttTelemetryNudge>();
builder.Services.AddSingleton<IChargerTelemetrySink>(sp => new TelemetryFanout(
    [
        sp.GetRequiredService<ChargerStatusTracker>(),
        sp.GetRequiredService<ChargeSessionRecorder>(),
        sp.GetRequiredService<MqttTelemetryNudge>()
    ],
    sp.GetRequiredService<ILogger<TelemetryFanout>>()));

// OCPP: outbound command surface (config push, availability, unlock)
builder.Services.AddSingleton<ICommandSender, SessionCommandSender>();
builder.Services.AddSingleton<IOcppChargerConfigurationProvider, DbOcppChargerConfigurationProvider>();
builder.Services.AddSingleton<ChargerConfigurationService>();
builder.Services.AddSingleton<IChargerControl, ChargerControl>();

// Prices: EPEX fetch + cache
builder.Services.AddScoped<IPriceFetcher, EpexPriceFetcher>();
builder.Services.AddHostedService<PriceFetchService>();

// Plan: cheapest-hour schedule calculator wiring + shared plan factory (manual + auto-arm)
builder.Services.AddScoped<IPlanScheduleService, PlanScheduleService>();
builder.Services.AddScoped<IChargePlanFactory, ChargePlanFactory>();

// Auto-schedule: recurring weekly departure + overrides → next-deadline resolver
builder.Services.AddScoped<IAutoScheduleResolver, AutoScheduleResolver>();

// Charge control: HA start/stop wrapper, manual override window, and the orchestrator loop
builder.Services.AddScoped<IChargeControlService, ChargeControlService>();
builder.Services.AddSingleton<ManualOverrideState>();
builder.Services.AddSingleton<PlugStateTracker>();
builder.Services.AddHostedService<ChargeOrchestratorService>();

// MQTT: publish charging telemetry to Home Assistant via MQTT discovery + serve the switch command.
// The publisher is one hosted singleton that also backs the /api/mqtt/status endpoint.
builder.Services.AddSingleton<MqttSnapshotBuilder>();
builder.Services.AddSingleton<MqttAvailabilityCommandHandler>();
builder.Services.AddSingleton<IMqttSettingsNotifier, MqttSettingsNotifier>();
builder.Services.AddSingleton<IMqttConnectionTester, MqttConnectionTester>();
builder.Services.AddSingleton<MqttPublisherService>();
builder.Services.AddSingleton<IMqttPublisherStatus>(sp => sp.GetRequiredService<MqttPublisherService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttPublisherService>());

var app = builder.Build();
var startupLogger = app.Logger;

if (OcppRawLog.IsEnabled)
{
    startupLogger.LogInformation("OCPP raw frame log enabled: {Path}", OcppRawLog.FilePath);
}

// HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseWebSockets();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

// Serve the bundled SPA (present in the container image under wwwroot). Any route that
// isn't an API/OCPP endpoint or a static file returns index.html, with <base href> set
// to the HA ingress path (X-Ingress-Path header) so the client resolves assets, API calls
// and router routes under the prefix. Skipped when no SPA is bundled (standalone dev runs
// the Vite server separately on :5173).
var spaIndexPath = app.Environment.WebRootPath is { } webRoot
    ? Path.Combine(webRoot, "index.html")
    : null;
if (spaIndexPath is not null && File.Exists(spaIndexPath))
{
    app.MapFallback(async context =>
    {
        var ingressPath = context.Request.Headers["X-Ingress-Path"].ToString();
        var html = await File.ReadAllTextAsync(spaIndexPath);
        if (!string.IsNullOrEmpty(ingressPath))
        {
            html = html.Replace("<base href=\"/\"", $"<base href=\"{ingressPath}/\"");
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html);
    });
}

// Startup initialization
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Idempotent: applies any pending migrations, creates the DB if absent, no-ops when current.
    await dbContext.Database.MigrateAsync();

    // Rehydrate charge sessions left open by a mid-charge restart. The OCPP telemetry maps are
    // in-memory, so without this a charger that keeps charging across a restart has its meter
    // values dropped (no tracked transaction) and the session is never continued. Runs before
    // app.Run() starts Kestrel, so it always precedes the charger reconnecting. Seed the newest
    // open session per connector; any older still-open rows are stale duplicates.
    var openSessions = await dbContext.ChargeSessions
        .AsNoTracking()
        .Where(s => s.CompletedAt == null)
        .OrderByDescending(s => s.StartedAt)
        .ToListAsync();

    var recorder = scope.ServiceProvider.GetRequiredService<ChargeSessionRecorder>();
    var statusTracker = scope.ServiceProvider.GetRequiredService<ChargerStatusTracker>();
    var seededConnectors = new HashSet<(string, int)>();
    foreach (var session in openSessions)
    {
        if (!seededConnectors.Add((session.ChargePointId, session.ConnectorId)))
        {
            continue; // a newer open session already owns this connector
        }

        recorder.AdoptOpenSession(session.ChargePointId, session.ConnectorId, session.TransactionId);
        statusTracker.SeedActiveTransaction(
            session.ChargePointId, session.ConnectorId, session.TransactionId,
            session.MeterStartWh / 1000.0, session.StartedAt);
    }

    if (openSessions.Count > 0)
    {
        startupLogger.LogInformation("Rehydrated {Count} open charge session(s) after startup.", seededConnectors.Count);
    }

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
