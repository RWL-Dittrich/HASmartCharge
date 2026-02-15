using HASmartChargeBackend.Models.Auth;

namespace HASmartChargeBackend.Services.Auth.Interfaces;

public interface IAuthStateStore
{
    /// <summary>
    /// Stores a new auth state with the given state token
    /// </summary>
    void StoreState(AuthState authState);
    
    /// <summary>
    /// Retrieves and validates an auth state by state token
    /// </summary>
    AuthState? GetState(string state);
    
    /// <summary>
    /// Removes an auth state from the store
    /// </summary>
    void RemoveState(string state);
    
    /// <summary>
    /// Updates an existing auth state with authorization code
    /// </summary>
    bool UpdateAuthorizationCode(string state, string authorizationCode);
    
    /// <summary>
    /// Clean up expired states
    /// </summary>
    void CleanupExpiredStates();
}

