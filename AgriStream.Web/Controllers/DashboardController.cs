using AgriStream.Web.Data;
using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

[Authorize]
public class DashboardController(
    AppDbContext db,
    FunctionsClient functions,
    TelemetryService telemetry,
    ILogger<DashboardController> logger) : Controller
{
    public async Task<IActionResult> Index(string? deviceId)
    {
        var vm = new DashboardViewModel
        {
            SelectedDeviceId = deviceId,
            Devices = await db.Devices.AsNoTracking().OrderBy(d => d.DeviceId).ToListAsync()
        };

        // 1) Preferred path: the HTTP GET Azure Function.
        if (functions.IsTelemetryConfigured)
        {
            try
            {
                var snapshot = await functions.GetLatestTelemetryAsync(deviceId, 24, HttpContext.RequestAborted);
                if (snapshot is not null)
                {
                    vm.Snapshot = snapshot;
                    vm.DataSource = "Azure Function · GetLatestTelemetry";
                    vm.FromFunction = true;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "GetLatestTelemetry function call failed - falling back to Table Storage");
                vm.Warning = "The telemetry function didn't answer, so we read Table Storage directly.";
            }
        }
        else
        {
            vm.Warning = "Functions:GetTelemetryUrl isn't set, so we read Table Storage directly.";
        }

        // 2) Fallback: read Azure Table Storage directly.
        if (!vm.FromFunction)
        {
            try
            {
                var recent = await telemetry.GetRecentAsync(24, deviceId, ct: HttpContext.RequestAborted);
                vm.Snapshot = TelemetrySnapshot.FromReadings(recent, "Table Storage");
                vm.DataSource = "Table Storage (direct fallback)";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not read telemetry from Table Storage");
                vm.Warning = "We couldn't load telemetry right now. Check the storage connection string.";
            }
        }

        BuildTrend(vm);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Simulate()
    {
        try
        {
            var ids = await db.Devices.Where(d => d.Status != "Offline").Select(d => d.DeviceId).ToListAsync();
            var count = await telemetry.SimulateAsync(ids, 12, HttpContext.RequestAborted);
            TempData["Success"] = $"📡 {count} fresh sensor readings sent to Table Storage!";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Simulating telemetry failed");
            TempData["Error"] = "Couldn't write simulated readings. Is the storage account reachable?";
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Groups recent readings into 30-minute buckets for the trend chart.</summary>
    private static void BuildTrend(DashboardViewModel vm)
    {
        var buckets = vm.Snapshot.Recent
            .GroupBy(r => new DateTimeOffset(r.RecordedAt.UtcTicks - r.RecordedAt.UtcTicks % TimeSpan.FromMinutes(30).Ticks, TimeSpan.Zero))
            .OrderBy(g => g.Key)
            .TakeLast(24)
            .ToList();

        foreach (var g in buckets)
        {
            vm.TrendLabels.Add(g.Key.ToLocalTime().ToString("HH:mm"));
            vm.TrendMoisture.Add(Math.Round(g.Average(r => r.SoilMoisture), 1));
            vm.TrendTemperature.Add(Math.Round(g.Average(r => r.AirTemperature), 1));
            vm.TrendHumidity.Add(Math.Round(g.Average(r => r.Humidity), 1));
        }
    }
}
