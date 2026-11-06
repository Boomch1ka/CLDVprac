using AgriStream.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasIndex(r => r.Name).IsUnique();

        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Device>().HasIndex(d => d.DeviceId).IsUnique();

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = AppRoles.Farmer },
            new Role { Id = 2, Name = AppRoles.Agronomist },
            new Role { Id = 3, Name = AppRoles.Admin });
    }
}
