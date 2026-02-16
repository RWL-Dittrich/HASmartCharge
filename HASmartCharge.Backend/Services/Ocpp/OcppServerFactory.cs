using GoldsparkIT.OCPP;

namespace HASmartCharge.Backend.Services.Ocpp;

public interface IOcppServerFactory
{
    OcppServer CreateServer(string chargePointId);
}

public class OcppServerFactory : IOcppServerFactory
{
    private readonly IServiceProvider _serviceProvider;

    public OcppServerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public OcppServer CreateServer(string chargePointId)
    {
        var scope = _serviceProvider.CreateScope();
        var ocppHandler = scope.ServiceProvider.GetRequiredService<JsonServerHandler>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<OcppServer>>();

        return new OcppServer(chargePointId, ocppHandler, logger);
    }
}
