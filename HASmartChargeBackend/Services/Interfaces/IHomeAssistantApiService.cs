using HASmartChargeBackend.Models.HomeAssistant;

namespace HASmartChargeBackend.Services.Interfaces;

public interface IHomeAssistantApiService
{
    Task<List<HaEntity>> GetDevicesAsync();
}