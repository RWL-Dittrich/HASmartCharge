using System.Text.Json;

namespace HASmartCharge.Application.Interfaces;

public sealed class ChargerCommandResult
{
    public bool Success { get; init; }
    public JsonElement? RawPayload { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDescription { get; init; }

    public static ChargerCommandResult Succeeded(JsonElement? rawPayload = null) =>
        new() { Success = true, RawPayload = rawPayload };

    public static ChargerCommandResult Failed(string? errorCode, string? errorDescription, JsonElement? rawPayload = null) =>
        new() { Success = false, ErrorCode = errorCode, ErrorDescription = errorDescription, RawPayload = rawPayload };

    public static ChargerCommandResult ChargerNotFound() =>
        Failed("ChargerNotFound", "No active session for the requested charger.");

    public static ChargerCommandResult ChargerOffline() =>
        Failed("ChargerOffline", "The charger is connected but not in an active state.");
}
