namespace HASmartCharge.Backend.OCPP.Models;

/// <summary>
/// OCPP Message Types as per OCPP 1.6J specification
/// </summary>
public enum OcppMessageType
{
    /// <summary>
    /// CALL - Request from charge point to central system or vice versa
    /// </summary>
    Call = 2,
    
    /// <summary>
    /// CALLRESULT - Response to a successful request
    /// </summary>
    CallResult = 3,
    
    /// <summary>
    /// CALLERROR - Error response
    /// </summary>
    CallError = 4
}
