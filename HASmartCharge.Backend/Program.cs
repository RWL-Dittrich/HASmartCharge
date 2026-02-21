using HASmartCharge.Backend.BackgroundServices;
using HASmartCharge.Backend.Configuration;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.Models.HomeAssistant;
using HASmartCharge.Backend.Services;
using HASmartCharge.Backend.Services.Auth;
using HASmartCharge.Backend.Services.Auth.Interfaces;
using HASmartCharge.Backend.Services.Interfaces;
using HASmartCharge.Backend.OCPP.Services;
using Microsoft.EntityFrameworkCore;
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

// New architecture components
builder.Services.AddSingleton<HASmartCharge.Backend.OCPP.Domain.ISessionManager, HASmartCharge.Backend.OCPP.Domain.SessionManager>();
builder.Services.AddSingleton<HASmartCharge.Backend.OCPP.Application.IOcppMessageRouter, HASmartCharge.Backend.OCPP.Application.OcppMessageRouter>();
builder.Services.AddSingleton<HASmartCharge.Backend.OCPP.Infrastructure.OcppConnectionOrchestrator>();

// Command sender (uses new architecture)
builder.Services.AddSingleton<HASmartCharge.Backend.OCPP.Services.ICommandSender, HASmartCharge.Backend.OCPP.Services.SessionCommandSender>();
builder.Services.AddSingleton<ChargerConfigurationService>();

WebApplication app = builder.Build();

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

{
    using IServiceScope scope = app.Services.CreateScope();
    ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    //Do migrations here 
    if((await dbContext.Database.GetPendingMigrationsAsync()).Any())
    {
        await dbContext.Database.MigrateAsync();
    }
    
    // Initialize the Home Assistant connection manager
    IHomeAssistantConnectionManager connectionManager = scope.ServiceProvider.GetRequiredService<IHomeAssistantConnectionManager>();
    await connectionManager.InitializeAsync();
    
    try
    {
        IHomeAssistantApiService apiService = scope.ServiceProvider.GetRequiredService<IHomeAssistantApiService>();
        List<HaEntity> devices = await apiService.GetDevicesAsync();
        Console.WriteLine($"Home Assistant devices found: {devices.Count}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not connect to Home Assistant: {ex.Message}");
    }
}

app.Run();