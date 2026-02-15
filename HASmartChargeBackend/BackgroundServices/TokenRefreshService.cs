using HASmartChargeBackend.Services;
using HASmartChargeBackend.Services.Auth;
using HASmartChargeBackend.Services.Auth.Interfaces;

namespace HASmartChargeBackend.BackgroundServices;

/// <summary>
/// Background service that automatically refreshes the Home Assistant access token
/// </summary>
public class TokenRefreshService : BackgroundService
{
    private readonly ILogger<TokenRefreshService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public TokenRefreshService(
        ILogger<TokenRefreshService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Token refresh service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
                
                using var scope = _serviceProvider.CreateScope();
                var connectionManager = scope.ServiceProvider.GetRequiredService<IHomeAssistantConnectionManager>();
                
                if (connectionManager.IsConnected())
                {
                    var connection = connectionManager.GetConnection();
                    if (connection != null)
                    {
                        var timeUntilExpiry = connection.ExpiresAt - DateTime.UtcNow;
                        
                        // Refresh if token expires in less than 10 minutes
                        if (timeUntilExpiry.TotalMinutes < 10)
                        {
                            _logger.LogInformation("Token expires in {Minutes} minutes, refreshing...", 
                                timeUntilExpiry.TotalMinutes);
                            
                            await connectionManager.RefreshAccessTokenAsync();
                        }
                        else
                        {
                            _logger.LogDebug("Token still valid for {Minutes} minutes", 
                                timeUntilExpiry.TotalMinutes);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in token refresh service");
            }
        }
        
        _logger.LogInformation("Token refresh service stopped");
    }
}

