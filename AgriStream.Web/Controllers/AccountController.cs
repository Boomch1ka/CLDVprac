using System.Security.Claims;
using AgriStream.Web.Data;
using AgriStream.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

public class AccountController(AppDbContext db, IPasswordHasher<User> hasher) : Controller
{
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Dashboard");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == model.Username.Trim());

        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Hmm, that username or password doesn't look right.");
            return View(model);
        }

        await SignInAsync(user, model.RememberMe);
        TempData["Success"] = $"Welcome back, {user.Username}! 🌞";

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Dashboard");
        return View(new RegisterViewModel());
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!AppRoles.SelfRegister.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Please pick Farmer or Agronomist.");

        if (ModelState.IsValid && await db.Users.AnyAsync(u => u.Username == model.Username.Trim()))
            ModelState.AddModelError(nameof(model.Username), "That username is already taken.");

        if (!ModelState.IsValid) return View(model);

        var role = await db.Roles.FirstAsync(r => r.Name == model.Role);
        var user = new User { Username = model.Username.Trim(), RoleId = role.Id, Role = role, CreatedAt = DateTime.UtcNow };
        user.PasswordHash = hasher.HashPassword(user, model.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await SignInAsync(user, false);
        TempData["Success"] = $"Account created - welcome to AgriStream, {user.Username}! 🎉";
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "You've been signed out. See you soon! 👋";
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private async Task SignInAsync(User user, bool persistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role!.Name)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = persistent });
    }
}
