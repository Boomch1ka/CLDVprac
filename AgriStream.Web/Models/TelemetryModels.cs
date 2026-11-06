namespace AgriStream.Web.Models;

/// <summary>A single soil-sensor reading stored in Azure Table Storage.</summary>
public class TelemetryReading
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
    public double SoilMoisture { get; set; }      // %
    public double SoilTemperature { get; set; }   // °C
    public double AirTemperature { get; set; }    // °C
    public double Humidity { get; set; }          // %
    public double Ph { get; set; }
    public double BatteryLevel { get; set; }      // %
}

/// <summary>Shape returned by the GetLatestTelemetry HTTP GET Azure Function.</summary>
public class TelemetrySnapshot
{
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = string.Empty;
    public List<TelemetryReading> Latest { get; set; } = new();
    public List<TelemetryReading> Recent { get; set; } = new();

    public static TelemetrySnapshot FromReadings(IEnumerable<TelemetryReading> readings, string source, int recentLimit = 500)
    {
        var ordered = readings.OrderByDescending(r => r.RecordedAt).ToList();
        return new TelemetrySnapshot
        {
            Source = source,
            Latest = ordered.GroupBy(r => r.DeviceId).Select(g => g.First()).OrderBy(r => r.DeviceId).ToList(),
            Recent = ordered.Take(recentLimit).ToList()
        };
    }
}

/// <summary>Payload sent to the TriggerIrrigation HTTP POST Azure Function.</summary>
public class IrrigationRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
}

public class DroneImageItem
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public string UploadedBy { get; set; } = "unknown";
    public string FieldName { get; set; } = "-";
    public string? Notes { get; set; }
}

/// <summary>Row written to the UploadAudit table by the Blob-triggered function.</summary>
public class UploadAuditEntry
{
    public string BlobName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; }
}

/// <summary>Row written to the IrrigationEvents table by the Queue-triggered function.</summary>
public class IrrigationEventEntry
{
    public string CommandId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
