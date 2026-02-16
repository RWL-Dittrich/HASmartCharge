using HASmartCharge.Backend.BackgroundServices;
using HASmartCharge.Backend.Configuration;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.Services;
using HASmartCharge.Backend.Services.Auth;
using HASmartCharge.Backend.Services.Auth.Interfaces;
using HASmartCharge.Backend.Services.Interfaces;
using HASmartCharge.Backend.OCPP.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

// Register OCPP services
builder.Services.AddSingleton<WebSocketMessageService>();
builder.Services.AddSingleton<OcppServerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Enable WebSockets middleware
app.UseWebSockets();

app.UseAuthorization();

app.MapControllers();

{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    //Do migrations here 
    if((await dbContext.Database.GetPendingMigrationsAsync()).Any())
    {
        await dbContext.Database.MigrateAsync();
    }
    
    // Initialize the Home Assistant connection manager
    var connectionManager = scope.ServiceProvider.GetRequiredService<IHomeAssistantConnectionManager>();
    await connectionManager.InitializeAsync();
    
    try
    {
        var apiService = scope.ServiceProvider.GetRequiredService<IHomeAssistantApiService>();
        var devices = await apiService.GetDevicesAsync();
        Console.WriteLine($"Home Assistant devices found: {devices.Count}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not connect to Home Assistant: {ex.Message}");
    }
}

app.Run();