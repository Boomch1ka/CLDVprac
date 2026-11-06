using System.Net;
using System.Web;
using AgriStream.Functions.Models;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions;

/// <summary>
/// (2) HTTP TRIGGER - GET
/// Called by the MVC dashboard to fetch the latest telemetry from the "Telemetry" table.
/// GET /api/telemetry/latest?hours=24&amp;deviceId=SENSOR-001
/// </summary>
public class GetLatestTelemetryFunction(TableServiceClient tables, ILogger<GetLatestTelemetryFunction> logger)
{
    [Function("GetLatestTelemetry")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "telemetry/latest")] HttpRequestData req)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var hours = int.TryParse(query["hours"], out var h) ? Math.Clamp(h, 1, 168) : 24;
        var deviceId = query["deviceId"];
        var since = DateTimeOffset.UtcNow.AddHours(-hours);

        var table = tables.GetTableClient("Telemetry");
        await table.CreateIfNotExistsAsync();

        var filter = string.IsNullOrWhiteSpace(deviceId)
            ? TableClient.CreateQueryFilter($"RecordedAt ge {since}")
            : TableClient.CreateQueryFilter($"PartitionKey eq {deviceId} and RecordedAt ge {since}");

        var readings = new List<TelemetryReading>();
        await foreach (var e in table.QueryAsync<TableEntity>(filter, maxPerPage: 1000))
        {
            readings.Add(new TelemetryReading
            {
                DeviceId = e.PartitionKey,
                RecordedAt = e.GetDateTimeOffset("RecordedAt") ?? e.Timestamp ?? DateTimeOffset.UtcNow,
                SoilMoisture = e.GetDouble("SoilMoisture") ?? 0,
                SoilTemperature = e.GetDouble("SoilTemperature") ?? 0,
                AirTemperature = e.GetDouble("AirTemperature") ?? 0,
                Humidity = e.GetDouble("Humidity") ?? 0,
                Ph = e.GetDouble("Ph") ?? 0,
                BatteryLevel = e.GetDouble("BatteryLevel") ?? 0
            });
            if (readings.Count >= 5000) break;
        }

        var ordered = readings.OrderByDescending(r => r.RecordedAt).ToList();
        var snapshot = new TelemetrySnapshot
        {
            Latest = ordered.GroupBy(r => r.DeviceId).Select(g => g.First()).OrderBy(r => r.DeviceId).ToList(),
            Recent = ordered.Take(500).ToList()
        };

        logger.LogInformation("GetLatestTelemetry returned {Devices} devices / {Readings} readings (hours={Hours}, device={Device})",
            snapshot.Latest.Count, snapshot.Recent.Count, hours, deviceId ?? "all");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(snapshot);
        return response;
    }
}
