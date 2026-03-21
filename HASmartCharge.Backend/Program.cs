using HASmartCharge.Application.Commands;
using HASmartCharge.Application.Events;
using HASmartCharge.Application.Interfaces;
using HASmartCharge.Backend.BackgroundServices;
using HASmartCharge.Backend.Configuration;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.Models.HomeAssistant;
using HASmartCharge.Backend.Services;
using HASmartCharge.Backend.Services.Auth;
using HASmartCharge.Backend.Services.Auth.Interfaces;
using HASmartCharge.Backend.Services.Interfaces;
using HASmartCharge.Backend.OCPP.Services;
using HASmartCharge.Backend.OCPP.Services.EventHandlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add HTTP client factory for Home Assistant API calls
builder.Services.AddHttpClient();

// Configure Home Assistant Auth Options
builder.Services.Configure<HomeAssistantAuthOptions>(
    builder.Configuration.GetSection(HomeAssistantAuthOptions.SectionName));

//Configure Database
builder.Services.AddSqlite<ApplicationDbContext>(builder.Configuration.GetConnectionString("DefaultConnection"));

// Register authentication services
builder.Services.AddSingleton<IAuthStateStore, InMemoryAuthStateStore>();
builder.Services.AddSingleton<IHomeAssistantAuthService, HomeAssistantAuthService>();
builder.Services.AddSingleton<IHomeAssistantConnectionManager, HomeAssistantConnectionManager>();

// Register background services
builder.Services.AddHostedService<AuthStateCleanupService>();
builder.Services.AddHostedService<TokenRefreshService>();

// Register services
builder.Services.AddScoped<IHomeAssistantApiService, HomeAssistantApiService>();

// Register OCPP services (new layered architecture)
builder.Services.AddSingleton<WebSocketMessageService>();
builder.Services.AddSingleton<ChargerStatusTracker>();
builder.Services.AddSingleton<IChargerReadModel>(serviceProvider =>
    serviceProvider.GetRequiredService<ChargerStatusTracker>());

// New architecture components
builder.Services.AddSingleton<HASmartCharge.Backend.OCPP.Domain.ISessionManager, HASmartCharge.Backend.OCPP.Domain.SessionManager>();
builder.Services.AddSingleton<HASmartCharge.Backend.OCPP.Application.IOcppMessageRouter, HASmartCharge.Backend.OCPP.Application.OcppMessageRouter>();
builder.Services.AddSingleton<HASmartCharge.Backend.OCPP.Infrastructure.OcppConnectionOrchestrator>();

// Command sender (uses new architecture)
builder.Services.AddSingleton<HASmartCharge.Backend.OCPP.Services.ICommandSender, HASmartCharge.Backend.OCPP.Services.SessionCommandSender>();
builder.Services.AddSingleton<IChargerGateway, OcppChargerGateway>();
builder.Services.AddSingleton<ChargerConfigurationService>();

// Domain event dispatcher
builder.Services.AddSingleton<DomainEventDispatcher>();
builder.Services.AddSingleton<IDomainEventDispatcher>(sp => sp.GetRequiredService<DomainEventDispatcher>());

// Concrete repositories (implement application interfaces, backed by EF Core)
builder.Services.AddSingleton<IChargerRepository, EfChargerRepository>();
builder.Services.AddSingleton<IChargingSessionRepository, EfChargingSessionRepository>();

// Application command handlers
builder.Services.AddSingleton<RegisterChargerHandler>();
builder.Services.AddSingleton<BeginChargingSessionHandler>();
builder.Services.AddSingleton<CompleteChargingSessionHandler>();
builder.Services.AddSingleton<UpdateConnectorStatusHandler>();

// OCPP persistence (legacy — kept for OcppRepository which may still be referenced elsewhere)
builder.Services.AddSingleton<HASmartCharge.Backend.OCPP.Services.IOcppPersistence, HASmartCharge.Backend.DB.OcppRepository>();

WebApplication app = builder.Build();
ILogger startupLogger = app.Logger;

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Enable WebSockets middleware
app.UseWebSockets();

app.UseAuthorization();

app.MapControllers();

// Wire domain event handlers to the dispatcher
{
    var dispatcher = app.Services.GetRequiredService<DomainEventDispatcher>();
    var tracker = app.Services.GetRequiredService<ChargerStatusTracker>();
    dispatcher.Register(new ChargerConnectedHandler(tracker));
    dispatcher.Register(new ChargerDisconnectedHandler(tracker));
    dispatcher.Register(new ChargerRegisteredHandler(tracker));
    dispatcher.Register(new ChargingSessionStartedHandler(tracker));
    dispatcher.Register(new ChargingSessionCompletedHandler(tracker));
    dispatcher.Register(new ConnectorStatusUpdatedHandler(tracker));
}

{
    using IServiceScope scope = app.Services.CreateScope();
    ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    //Do migrations here
    if ((await dbContext.Database.GetPendingMigrationsAsync()).Any())
    {
        await dbContext.Database.MigrateAsync();
    }

    // Seed the in-memory charger status tracker from the database
    // so the API shows all known chargers (as disconnected) before any WebSocket connections arrive
    IChargerRepository chargerRepository = app.Services.GetRequiredService<IChargerRepository>();
    ChargerStatusTracker statusTracker = app.Services.GetRequiredService<ChargerStatusTracker>();
    IReadOnlyList<HASmartCharge.Domain.Entities.Charger> knownChargers = await chargerRepository.GetAllAsync();
    statusTracker.SeedFromDomainChargers(knownChargers);

    // Initialize the Home Assistant connection manager
    IHomeAssistantConnectionManager connectionManager = scope.ServiceProvider.GetRequiredService<IHomeAssistantConnectionManager>();
    await connectionManager.InitializeAsync();

    try
    {
        IHomeAssistantApiService apiService = scope.ServiceProvider.GetRequiredService<IHomeAssistantApiService>();
        List<HaEntity> devices = await apiService.GetDevicesAsync();
        startupLogger.LogInformation("Home Assistant devices found: {DeviceCount}", devices.Count);
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex, "Could not connect to Home Assistant during startup initialization.");
    }
}

app.Run();
