namespace HASmartCharge.Backend.Configuration;

public class HomeAssistantAuthOptions
{
    public const string SectionName = "HomeAssistantAuth";
    
    public int StateExpirationMinutes { get; set; } = 10;
}


