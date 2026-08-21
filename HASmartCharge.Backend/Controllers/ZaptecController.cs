using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// Read-only Zaptec endpoints: list the chargers on the configured account, and report poller
/// status. Credentials go through the normal charger-settings PUT (<c>SettingsController</c>) —
/// there's no separate connect/disconnect step (Zaptec has no refresh token to store).
/// </summary>
[ApiController]
[Route("api/zaptec")]
public class ZaptecController : ControllerBase
{
    private readonly ZaptecService _zaptecService;
    private readonly ApplicationDbContext _dbContext;

    public ZaptecController(ZaptecService zaptecService, ApplicationDbContext dbContext)
    {
        _zaptecService = zaptecService;
        _dbContext = dbContext;
    }

    [HttpGet("chargers")]
    public async Task<IActionResult> GetChargers(CancellationToken ct)
    {
        try
        {
            var chargers = await _zaptecService.ListChargersAsync(ct);
            return Ok(chargers);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var settings = await _dbContext.ChargerSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var isZaptec = string.Equals(settings?.ChargerType, ChargerTypes.Zaptec, StringComparison.OrdinalIgnoreCase);

        // Captured with DateTime.UtcNow in-process (Kind=Utc); re-stamp anyway per CLAUDE.md so
        // this stays correct even if the field's provenance ever changes.
        var lastPollAt = _zaptecService.LastPollAtUtc is { } t ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : (DateTime?)null;
        var lastError = _zaptecService.LastError;

        return Ok(new
        {
            connected = isZaptec && lastError is null && lastPollAt is not null,
            lastPollAt,
            lastError,
            isOnline = _zaptecService.IsOnline,
            operationMode = _zaptecService.OperationMode
        });
    }

    private static readonly string[] _allowedApiCallMethods = ["GET", "POST", "PUT", "DELETE"];

    /// <summary>
    /// Developer/diagnostic escape hatch (Settings → Developer tab): forwards an arbitrary request
    /// to the Zaptec API under the account's bearer token — the Zaptec twin of
    /// <c>POST /api/charger/ocpp/call</c>. Restricted to <c>/api/...</c> paths so it can't be
    /// pointed at the token endpoint. Nothing in the app calls this.
    /// </summary>
    [HttpPost("api-call")]
    public async Task<IActionResult> SendApiCall([FromBody] ZaptecApiCallRequest request, CancellationToken ct)
    {
        var method = request.Method?.Trim().ToUpperInvariant() ?? "";
        if (!_allowedApiCallMethods.Contains(method))
        {
            return BadRequest(new { error = "Method must be GET, POST, PUT or DELETE" });
        }

        // Relative /api/ paths only: keeps the call on the Zaptec REST API (no /oauth/token, no
        // absolute URLs that would leak the bearer token to another host).
        if (request.Path is null || !request.Path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || request.Path.Contains("..", StringComparison.Ordinal))
        {
            return BadRequest(new { error = "Path must be a relative Zaptec API path starting with /api/" });
        }

        try
        {
            var result = await _zaptecService.CallApiAsync(method, request.Path, request.Body, ct);
            return Ok(new { statusCode = result.StatusCode, success = result.Success, body = result.Body });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }

    public record ZaptecApiCallRequest(string? Method, string? Path, string? Body);
}
