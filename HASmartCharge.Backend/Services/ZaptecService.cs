using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.DB.Models;
using HASmartCharge.Backend.Services.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Services;

/// <summary>
/// Polls a Zaptec cloud charger (<c>https://api.zaptec.com</c>) and feeds the neutral
/// <see cref="IChargerTelemetry"/> contract directly — no OCPP shapes involved (see
/// <see cref="OcppTelemetryAdapter"/> for the OCPP side). Hosted the same way as
/// <see cref="Mqtt.MqttPublisherService"/>: one singleton, registered both for its own type
/// (so controllers/<c>ChargeControlService</c> can call its public methods) and as a
/// <see cref="BackgroundService"/>.
///
/// Auth is the ROPC password grant (Zaptec offers no refresh token to non-partner integrations
/// as of Nov 2025) — the bearer token lives in memory only and a restart just re-logs-in.
/// The poll loop re-reads <see cref="ChargerSettings"/> every tick so a live settings change
/// (switching charger type, editing credentials) takes effect without restarting the service.
/// </summary>
public sealed class ZaptecService : BackgroundService
{
    private const string BaseUrl = "https://api.zaptec.com";
    private const int PauseCommand = 506; // Zaptec "StopChargingFinal": pauses, FinalStopActive=1, resumable.
    private const int ResumeCommand = 507;

    private static readonly TimeSpan IdleRecheckInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan TokenExpiryMargin = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IChargerTelemetry _telemetry;
    private readonly ChargerStatusTracker _statusTracker;
    private readonly ILogger<ZaptecService> _logger;

    // Token cache. Guarded by _tokenLock so concurrent callers (poll loop + a controller call)
    // don't both hit the 1 req/s login endpoint at once.
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTime _tokenExpiresAtUtc;

    // Per-poll translation state (single charger, so plain fields are fine).
    private bool _wasPolling;
    private string? _polledChargerId;
    private bool? _lastIsOnline;
    private int? _lastOperationMode;
    private Guid? _trackedSessionGuid;
    private int? _trackedSessionId;
    private double _lastSessionKwh;
    private int _consecutiveDisconnectPolls;

    // Status snapshot, read by ZaptecController. Written only from the poll loop (single writer),
    // so plain fields are enough — no interlocking needed for the reads.
    private DateTime? _lastPollAtUtc;
    private string? _lastError;
    private bool? _isOnline;
    private int? _operationMode;
    private volatile bool _finalStopActive;

    public DateTime? LastPollAtUtc => _lastPollAtUtc;
    public string? LastError => _lastError;
    public bool? IsOnline => _isOnline;
    public int? OperationMode => _operationMode;
    public bool FinalStopActive => _finalStopActive;

    public ZaptecService(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IChargerTelemetry telemetry,
        ChargerStatusTracker statusTracker,
        ILogger<ZaptecService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _telemetry = telemetry;
        _statusTracker = statusTracker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Zaptec service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                delay = await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in Zaptec poll tick.");
                _lastError = ex.Message;
                delay = IdleRecheckInterval;
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Zaptec service stopped.");
    }

    /// <summary>One tick: not-configured check, then (if configured) one state poll. Returns how
    /// long to wait before the next tick.</summary>
    private async Task<TimeSpan> TickAsync(CancellationToken ct)
    {
        var settings = await GetSettingsAsync(ct);

        if (!IsZaptecConfigured(settings))
        {
            if (_wasPolling)
            {
                // Disconnect the charger we were actually polling, not settings.ActiveChargerId:
                // when the type has just been switched back to Ocpp that resolves to the OCPP
                // ChargePointId, and marking a live OCPP charger offline breaks the orchestrator's
                // plug-state resolution until it reconnects.
                if (_polledChargerId is not null)
                {
                    _telemetry.OnDisconnected(_polledChargerId);
                }

                ResetTranslationState();
                _polledChargerId = null;
            }

            _wasPolling = false;
            return IdleRecheckInterval;
        }

        _wasPolling = true;
        _polledChargerId = settings.ZaptecChargerId;

        await PollOnceAsync(settings, ct);

        return TimeSpan.FromSeconds(Math.Max(5, settings.ZaptecPollSeconds));
    }

