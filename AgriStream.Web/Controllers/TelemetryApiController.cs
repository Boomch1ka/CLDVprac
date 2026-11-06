using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriStream.Web.Controllers;

/// <summary>
/// Endpoints used by field sensors and by the load-testing script.
///  POST /api/telemetry           - ingest one sensor reading into Table Storage (header X-Device-Key)
///  GET  /api/telemetry/forecast  - CPU-heavy soil-moisture forecast (great for triggering CPU auto-scale)
///  GET  /api/telemetry/instance  - which App Service instance served the request (scale-out evidence)
/// </summary>
[ApiController]
[Route("api/telemetry")]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class TelemetryApiController(TelemetryService telemetry, IConfiguration config) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ingest([FromBody] TelemetryReading reading, [FromHeader(Name = "X-Device-Key")] string? deviceKey)
    {
        if (string.IsNullOrEmpty(deviceKey) || deviceKey != config["Telemetry:IngestKey"])
            return Unauthorized(new { error = "Missing or invalid X-Device-Key header." });
        if (string.IsNullOrWhiteSpace(reading.DeviceId))
            return BadRequest(new { error = "deviceId is required." });

        if (reading.RecordedAt == default) reading.RecordedAt = DateTimeOffset.UtcNow;
        await telemetry.WriteAsync(reading, HttpContext.RequestAborted);
        return Accepted(new { reading.DeviceId, reading.RecordedAt, instance = InstanceName });
    }

    [HttpGet("forecast")]
    public IActionResult Forecast(double moisture = 40, int days = 7, int simulations = 20000)
    {
        simulations = Math.Clamp(simulations, 100, 100_000);
        days = Math.Clamp(days, 1, 30);
        moisture = Math.Clamp(moisture, 0, 100);

        // Monte-Carlo simulation of hourly evaporation and random rain showers.
        var rnd = new Random();
        double total = 0;
        var dry = 0;
        for (var s = 0; s < simulations; s++)
        {
            var m = moisture;
            for (var h = 0; h < days * 24; h++)
            {
                m -= 0.08 + rnd.NextDouble() * 0.12;            // evaporation
                if (rnd.NextDouble() < 0.01) m += 5 + rnd.NextDouble() * 10; // rain
                m = Math.Clamp(m, 0, 100);
            }
            total += m;
            if (m < 25) dry++;
        }

        return Ok(new
        {
            startMoisture = moisture,
            days,
            simulations,
            expectedMoisture = Math.Round(total / simulations, 2),
            chanceOfDrySoil = Math.Round(dry / (double)simulations, 3),
            instance = InstanceName
        });
    }

    [HttpGet("instance")]
    public IActionResult Instance() => Ok(new { instance = InstanceName, machine = Environment.MachineName, utc = DateTimeOffset.UtcNow });

    private static string InstanceName =>
        Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") is { Length: > 0 } id ? id[..Math.Min(12, id.Length)] : Environment.MachineName;
}
