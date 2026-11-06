using AgriStream.Web.Models;
using Azure.Data.Tables;

namespace AgriStream.Web.Services;

/// <summary>
/// Writes high-frequency, non-relational sensor telemetry to Azure Table Storage.
/// PartitionKey = DeviceId, RowKey = inverted ticks (newest first).
/// </summary>
public class TelemetryService
{
    private readonly TableClient _table;

    public TelemetryService(TableServiceClient tables, IConfiguration config)
    {
        _table = tables.GetTableClient(config["Storage:TelemetryTable"] ?? "Telemetry");
    }

    public Task EnsureCreatedAsync() => _table.CreateIfNotExistsAsync();

    public async Task WriteAsync(TelemetryReading reading, CancellationToken ct = default)
    {
        await _table.AddEntityAsync(ToEntity(reading), ct);
    }

    /// <summary>Generates realistic readings for each device (used by the "Simulate sensors" button).</summary>
    public async Task<int> SimulateAsync(IEnumerable<string> deviceIds, int readingsPerDevice, CancellationToken ct = default)
    {
        var rnd = Random.Shared;
        var total = 0;

        foreach (var deviceId in deviceIds)
        {
            var moisture = rnd.Next(18, 65) + rnd.NextDouble();
            var airTemp = rnd.Next(16, 30) + rnd.NextDouble();
            var battery = rnd.Next(55, 100);
            var actions = new List<TableTransactionAction>();

            for (var i = 0; i < readingsPerDevice; i++)
            {
                var at = DateTimeOffset.UtcNow.AddMinutes(-(readingsPerDevice - i) * 10);
                moisture = Math.Clamp(moisture + (rnd.NextDouble() - 0.55) * 3, 5, 95);
                airTemp = Math.Clamp(airTemp + (rnd.NextDouble() - 0.4) * 1.2, 5, 42);

                var reading = new TelemetryReading
                {
                    DeviceId = deviceId,
                    RecordedAt = at,
                    SoilMoisture = Math.Round(moisture, 1),
                    SoilTemperature = Math.Round(airTemp - 3 + rnd.NextDouble() * 2, 1),
                    AirTemperature = Math.Round(airTemp, 1),
                    Humidity = Math.Round(Math.Clamp(80 - airTemp + rnd.NextDouble() * 15, 15, 98), 1),
                    Ph = Math.Round(6 + rnd.NextDouble() * 1.4, 2),
                    BatteryLevel = Math.Max(5, battery - i * 0.1)
                };
                actions.Add(new TableTransactionAction(TableTransactionActionType.Add, ToEntity(reading)));
            }

            // Entity group transactions: up to 100 entities in the same partition per batch.
            foreach (var chunk in actions.Chunk(100))
            {
                await _table.SubmitTransactionAsync(chunk, ct);
            }
            total += actions.Count;
        }

        return total;
    }

    /// <summary>Direct read used only as a local fallback when the GET Function URL isn't configured.</summary>
    public async Task<List<TelemetryReading>> GetRecentAsync(int hours = 24, string? deviceId = null, int max = 5000, CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-hours);
        var filter = string.IsNullOrWhiteSpace(deviceId)
            ? TableClient.CreateQueryFilter($"RecordedAt ge {since}")
            : TableClient.CreateQueryFilter($"PartitionKey eq {deviceId} and RecordedAt ge {since}");

        var list = new List<TelemetryReading>();
        await foreach (var e in _table.QueryAsync<TableEntity>(filter, maxPerPage: 1000, cancellationToken: ct))
        {
            list.Add(FromEntity(e));
            if (list.Count >= max) break;
        }
        return list.OrderByDescending(r => r.RecordedAt).ToList();
    }

    private static TableEntity ToEntity(TelemetryReading r) => new(r.DeviceId, EntityHelpers.NewestFirstRowKey(r.RecordedAt))
    {
        ["DeviceId"] = r.DeviceId,
        ["RecordedAt"] = r.RecordedAt,
        ["SoilMoisture"] = r.SoilMoisture,
        ["SoilTemperature"] = r.SoilTemperature,
        ["AirTemperature"] = r.AirTemperature,
        ["Humidity"] = r.Humidity,
        ["Ph"] = r.Ph,
        ["BatteryLevel"] = r.BatteryLevel
    };

    private static TelemetryReading FromEntity(TableEntity e) => new()
    {
        DeviceId = e.PartitionKey,
        RecordedAt = e.Date("RecordedAt"),
        SoilMoisture = e.Dbl("SoilMoisture"),
        SoilTemperature = e.Dbl("SoilTemperature"),
        AirTemperature = e.Dbl("AirTemperature"),
        Humidity = e.Dbl("Humidity"),
        Ph = e.Dbl("Ph"),
        BatteryLevel = e.Dbl("BatteryLevel")
    };
}
