using HASmartCharge.Backend.Services.Auth.Interfaces;
using HASmartCharge.Backend.Services;
using HASmartCharge.Backend.Services.Auth;

namespace HASmartCharge.Backend.BackgroundServices;

public class AuthStateCleanupService : BackgroundService
{
    private readonly IAuthStateStore _authStateStore;
    private readonly ILogger<AuthStateCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    public AuthStateCleanupService(
        IAuthStateStore authStateStore,
        ILogger<AuthStateCleanupService> logger)
    {
        _authStateStore = authStateStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auth State Cleanup Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _authStateStore.CleanupExpiredStates();
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auth state cleanup");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Auth State Cleanup Service stopped");
    }
}