    private static bool IsZaptecConfigured(ChargerSettings settings) =>
        string.Equals(settings.ChargerType, ChargerTypes.Zaptec, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(settings.ZaptecUsername)
        && !string.IsNullOrWhiteSpace(settings.ZaptecPassword)
        && !string.IsNullOrWhiteSpace(settings.ZaptecChargerId);

    private void ResetTranslationState()
    {
        _lastIsOnline = null;
        _lastOperationMode = null;
        _trackedSessionGuid = null;
        _trackedSessionId = null;
        _lastSessionKwh = 0;
        _consecutiveDisconnectPolls = 0;
        _isOnline = null;
        _operationMode = null;
        _finalStopActive = false;
    }

    private async Task PollOnceAsync(ChargerSettings settings, CancellationToken ct)
    {
        var chargerId = settings.ZaptecChargerId;

        List<ZaptecObservation> observations;
        try
        {
            var response = await SendAsync(HttpMethod.Get, $"/api/chargers/{chargerId}/state",
                settings.ZaptecUsername, settings.ZaptecPassword, null, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Zaptec state request failed ({(int)response.StatusCode}): {text}");
            }

            observations = JsonSerializer.Deserialize<List<ZaptecObservation>>(text, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _logger.LogWarning(ex, "Zaptec poll failed for charger {ChargerId}.", chargerId);
            return;
        }

        _lastError = null;
        _lastPollAtUtc = DateTime.UtcNow;

        var byId = new Dictionary<int, string?>();
        foreach (var o in observations)
        {
            byId[o.StateId] = o.ValueAsString;
        }

        TranslateToTelemetry(chargerId, byId);
    }

    /// <summary>
    /// Maps one Zaptec /state poll onto the neutral telemetry contract. See plan step 4 for the
    /// observation-id table; kept as one method since every branch shares the same parsed fields.
    /// </summary>
    private void TranslateToTelemetry(string chargerId, Dictionary<int, string?> byId)
    {
        var isOnline = ParseBool(byId.GetValueOrDefault(-2));
        if (isOnline.HasValue && isOnline != _lastIsOnline)
        {
            if (isOnline.Value) _telemetry.OnConnected(chargerId);
            else _telemetry.OnDisconnected(chargerId);
            _lastIsOnline = isOnline;
        }

        _isOnline = isOnline;
        _telemetry.OnHeartbeat(chargerId);

        var opMode = ParseInt(byId.GetValueOrDefault(710));
        _operationMode = opMode;

        var finalStopActive = ParseBool(byId.GetValueOrDefault(718)) ?? false;
        _finalStopActive = finalStopActive;

        var sessionGuid = ParseGuid(byId.GetValueOrDefault(721));
        var sessionKwh = ParseDouble(byId.GetValueOrDefault(553));

        // Blip guard: a transient opMode==1 while the session GUID is still reported must not end
        // the session — ending and re-starting the same GUID re-derives the same session id, and
        // the recorder's id-reuse path would wipe the session's accumulated hourly cost buckets.
        // A GUID that disappears is an authoritative end; opMode==1 alone needs two consecutive
        // polls before it counts, so one stale poll is ignored outright (no status emit either,
        // which would otherwise terminal-finalize the session via the recorder).
        if (opMode == 1 && _trackedSessionGuid is not null && sessionGuid is not null)
        {
            _consecutiveDisconnectPolls++;
            if (_consecutiveDisconnectPolls < 2)
            {
                return;
            }
        }
        else
        {
            _consecutiveDisconnectPolls = 0;
        }

        // Session end/start, evaluated before the connector-status emission below: a session that
        // ends must be finalized (OnSessionStopped) before the connector flips to Available, or the
        // recorder would finalize a session that's already been wiped from the tracker.
        var previousSessionGuid = _trackedSessionGuid;
        var sessionEnded = previousSessionGuid is not null && (sessionGuid is null || opMode == 1);
        if (sessionEnded)
        {
            _telemetry.OnSessionStopped(chargerId, _trackedSessionId!.Value, _lastSessionKwh, "Ended", DateTimeOffset.UtcNow);
            _trackedSessionGuid = null;
            _trackedSessionId = null;
            _lastSessionKwh = 0;
        }

        var sessionStarting = sessionGuid is not null && sessionGuid != previousSessionGuid && opMode != 1;
        if (sessionStarting)
        {
            var sessionId = DeriveSessionId(sessionGuid!.Value);

            // Restart guard: Program.cs rehydrates an open session at boot with this same
            // deterministic id. Without this check, re-announcing it here would hit the
            // recorder's id-reuse path and wipe the session's accumulated hourly cost buckets.
            var alreadyTracked = _statusTracker.GetConnectorStatus(chargerId, 1)?.ActiveTransactionId == sessionId;
            if (!alreadyTracked)
            {
                _telemetry.OnSessionStarted(chargerId, 1, sessionId, meterStartKwh: 0, "zaptec", DateTimeOffset.UtcNow);
            }

            _trackedSessionGuid = sessionGuid;
            _trackedSessionId = sessionId;
        }

        if (opMode != _lastOperationMode)
        {
            _telemetry.OnConnectorStatus(chargerId, 1, MapConnectorState(opMode, finalStopActive), null);
            _lastOperationMode = opMode;
        }

        if (_trackedSessionGuid is not null)
        {
            _lastSessionKwh = sessionKwh ?? _lastSessionKwh;

            var powerW = ParseDouble(byId.GetValueOrDefault(513));
            _telemetry.OnMeterSample(chargerId, 1, new ChargerMeterSample(
                EnergyRegisterKwh: _lastSessionKwh,
                PowerKw: powerW.HasValue ? powerW / 1000 : null,
                VoltageL1: ParseDouble(byId.GetValueOrDefault(501)),
                VoltageL2: ParseDouble(byId.GetValueOrDefault(502)),
                VoltageL3: ParseDouble(byId.GetValueOrDefault(503)),
                CurrentL1: ParseDouble(byId.GetValueOrDefault(507)),
                CurrentL2: ParseDouble(byId.GetValueOrDefault(508)),
                CurrentL3: ParseDouble(byId.GetValueOrDefault(509)),
                SocPercent: null,
                TimestampUtc: DateTime.UtcNow));
        }
    }

    private static ConnectorState MapConnectorState(int? opMode, bool finalStopActive) => opMode switch
    {
        1 => ConnectorState.Available,
        2 => ConnectorState.Preparing,
        3 => ConnectorState.Charging,
        5 => finalStopActive ? ConnectorState.SuspendedEVSE : ConnectorState.SuspendedEV,
        _ => ConnectorState.Unknown
    };

    /// <summary>Deterministic int session id from the Zaptec session GUID (the recorder's PK is an
    /// OCPP-style int transaction id). Coerces the rare 0 result to 1 — 0 reads as "no session".</summary>
    internal static int DeriveSessionId(Guid sessionGuid)
    {
        var id = BitConverter.ToInt32(sessionGuid.ToByteArray(), 0) & int.MaxValue;
        return id == 0 ? 1 : id;
    }

    internal static bool? ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value == "1") return true;
        if (value == "0") return false;
        return bool.TryParse(value, out var b) ? b : null;
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double? ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static Guid? ParseGuid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value, out var g) ? g : null;

