using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HR28.Web.Filters;

/// <summary>
/// Keeps the role in the session in step with the server, so menus change
/// within a minute of an administrator changing someone's role. If the API
/// says the account is no longer active, the session ends.
/// The API itself always checks roles live; this only affects what is shown.
/// </summary>
public class SessionRoleRefreshFilter : IAsyncActionFilter
{
    private const string CheckedAtKey = "RoleCheckedAt";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly ApiClient _apiClient;

    public SessionRoleRefreshFilter(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var session = context.HttpContext.Session;
        var token = session.GetString("JwtToken");

        var isSignInPage = string.Equals(
            context.RouteData.Values["controller"]?.ToString(), "Auth",
            StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(token) || isSignInPage || !IsDue(session.GetString(CheckedAtKey)))
        {
            await next();
            return;
        }

        var account = await _apiClient.GetAsync<MyAccountDto>("Settings/me", token);

        if (account.IsUnauthorized)
        {
            session.Clear();
            context.Result = new RedirectToActionResult("Login", "Auth", null);
            return;
        }

        if (account.Success && account.Data != null)
        {
            session.SetString(
                "UserRole",
                Hr28Roles.ToSession(
                    account.Data.Roles.Count > 0
                        ? account.Data.Roles
                        : new[] { account.Data.RoleName }));
            session.SetString("UserName", account.Data.FullName ?? string.Empty);
            session.SetString(
                Hr28Permissions.SessionKey,
                Hr28Permissions.ToSession(account.Data.Permissions));
            session.SetString(
                Hr28Permissions.ProfileViewSessionKey,
                account.Data.VoterProfileView ?? "Full");
            session.SetString(
                Hr28Permissions.StartPageSessionKey,
                account.Data.StartPage ?? "Dashboard");
            session.SetString(
                Hr28Permissions.SearchAreaSessionKey,
                account.Data.SearchArea ?? "None");
        }

        // The campaign name (Settings → System) appears in the sidebar; keep it current too.
        var settings = await _apiClient.GetAsync<SystemSettingsDto>("Settings/system", token);

        if (settings.Success && !string.IsNullOrWhiteSpace(settings.Data?.CampaignName))
            session.SetString("CampaignName", settings.Data.CampaignName);

        // Record the check even if the API was briefly unavailable, to avoid hammering it.
        session.SetString(CheckedAtKey, DateTime.UtcNow.Ticks.ToString());

        await next();
    }

    private static bool IsDue(string? lastCheckedTicks) =>
        !long.TryParse(lastCheckedTicks, out var ticks) ||
        DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) >= Interval;
}
