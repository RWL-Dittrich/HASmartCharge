using HASmartCharge.Backend.Services;
using Microsoft.AspNetCore.SignalR;

namespace HASmartCharge.Backend.Hubs;

/// <summary>
/// Push-only SignalR hub for the developer-tab OCPP frame log. Server-to-client only — no
/// client methods; sending OCPP calls stays plain HTTP via <c>ChargerController</c>.
/// </summary>
public class OcppLogHub : Hub
{
    private readonly OcppFrameLogBuffer _buffer;

    public OcppLogHub(OcppFrameLogBuffer buffer)
    {
        _buffer = buffer;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("frames", _buffer.Snapshot());
        await base.OnConnectedAsync();
    }
}
