using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Data;

/// <summary>
/// Creates the SQL schema (if missing), seeds roles / admin / sample devices,
/// and makes sure the Blob container and Tables exist.
/// If SQL firewall blocks connection at startup, retries automatically on incoming requests
/// once the user enables Public Network Access in the Azure Portal.
/// </summary>
public static class StartupInitializer
{
    private static volatile bool _sqlReady;
    private static readonly SemaphoreSlim SqlLock = new(1, 1);

    public static bool IsSqlReady => _sqlReady;

    public static async Task InitializeAsync(IServiceProvider services)
    {
        await TryEnsureSqlAsync(services);

        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        try
        {
            await sp.GetRequiredService<BlobMediaService>().EnsureCreatedAsync();
            await sp.GetRequiredService<TelemetryService>().EnsureCreatedAsync();
            await sp.GetRequiredService<AuditService>().EnsureCreatedAsync();
            logger.LogInformation("Azure Storage (blob container + tables) ready.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not initialise Azure Storage. Check ConnectionStrings:AzureStorage.");
        }
    }

    public static async Task TryEnsureSqlAsync(IServiceProvider services)
    {
        if (_sqlReady) return;
        if (!await SqlLock.WaitAsync(0)) return;

        try
        {
            if (_sqlReady) return;
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

            var db = sp.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db, sp.GetRequiredService<IPasswordHasher<User>>(), sp.GetRequiredService<IConfiguration>());
            _sqlReady = true;
            logger.LogInformation("Azure SQL database ready.");
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
            logger.LogWarning("Azure SQL not reachable yet ({Message}). Enable Public Network Access + Allow Azure Services on 'agristream' SQL server in Azure Portal.", ex.Message);
        }
        finally
        {
            SqlLock.Release();
        }
    }

    private static async Task SeedAsync(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration config)
    {
        var adminName = config["SeedAdmin:Username"] ?? "admin";
        if (!await db.Users.AnyAsync(u => u.Username == adminName))
        {
            var adminRoleId = await db.Roles.Where(r => r.Name == AppRoles.Admin).Select(r => r.Id).FirstAsync();
            var admin = new User { Username = adminName, RoleId = adminRoleId, CreatedAt = DateTime.UtcNow };
            admin.PasswordHash = hasher.HashPassword(admin, config["SeedAdmin:Password"] ?? "Admin@123");
            db.Users.Add(admin);
        }

        if (!await db.Devices.AnyAsync())
        {
            db.Devices.AddRange(
                new Device { DeviceId = "SENSOR-001", Name = "North Maize Probe", FieldName = "North Field" },
                new Device { DeviceId = "SENSOR-002", Name = "Sunflower Ridge Probe", FieldName = "East Ridge" },
                new Device { DeviceId = "SENSOR-003", Name = "Orchard Drip Sensor", FieldName = "Citrus Orchard" },
                new Device { DeviceId = "SENSOR-004", Name = "Wheat Valley Probe", FieldName = "South Valley" },
                new Device { DeviceId = "SENSOR-005", Name = "Greenhouse Monitor", FieldName = "Greenhouse A" },
                new Device { DeviceId = "SENSOR-006", Name = "Vineyard Probe", FieldName = "West Vineyard", Status = "Maintenance" });
        }

        await db.SaveChangesAsync();
    }
}
