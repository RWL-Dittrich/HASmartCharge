using HASmartCharge.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace HASmartCharge.Backend.Controllers;

/// <summary>
/// Manual charge control: start/stop the car directly via Home Assistant, bypassing
/// the orchestrator's automatic toggling for a configurable override window.
/// </summary>
[ApiController]
[Route("api/charge")]
public class ChargeController : ControllerBase
{
    private const int DefaultOverrideMinutes = 60;

    private readonly IChargeControlService _chargeControl;
    private readonly ManualOverrideState _overrideState;

    public ChargeController(IChargeControlService chargeControl, ManualOverrideState overrideState)
    {
        _chargeControl = chargeControl;
        _overrideState = overrideState;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromQuery] int overrideMinutes = DefaultOverrideMinutes, CancellationToken ct = default)
    {
        _overrideState.Activate(TimeSpan.FromMinutes(overrideMinutes));

        try
        {
            await _chargeControl.StartChargingAsync(ct);
            return Ok(new { overrideUntilUtc = _overrideState.OverrideUntilUtc });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop([FromQuery] int overrideMinutes = DefaultOverrideMinutes, CancellationToken ct = default)
    {
        _overrideState.Activate(TimeSpan.FromMinutes(overrideMinutes));

        try
        {
            await _chargeControl.StopChargingAsync(ct);
            return Ok(new { overrideUntilUtc = _overrideState.OverrideUntilUtc });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }
}
