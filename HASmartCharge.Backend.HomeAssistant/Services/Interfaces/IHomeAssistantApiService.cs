using HASmartCharge.Backend.HomeAssistant.Models;

namespace HASmartCharge.Backend.HomeAssistant.Services.Interfaces;

public interface IHomeAssistantApiService
{
    Task<List<HaEntity>> GetDevicesAsync();
}
