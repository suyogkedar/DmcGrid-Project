using System.Security.Claims;
using DmcGrid.Core.Interfaces;
using DmcGrid.Core.Models;
using DmcGrid.Core.Services;
using DmcGrid.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DmcGrid.Web.Controllers;

public class AccountController(IUserRepository users, IAuditRepository audit, IWebHostEnvironment env) : AppController
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToWorkspace(User.FindFirstValue(ClaimTypes.Role));
        return View(new LoginViewModel { ReturnUrl = returnUrl, ShowDemoAccounts = env.IsDevelopment() });
    }

    [HttpPost]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(string portal, string email, string password, string? returnUrl = null)
    {
        var role = portal == "dmc" ? Roles.Dmc : Roles.Agent;
        var user = await users.GetByEmailAsync(email ?? "");
        if (user is null || !user.IsActive || user.Role != role || !PasswordHasher.Verify(password ?? "", user.PasswordHash))
        {
            await audit.LogAsync(user?.UserId, "LoginFailed", "User", user?.UserId, $"portal={portal}");
            return View(new LoginViewModel
            {
                Portal = portal, Email = email ?? "", Message = "Invalid email or password", ReturnUrl = returnUrl,
                ShowDemoAccounts = env.IsDevelopment()
            });
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("org", user.OrgName)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));
        await audit.LogAsync(user.UserId, "Login", "User", user.UserId);

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToWorkspace(user.Role);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    private RedirectToActionResult RedirectToWorkspace(string? role) =>
        RedirectToAction("Index", role == Roles.Dmc ? "Dmc" : "Agent");
}
