using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriStream.Web.Controllers;

[Authorize(Roles = AppRoles.Agronomist + "," + AppRoles.Admin)]
public class AuditController(AuditService audit, ILogger<AuditController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        var vm = new AuditViewModel();
        try
        {
            vm.Uploads = await audit.GetUploadAuditAsync(ct: HttpContext.RequestAborted);
            vm.Irrigations = await audit.GetIrrigationEventsAsync(ct: HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not read audit tables");
            vm.Error = "We couldn't read the audit tables. Check the storage connection string.";
        }
        return View(vm);
    }
}
