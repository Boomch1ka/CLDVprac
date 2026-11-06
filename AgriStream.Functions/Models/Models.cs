namespace AgriStream.Functions.Models;

public class TelemetryReading
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
    public double SoilMoisture { get; set; }
    public double SoilTemperature { get; set; }
    public double AirTemperature { get; set; }
    public double Humidity { get; set; }
    public double Ph { get; set; }
    public double BatteryLevel { get; set; }
}

public class TelemetrySnapshot
{
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = "Azure Function: GetLatestTelemetry";
    public List<TelemetryReading> Latest { get; set; } = new();
    public List<TelemetryReading> Recent { get; set; } = new();
}

/// <summary>Body of the HTTP POST sent by the MVC app.</summary>
public class IrrigationRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>Message placed on the "irrigation-commands" Storage Queue.</summary>
public class IrrigationCommand
{
    public string CommandId { get; set; } = Guid.NewGuid().ToString("N");
    public string DeviceId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Row written to the UploadAudit table via a Table output binding.</summary>
public class UploadAuditEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}

/// <summary>Row written to the IrrigationEvents table via a Table output binding.</summary>
public class IrrigationEventEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public string CommandId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}

public static class Keys
{
    /// <summary>Row key that sorts newest-first inside a partition.</summary>
    public static string NewestFirst(DateTimeOffset at) => $"{DateTime.MaxValue.Ticks - at.UtcTicks:D19}_{Guid.NewGuid():N}";
}
