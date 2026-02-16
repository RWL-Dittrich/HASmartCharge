using System.Text.Json;
using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

/// <summary>
/// Base class for OCPP messages following the format:
/// [MessageType, MessageId, Action, Payload]
/// or [MessageType, MessageId, Payload] for responses
/// </summary>
public class OcppMessage
{
    [JsonPropertyOrder(0)]
    public int MessageType { get; set; }
    
    [JsonPropertyOrder(1)]
    public string MessageId { get; set; } = string.Empty;
    
    [JsonPropertyOrder(2)]
    public string? Action { get; set; }
    
    [JsonPropertyOrder(3)]
    public JsonElement Payload { get; set; }
    
    public static OcppMessage Parse(string json)
    {
        JsonElement[]? array = JsonSerializer.Deserialize<JsonElement[]>(json);
        if (array == null || array.Length < 3)
        {
            throw new ArgumentException("Invalid OCPP message format");
        }
        
        OcppMessage message = new OcppMessage
        {
            MessageType = array[0].GetInt32(),
            MessageId = array[1].GetString() ?? string.Empty
        };
        
        if (message.MessageType == (int)OcppMessageType.Call && array.Length >= 4)
        {
            message.Action = array[2].GetString();
            message.Payload = array[3];
        }
        else if (message.MessageType == (int)OcppMessageType.CallResult && array.Length >= 3)
        {
            message.Payload = array[2];
        }
        else if (message.MessageType == (int)OcppMessageType.CallError && array.Length >= 4)
        {
            // For CALLERROR: [4, MessageId, ErrorCode, ErrorDescription, ErrorDetails]
            message.Action = array[2].GetString(); // ErrorCode
            message.Payload = array.Length > 3 ? array[3] : new JsonElement();
        }
        
        return message;
    }
    
    public string ToJson()
    {
        if (MessageType == (int)OcppMessageType.Call)
        {
            return JsonSerializer.Serialize(new object[] { MessageType, MessageId, Action!, Payload });
        }
        else
        {
            return JsonSerializer.Serialize(new object[] { MessageType, MessageId, Payload });
        }
    }
}

/// <summary>
/// OCPP Error response following the format:
/// [4, MessageId, ErrorCode, ErrorDescription, ErrorDetails]
/// </summary>
public class OcppErrorMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorDescription { get; set; } = string.Empty;
    public object ErrorDetails { get; set; } = new { };
    
    public string ToJson()
    {
        return JsonSerializer.Serialize(new object[] 
        { 
            (int)OcppMessageType.CallError, 
            MessageId, 
            ErrorCode, 
            ErrorDescription, 
            ErrorDetails 
        });
    }
}
