using HR28.Web.Filters;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

/// <summary>
/// Shared helpers for signed-in pages: token, role and consistent
/// handling of expired sessions and permission errors from the API.
/// </summary>
[SessionAuthorize]
public abstract class AppController : Controller
{
    protected string? Token => HttpContext.Session.GetString("JwtToken");

    protected string? Role => HttpContext.Session.GetString("UserRole");

    protected bool IsAdministrator => Hr28Roles.IsAdministrator(Role);

    /// <summary>
    /// Returns a redirect for session/permission failures, or null when the
    /// caller should show <see cref="ApiResult{T}.Message"/> on the page.
    /// </summary>
    protected IActionResult? HandleApiFailure<T>(ApiResult<T> result)
    {
        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (result.IsForbidden)
        {
            // Shown once by the layout on the dashboard.
            TempData["FlashError"] = string.IsNullOrWhiteSpace(result.Message)
                ? "You don't have permission to do that."
                : result.Message;
            return RedirectToAction("Index", "Dashboard");
        }

        return null;
    }
}
