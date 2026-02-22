using System.Text.Json;

namespace HASmartCharge.Backend.OCPP.Models;

/// <summary>
/// Wraps the outcome of an OCPP command sent from the CSMS to a charge point.
/// </summary>
public class OcppCommandResult
{
    /// <summary>True when a CALLRESULT was received; false on CALLERROR or timeout.</summary>
    public bool Success { get; init; }

    /// <summary>Raw JSON payload from the CALLRESULT, or null on error/timeout.</summary>
    public JsonElement? RawPayload { get; init; }

    /// <summary>OCPP error code from a CALLERROR, or null on success.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Human-readable error description from a CALLERROR, or null on success.</summary>
    public string? ErrorDescription { get; init; }

    /// <summary>Creates a successful result from a CALLRESULT payload.</summary>
    public static OcppCommandResult FromCallResult(JsonElement payload) =>
        new() { Success = true, RawPayload = payload };

    /// <summary>Creates a failed result from a CALLERROR.</summary>
    public static OcppCommandResult FromCallError(string errorCode, string? errorDescription) =>
        new() { Success = false, ErrorCode = errorCode, ErrorDescription = errorDescription };

    /// <summary>Creates a failed result for a command that timed out.</summary>
    public static OcppCommandResult TimedOut() =>
        new() { Success = false, ErrorCode = "Timeout", ErrorDescription = "The charge point did not respond in time" };
}

/// <summary>
/// Typed variant of <see cref="OcppCommandResult"/> that includes a deserialized response object.
/// </summary>
/// <typeparam name="TResponse">The expected OCPP response type (e.g. <see cref="ResetResponse"/>).</typeparam>
public class OcppCommandResult<TResponse> : OcppCommandResult
{
    /// <summary>Deserialized OCPP response, or null on error/timeout.</summary>
    public TResponse? Response { get; init; }
}
