using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriStream.Web.Models;

/// <summary>System roles stored in Azure SQL (Farmer, Agronomist, Admin).</summary>
public static class AppRoles
{
    public const string Farmer = "Farmer";
    public const string Agronomist = "Agronomist";
    public const string Admin = "Admin";

    public static readonly string[] All = { Farmer, Agronomist, Admin };
    public static readonly string[] SelfRegister = { Farmer, Agronomist };
}

/// <summary>Roles table (one role -> many users).</summary>
public class Role
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}

/// <summary>Users table holding login credentials (hashed password) and an assigned role.</summary>
public class User
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    public int RoleId { get; set; }
    public Role? Role { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Master device registry. Updated by the Queue-triggered Azure Function.</summary>
public class Device
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string DeviceId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FieldName { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Online";

    public DateTime? LastIrrigatedAt { get; set; }
    public DateTime? IrrigationEndsAt { get; set; }

    [MaxLength(50)]
    public string? LastCommandBy { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public bool IsIrrigating => IrrigationEndsAt.HasValue && IrrigationEndsAt.Value > DateTime.UtcNow;

    [NotMapped]
    public string DisplayStatus => IsIrrigating ? "Irrigating" : (Status == "Irrigating" ? "Online" : Status);
}
