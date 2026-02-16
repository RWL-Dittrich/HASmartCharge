using System.Text.Json.Serialization;

namespace HASmartCharge.Backend.OCPP.Models;

// ==================== BootNotification ====================

public class BootNotificationRequest
{
    [JsonPropertyName("chargePointVendor")]
    public string ChargePointVendor { get; set; } = string.Empty;
    
    [JsonPropertyName("chargePointModel")]
    public string ChargePointModel { get; set; } = string.Empty;
    
    [JsonPropertyName("chargePointSerialNumber")]
    public string? ChargePointSerialNumber { get; set; }
    
    [JsonPropertyName("chargeBoxSerialNumber")]
    public string? ChargeBoxSerialNumber { get; set; }
    
    [JsonPropertyName("firmwareVersion")]
    public string? FirmwareVersion { get; set; }
    
    [JsonPropertyName("iccid")]
    public string? Iccid { get; set; }
    
    [JsonPropertyName("imsi")]
    public string? Imsi { get; set; }
    
    [JsonPropertyName("meterType")]
    public string? MeterType { get; set; }
    
    [JsonPropertyName("meterSerialNumber")]
    public string? MeterSerialNumber { get; set; }
}

public class BootNotificationResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Accepted"; // Accepted, Pending, Rejected
    
    [JsonPropertyName("currentTime")]
    public DateTime CurrentTime { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("interval")]
    public int Interval { get; set; } = 60;
}

// ==================== Heartbeat ====================

public class HeartbeatRequest
{
    // Empty payload
}

public class HeartbeatResponse
{
    [JsonPropertyName("currentTime")]
    public DateTime CurrentTime { get; set; } = DateTime.UtcNow;
}

// ==================== Authorize ====================

public class AuthorizeRequest
{
    [JsonPropertyName("idTag")]
    public string IdTag { get; set; } = string.Empty;
}

public class IdTagInfo
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Accepted"; // Accepted, Blocked, Expired, Invalid, ConcurrentTx
    
    [JsonPropertyName("expiryDate")]
    public DateTime? ExpiryDate { get; set; }
    
    [JsonPropertyName("parentIdTag")]
    public string? ParentIdTag { get; set; }
}

public class AuthorizeResponse
{
    [JsonPropertyName("idTagInfo")]
    public IdTagInfo IdTagInfo { get; set; } = new();
}

// ==================== StartTransaction ====================

public class StartTransactionRequest
{
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }
    
    [JsonPropertyName("idTag")]
    public string IdTag { get; set; } = string.Empty;
    
    [JsonPropertyName("meterStart")]
    public int MeterStart { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("reservationId")]
    public int? ReservationId { get; set; }
}

public class StartTransactionResponse
{
    [JsonPropertyName("idTagInfo")]
    public IdTagInfo IdTagInfo { get; set; } = new();
    
    [JsonPropertyName("transactionId")]
    public int TransactionId { get; set; }
}

// ==================== StopTransaction ====================

public class SampledValue
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    
    [JsonPropertyName("context")]
    public string? Context { get; set; }
    
    [JsonPropertyName("format")]
    public string? Format { get; set; }
    
    [JsonPropertyName("measurand")]
    public string? Measurand { get; set; }
    
    [JsonPropertyName("phase")]
    public string? Phase { get; set; }
    
    [JsonPropertyName("location")]
    public string? Location { get; set; }
    
    [JsonPropertyName("unit")]
    public string? Unit { get; set; }
}

public class MeterValue
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("sampledValue")]
    public List<SampledValue> SampledValue { get; set; } = new();
}

public class StopTransactionRequest
{
    [JsonPropertyName("transactionId")]
    public int TransactionId { get; set; }
    
    [JsonPropertyName("idTag")]
    public string? IdTag { get; set; }
    
    [JsonPropertyName("meterStop")]
    public int MeterStop { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
    
    [JsonPropertyName("transactionData")]
    public List<MeterValue>? TransactionData { get; set; }
}

public class StopTransactionResponse
{
    [JsonPropertyName("idTagInfo")]
    public IdTagInfo? IdTagInfo { get; set; }
}

// ==================== MeterValues ====================

public class MeterValuesRequest
{
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }
    
    [JsonPropertyName("transactionId")]
    public int? TransactionId { get; set; }
    
    [JsonPropertyName("meterValue")]
    public List<MeterValue> MeterValue { get; set; } = new();
}

public class MeterValuesResponse
{
    // Empty payload
}

// ==================== StatusNotification ====================

public class StatusNotificationRequest
{
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = "NoError";
    
    [JsonPropertyName("info")]
    public string? Info { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }
    
    [JsonPropertyName("vendorId")]
    public string? VendorId { get; set; }
    
    [JsonPropertyName("vendorErrorCode")]
    public string? VendorErrorCode { get; set; }
}

public class StatusNotificationResponse
{
    // Empty payload
}

// ==================== DataTransfer ====================

public class DataTransferRequest
{
    [JsonPropertyName("vendorId")]
    public string VendorId { get; set; } = string.Empty;
    
    [JsonPropertyName("messageId")]
    public string? MessageId { get; set; }
    
    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

public class DataTransferResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Accepted"; // Accepted, Rejected, UnknownMessageId, UnknownVendorId
    
    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

// ==================== DiagnosticsStatusNotification ====================

public class DiagnosticsStatusNotificationRequest
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Idle, Uploaded, UploadFailed, Uploading
}

public class DiagnosticsStatusNotificationResponse
{
    // Empty payload
}

// ==================== FirmwareStatusNotification ====================

public class FirmwareStatusNotificationRequest
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Downloaded, DownloadFailed, Downloading, Idle, InstallationFailed, Installing, Installed
}

public class FirmwareStatusNotificationResponse
{
    // Empty payload
}