    // --- Public control surface (called by ZaptecController and, in a later step, ChargeControlService) ---

    public async Task<IReadOnlyList<ZaptecCharger>> ListChargersAsync(CancellationToken ct)
    {
        var settings = await GetSettingsAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.ZaptecUsername) || string.IsNullOrWhiteSpace(settings.ZaptecPassword))
        {
            throw new InvalidOperationException("Zaptec credentials are not configured.");
        }

        var response = await SendAsync(HttpMethod.Get, "/api/chargers", settings.ZaptecUsername, settings.ZaptecPassword, null, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Zaptec chargers request failed ({(int)response.StatusCode}): {text}");
        }

        var payload = JsonSerializer.Deserialize<ZaptecChargerListResponse>(text, JsonOptions) ?? new();
        return payload.Data
            .Select(d => new ZaptecCharger(d.Id, d.Name, d.DeviceId, d.IsOnline, d.OperatingMode))
            .ToList();
    }

    public Task PauseAsync(CancellationToken ct) => SendCommandAsync(PauseCommand, ct);

    public Task ResumeAsync(CancellationToken ct) => SendCommandAsync(ResumeCommand, ct);

    private async Task SendCommandAsync(int command, CancellationToken ct)
    {
        var settings = await GetSettingsAsync(ct);
        if (!IsZaptecConfigured(settings))
        {
            throw new InvalidOperationException("Zaptec charger is not configured.");
        }

        var response = await SendAsync(HttpMethod.Post, $"/api/chargers/{settings.ZaptecChargerId}/sendCommand/{command}",
            settings.ZaptecUsername, settings.ZaptecPassword, null, ct);
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Zaptec command {command} failed ({(int)response.StatusCode}): {text}");
        }
    }

    public async Task SetMaxChargeCurrentAsync(double amps, CancellationToken ct)
    {
        var settings = await GetSettingsAsync(ct);
        if (!IsZaptecConfigured(settings))
        {
            throw new InvalidOperationException("Zaptec charger is not configured.");
        }

        var response = await SendAsync(HttpMethod.Post, $"/api/chargers/{settings.ZaptecChargerId}/update",
            settings.ZaptecUsername, settings.ZaptecPassword,
            JsonSerializer.Serialize(new { maxChargeCurrent = amps }), ct);
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Zaptec update failed ({(int)response.StatusCode}): {text}");
        }
    }

    private async Task<ChargerSettings> GetSettingsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.ChargerSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new ChargerSettings();
    }

    // --- HTTP + auth ---

    /// <summary>Sends one authorized request, retrying once on 429 (honoring Retry-After) and once
    /// on 401 (forcing a re-login — the cached token may have been revoked server-side). The body is
    /// passed as JSON text, not as HttpContent: HttpClient disposes the content it sent, so a retry
    /// has to build a fresh one.</summary>
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string username, string password, string? jsonBody, CancellationToken ct)
    {
        var token = await EnsureAccessTokenAsync(username, password, ct);
        var client = _httpClientFactory.CreateClient();

        var response = await SendOnceAsync(client, method, path, token, jsonBody, ct);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero
                ? delta
                : TimeSpan.FromSeconds(1);
            await Task.Delay(retryAfter, ct);
            response = await SendOnceAsync(client, method, path, token, jsonBody, ct);
        }
        else if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            token = await EnsureAccessTokenAsync(username, password, ct, forceRefresh: true);
            response = await SendOnceAsync(client, method, path, token, jsonBody, ct);
        }

        return response;
    }

    private static Task<HttpResponseMessage> SendOnceAsync(
        HttpClient client, HttpMethod method, string path, string token, string? jsonBody, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, $"{BaseUrl}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        return client.SendAsync(request, ct);
    }

    private async Task<string> EnsureAccessTokenAsync(string username, string password, CancellationToken ct, bool forceRefresh = false)
    {
        if (!forceRefresh && _accessToken is not null && DateTime.UtcNow < _tokenExpiresAtUtc - TokenExpiryMargin)
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (!forceRefresh && _accessToken is not null && DateTime.UtcNow < _tokenExpiresAtUtc - TokenExpiryMargin)
            {
                return _accessToken;
            }

            var client = _httpClientFactory.CreateClient();
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = username,
                ["password"] = password,
                ["scope"] = "openid",
            });

            var response = await client.PostAsync($"{BaseUrl}/oauth/token", body, ct);
            var text = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                // Bad credentials (400) or any other login failure: don't cache a token, surface
                // the error text for the status endpoint, and keep the stored credentials — the
                // next poll tick simply tries again.
                _accessToken = null;
                throw new InvalidOperationException($"Zaptec login failed ({(int)response.StatusCode}): {text}");
            }

            var token = JsonSerializer.Deserialize<ZaptecTokenResponse>(text)
                ?? throw new InvalidOperationException("Zaptec login response could not be parsed.");

            _accessToken = token.AccessToken;
            _tokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
            return _accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private sealed class ZaptecTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; } = 3600;
    }

    private sealed class ZaptecObservation
    {
        public int StateId { get; set; }
        public string? ValueAsString { get; set; }
    }

    private sealed class ZaptecChargerListResponse
    {
        public List<ZaptecChargerDto> Data { get; set; } = [];
    }

    private sealed class ZaptecChargerDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? DeviceId { get; set; }
        public bool IsOnline { get; set; }
        public int OperatingMode { get; set; }
    }
}

/// <summary>One charger as listed by <c>GET /api/chargers</c>.</summary>
public sealed record ZaptecCharger(string Id, string Name, string? DeviceId, bool IsOnline, int OperatingMode);
