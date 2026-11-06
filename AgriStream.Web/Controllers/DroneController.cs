using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriStream.Web.Controllers;

[Authorize]
public class DroneController(BlobMediaService media, ILogger<DroneController> logger) : Controller
{
    private const long MaxBytes = 20 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".tif", ".tiff"
    };

    public async Task<IActionResult> Index()
    {
        var vm = new DroneIndexViewModel();
        try
        {
            vm.Images = await media.ListAsync(ct: HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not list drone images");
            vm.Error = "We couldn't load the gallery. Check the storage connection string.";
        }
        return View(vm);
    }

    [HttpPost]
    [RequestSizeLimit(105 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 105 * 1024 * 1024)]
    public async Task<IActionResult> Upload(List<IFormFile> files, string? fieldName, string? notes)
    {
        if (files is null || files.Count == 0)
        {
            TempData["Error"] = "Pick at least one image to upload 📷";
            return RedirectToAction(nameof(Index));
        }
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            TempData["Error"] = "Tell us which field the drone flew over 🌾";
            return RedirectToAction(nameof(Index));
        }

        var uploaded = 0;
        var skipped = new List<string>();

        foreach (var file in files.Take(10))
        {
            var ext = Path.GetExtension(file.FileName);
            if (file.Length == 0 || file.Length > MaxBytes || !AllowedExtensions.Contains(ext))
            {
                skipped.Add(file.FileName);
                continue;
            }

            try
            {
                await using var stream = file.OpenReadStream();
                await media.UploadAsync(stream, file.FileName,
                    string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    User.Identity?.Name ?? "unknown", fieldName.Trim(), notes, HttpContext.RequestAborted);
                uploaded++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Upload failed for {File}", file.FileName);
                skipped.Add(file.FileName);
            }
        }

        if (uploaded > 0)
            TempData["Success"] = $"🚁 {uploaded} image(s) uploaded! The Blob function will log them in the audit trail.";
        if (skipped.Count > 0)
            TempData["Error"] = $"Skipped: {string.Join(", ", skipped)} (images only, max 20 MB each).";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Proxy used only when the storage client can't issue SAS links.</summary>
    [HttpGet]
    public async Task<IActionResult> Image(string name)
    {
        var result = await media.OpenReadAsync(name, HttpContext.RequestAborted);
        if (result is null) return NotFound();
        return File(result.Value.Content, result.Value.ContentType);
    }
}
