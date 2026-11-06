using AgriStream.Web.Data;
using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

[Authorize]
public class DevicesController(AppDbContext db, FunctionsClient functions) : Controller
{
    public async Task<IActionResult> Index() => View(await BuildVm());

    /// <summary>Polled by the page so status pills update after the Queue function runs.</summary>
    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var devices = await db.Devices.AsNoTracking().ToListAsync();
        return Json(devices.Select(d => new
        {
            d.DeviceId,
            status = d.DisplayStatus,
            css = DisplayHelpers.StatusCss(d.DisplayStatus),
            lastIrrigated = d.LastIrrigatedAt.TimeAgo(),
            lastCommandBy = d.LastCommandBy ?? "-"
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Irrigate(string deviceId, int durationMinutes = 10)
    {
        durationMinutes = Math.Clamp(durationMinutes, 1, 120);
        var device = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        if (device is null)
        {
            TempData["Error"] = "We couldn't find that sensor.";
            return RedirectToAction(nameof(Index));
        }

        // HTTP POST Azure Function -> places an "Irrigation Command" on the Storage Queue.
        var (ok, message) = await functions.TriggerIrrigationAsync(new IrrigationRequest
        {
            DeviceId = device.DeviceId,
            DurationMinutes = durationMinutes,
            RequestedBy = User.Identity?.Name ?? "unknown"
        }, HttpContext.RequestAborted);

        TempData[ok ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Create([Bind(Prefix = "NewDevice")] DeviceInputModel input)
    {
        if (ModelState.IsValid && await db.Devices.AnyAsync(d => d.DeviceId == input.DeviceId.Trim()))
            ModelState.AddModelError("NewDevice.DeviceId", "That device ID already exists.");

        if (!ModelState.IsValid)
        {
            var vm = await BuildVm();
            vm.NewDevice = input;
            ViewData["ShowAddDevice"] = true;
            return View(nameof(Index), vm);
        }

        db.Devices.Add(new Device
        {
            DeviceId = input.DeviceId.Trim().ToUpperInvariant(),
            Name = input.Name.Trim(),
            FieldName = input.FieldName.Trim(),
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        TempData["Success"] = $"🌱 Sensor {input.DeviceId.ToUpperInvariant()} added to the registry.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var device = await db.Devices.FindAsync(id);
        if (device is not null)
        {
            db.Devices.Remove(device);
            await db.SaveChangesAsync();
            TempData["Success"] = $"Sensor {device.DeviceId} removed.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<DevicesViewModel> BuildVm() => new()
    {
        Devices = await db.Devices.AsNoTracking().OrderBy(d => d.DeviceId).ToListAsync(),
        IrrigationConfigured = functions.IsIrrigationConfigured
    };
}
