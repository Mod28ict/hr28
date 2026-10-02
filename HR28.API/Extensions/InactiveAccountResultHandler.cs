using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace HR28.API.Extensions;

/// <summary>
/// A deactivated account is answered with 401 (signed out) rather than 403
/// (not allowed), so the web app ends the session and returns to sign-in.
/// </summary>
public class InactiveAccountResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var inactive = authorizeResult.Forbidden &&
            authorizeResult.AuthorizationFailure?.FailureReasons
                .Any(r => r.Handler is AccessRequirementHandler) == true;

        if (inactive)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                new { message = "Your account is no longer active. Please contact your administrator." });
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
