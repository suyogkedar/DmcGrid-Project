using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace DmcGrid.Web.Controllers;

public abstract class AppController : Controller
{
    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected JsonResult Done(string message, object? data = null) => Json(new { ok = true, message, data });

    protected JsonResult Fail(string message) => Json(new { ok = false, message });
}
