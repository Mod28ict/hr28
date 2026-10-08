using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HR28.Web.Filters;

/// <summary>
/// On every signed-in page: if the sign-in token is close to running out, quietly get a
/// fresh one, so people who are working are never signed out mid-task. If the API refuses
/// (account switched off, or more than 12 hours since sign-in) the session ends and the
/// sign-in page says so.
/// </summary>
public class SessionTokenRefreshFilter : IAsyncActionFilter
{
    private readonly SessionKeeper _keeper;

    public SessionTokenRefreshFilter(SessionKeeper keeper)
    {
        _keeper = keeper;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;

        var isSignInPage = string.Equals(
            context.RouteData.Values["controller"]?.ToString(), "Auth",
            StringComparison.OrdinalIgnoreCase);

        if (!isSignInPage &&
            !string.IsNullOrWhiteSpace(http.Session.GetString("JwtToken")) &&
            !await _keeper.RefreshAsync(http.Session, force: false))
        {
            http.Session.Clear();
            context.Result = new RedirectToActionResult("Login", "Auth", null);
            return;
        }

        await next();
    }
}
