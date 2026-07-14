using HASmartCharge.Backend.DB;
using HASmartCharge.Backend.Services.Mqtt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// MQTT publisher status + a one-shot broker connectivity test. Settings CRUD lives on
/// <c>SettingsController</c> at <c>api/settings/mqtt</c>.
/// </summary>
[ApiController]
[Route("api/mqtt")]
public class MqttController : ControllerBase
{
    private readonly IMqttPublisherStatus _status;
    private readonly IMqttConnectionTester _tester;
    private readonly ApplicationDbContext _dbContext;

    public MqttController(IMqttPublisherStatus status, IMqttConnectionTester tester, ApplicationDbContext dbContext)
    {
        _status = status;
        _tester = tester;
        _dbContext = dbContext;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var snapshot = _status.GetSnapshot();

        // Timestamps are captured with DateTime.UtcNow in-process (Kind=Utc), so JSON already carries
        // the "Z" suffix — no EnsureUtc re-stamping needed (unlike DB-read DateTimes).
        return Ok(new
        {
            enabled = snapshot.Enabled,
            connected = snapshot.Connected,
            host = snapshot.Host,
            port = snapshot.Port,
            lastConnectedAt = snapshot.LastConnectedAt,
            lastPublishAt = snapshot.LastPublishAt,
            lastError = snapshot.LastError,
            lastErrorAt = snapshot.LastErrorAt
        });
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test(CancellationToken ct)
    {
        var settings = await _dbContext.MqttSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            return NotFound(new { error = "MQTT settings not found" });
        }

        var result = await _tester.TestAsync(settings, ct);
        return Ok(new { success = result.Success, error = result.Error });
    }
}
