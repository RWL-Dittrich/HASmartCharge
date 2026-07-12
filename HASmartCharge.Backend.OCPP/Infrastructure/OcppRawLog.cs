namespace HASmartCharge.Backend.OCPP.Infrastructure;

/// <summary>
/// Diagnostic append-only log of every raw OCPP frame exchanged with a charger, written
/// straight to a file independent of the ILogger level configuration. TEMPORARY
/// troubleshooting aid — configured via the <c>Ocpp:RawFrameLog</c> appsettings section
/// (see <see cref="Configure"/>). Disabled until <see cref="Configure"/> is called at startup.
/// </summary>
public static class OcppRawLog
{
    private static readonly object _gate = new();
    private static string? _path;

    public static bool IsEnabled => _path is not null;

    /// <summary>
    /// Enable/disable raw frame logging. Call once at startup with values bound from
    /// configuration. When <paramref name="enabled"/> is true, frames are appended to
    /// <paramref name="path"/> (relative paths resolve against the working directory);
    /// a null/blank path falls back to <c>ocpp-raw.log</c>.
    /// </summary>
    public static void Configure(bool enabled, string? path)
    {
        if (!enabled)
        {
            _path = null;
            return;
        }

        var resolved = string.IsNullOrWhiteSpace(path) ? "ocpp-raw.log" : path;
        _path = Path.IsPathRooted(resolved)
            ? resolved
            : Path.Combine(Directory.GetCurrentDirectory(), resolved);
    }

    /// <summary>Append one direction-tagged frame. "in" = charger→CS, "out" = CS→charger.</summary>
    public static void Append(string chargePointId, string direction, string frame)
    {
        if (_path is null)
        {
            return;
        }

        var line = $"{DateTime.UtcNow:O} [{chargePointId}] {direction} {frame}{Environment.NewLine}";
        try
        {
            lock (_gate)
            {
                File.AppendAllText(_path, line);
            }
        }
        catch
        {
            // Diagnostics must never disrupt message processing.
        }
    }
}
