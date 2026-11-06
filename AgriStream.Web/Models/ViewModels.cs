using System.ComponentModel.DataAnnotations;

namespace AgriStream.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "We need your username 🙂")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Don't forget your password")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required, StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_.\-]+$", ErrorMessage = "Letters, numbers, dots, dashes and underscores only.")]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords don't match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = AppRoles.Farmer;
}

public class CreateUserViewModel
{
    [Required, StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_.\-]+$", ErrorMessage = "Letters, numbers, dots, dashes and underscores only.")]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, Display(Name = "Role")]
    public int RoleId { get; set; } = 1;
}

public class UsersViewModel
{
    public List<User> Users { get; set; } = new();
    public List<Role> Roles { get; set; } = new();
    public CreateUserViewModel NewUser { get; set; } = new();
}

public class DeviceInputModel
{
    [Required, StringLength(50), Display(Name = "Device ID")]
    [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Letters, numbers, dashes and underscores only.")]
    public string DeviceId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "Field")]
    public string FieldName { get; set; } = string.Empty;
}

public class DevicesViewModel
{
    public List<Device> Devices { get; set; } = new();
    public bool IrrigationConfigured { get; set; }
    public DeviceInputModel NewDevice { get; set; } = new();
}

public class DashboardViewModel
{
    public TelemetrySnapshot Snapshot { get; set; } = new();
    public List<Device> Devices { get; set; } = new();
    public string DataSource { get; set; } = string.Empty;
    public bool FromFunction { get; set; }
    public string? Warning { get; set; }
    public string? SelectedDeviceId { get; set; }

    public List<string> TrendLabels { get; set; } = new();
    public List<double> TrendMoisture { get; set; } = new();
    public List<double> TrendTemperature { get; set; } = new();
    public List<double> TrendHumidity { get; set; } = new();

    private double Avg(Func<TelemetryReading, double> selector) =>
        Snapshot.Latest.Count == 0 ? 0 : Math.Round(Snapshot.Latest.Average(selector), 1);

    public double AvgMoisture => Avg(r => r.SoilMoisture);
    public double AvgAirTemperature => Avg(r => r.AirTemperature);
    public double AvgHumidity => Avg(r => r.Humidity);
    public double AvgBattery => Avg(r => r.BatteryLevel);
    public int IrrigatingCount => Devices.Count(d => d.IsIrrigating);
}

public class DroneIndexViewModel
{
    public List<DroneImageItem> Images { get; set; } = new();
    public string? Error { get; set; }
}

public class AuditViewModel
{
    public List<UploadAuditEntry> Uploads { get; set; } = new();
    public List<IrrigationEventEntry> Irrigations { get; set; } = new();
    public string? Error { get; set; }
}
