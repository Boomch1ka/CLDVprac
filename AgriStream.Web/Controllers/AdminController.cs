using System.Security.Claims;
using AgriStream.Web.Data;
using AgriStream.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class AdminController(AppDbContext db, IPasswordHasher<User> hasher) : Controller
{
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    public async Task<IActionResult> Users() => View(await BuildVm(new CreateUserViewModel()));

    [HttpPost]
    public async Task<IActionResult> CreateUser([Bind(Prefix = "NewUser")] CreateUserViewModel input)
    {
        if (ModelState.IsValid && await db.Users.AnyAsync(u => u.Username == input.Username.Trim()))
            ModelState.AddModelError("NewUser.Username", "That username is already taken.");
        if (ModelState.IsValid && !await db.Roles.AnyAsync(r => r.Id == input.RoleId))
            ModelState.AddModelError("NewUser.RoleId", "Pick a valid role.");

        if (!ModelState.IsValid) return View(nameof(Users), await BuildVm(input));

        var user = new User { Username = input.Username.Trim(), RoleId = input.RoleId, CreatedAt = DateTime.UtcNow };
        user.PasswordHash = hasher.HashPassword(user, input.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        TempData["Success"] = $"👤 User {user.Username} created.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    public async Task<IActionResult> ChangeRole(int id, int roleId)
    {
        var user = await db.Users.FindAsync(id);
        var role = await db.Roles.FindAsync(roleId);
        if (user is null || role is null) return NotFound();

        if (user.Id == CurrentUserId && role.Name != AppRoles.Admin)
        {
            TempData["Error"] = "You can't remove your own admin role 🙃";
            return RedirectToAction(nameof(Users));
        }

        user.RoleId = role.Id;
        await db.SaveChangesAsync();
        TempData["Success"] = $"{user.Username} is now a {role.Name}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (id == CurrentUserId)
        {
            TempData["Error"] = "You can't delete yourself 🙃";
            return RedirectToAction(nameof(Users));
        }

        var user = await db.Users.FindAsync(id);
        if (user is not null)
        {
            db.Users.Remove(user);
            await db.SaveChangesAsync();
            TempData["Success"] = $"User {user.Username} deleted.";
        }
        return RedirectToAction(nameof(Users));
    }

    private async Task<UsersViewModel> BuildVm(CreateUserViewModel newUser) => new()
    {
        Users = await db.Users.AsNoTracking().Include(u => u.Role).OrderBy(u => u.Username).ToListAsync(),
        Roles = await db.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync(),
        NewUser = newUser
    };
}
