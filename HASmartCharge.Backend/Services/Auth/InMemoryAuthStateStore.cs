using System.Collections.Concurrent;
using HASmartCharge.Backend.Models.Auth;
using HASmartCharge.Backend.Services.Auth.Interfaces;

namespace HASmartCharge.Backend.Services.Auth;

public class InMemoryAuthStateStore : IAuthStateStore
{
    private readonly ConcurrentDictionary<string, AuthState> _states = new();
    private readonly ILogger<InMemoryAuthStateStore> _logger;

    public InMemoryAuthStateStore(ILogger<InMemoryAuthStateStore> logger)
    {
        _logger = logger;
    }

    public void StoreState(AuthState authState)
    {
        _states[authState.State] = authState;
        _logger.LogInformation("Stored auth state for state token: {State}", authState.State);
    }

    public AuthState? GetState(string state)
    {
        if (_states.TryGetValue(state, out AuthState? authState))
        {
            if (authState.ExpiresAt > DateTime.UtcNow)
            {
                _logger.LogInformation("Retrieved valid auth state for state token: {State}", state);
                return authState;
            }
            
            _logger.LogWarning("Auth state expired for state token: {State}", state);
            RemoveState(state);
            return null;
        }
        
        _logger.LogWarning("Auth state not found for state token: {State}", state);
        return null;
    }

    public void RemoveState(string state)
    {
        _states.TryRemove(state, out _);
        _logger.LogInformation("Removed auth state for state token: {State}", state);
    }

    public bool UpdateAuthorizationCode(string state, string authorizationCode)
    {
        if (_states.TryGetValue(state, out AuthState? authState))
        {
            authState.AuthorizationCode = authorizationCode;
            _logger.LogInformation("Updated authorization code for state token: {State}", state);
            return true;
        }
        
        _logger.LogWarning("Failed to update authorization code - state not found: {State}", state);
        return false;
    }

    public void CleanupExpiredStates()
    {
        List<string> expiredStates = _states
            .Where(kvp => kvp.Value.ExpiresAt <= DateTime.UtcNow)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string state in expiredStates)
        {
            _states.TryRemove(state, out _);
        }

        if (expiredStates.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired auth states", expiredStates.Count);
        }
    }
}

