using HASmartCharge.Backend.Models.HomeAssistant;

namespace HASmartCharge.Backend.Services.Interfaces;

public interface IHomeAssistantApiService
{
    Task<List<HaEntity>> GetDevicesAsync();
}