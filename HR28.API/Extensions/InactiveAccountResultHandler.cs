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
    private readonly ILogger<InactiveAccountResultHandler> _logger;

    public InactiveAccountResultHandler(ILogger<InactiveAccountResultHandler> logger) => _logger = logger;

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

        // A signed-in person asked for something their rights don't allow (ASVS review M2).
        if (authorizeResult.Forbidden)
            SecurityLog.Refused(_logger, context, "missing right or role");

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
