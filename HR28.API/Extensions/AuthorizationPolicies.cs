using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace HR28.API.Extensions;

/// <summary>
/// Authorization is decided from the database on every request (via the cached
/// IAccessScopeService), not from roles frozen in the login token. Removing a
/// role or deactivating a user takes effect immediately.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Super and National Administrators: full data access and master data.</summary>
    public const string Administrator = "Administrator";

    /// <summary>The client's Administrator (stored as "Super Administrator"): users, roles and scopes.</summary>
    public const string SuperAdministrator = "SuperAdministrator";

    // Records (voters, encounters, pledges, influencers) are protected by named rights
    // instead: [RequirePermission(PermissionCatalog.X)] (see PermissionAuthorization.cs).

    public static IServiceCollection AddHr28AuthorizationPolicies(
        this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<IAuthorizationHandler, AccessRequirementHandler>();
        services.AddScoped<IAuthorizationHandler, PermissionRequirementHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<
            Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler,
            InactiveAccountResultHandler>();

        services.AddAuthorization(options =>
        {
            // Plain [Authorize] also requires an active account.
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new AccessRequirement(AccessLevel.ActiveUser))
                .Build();

            options.AddPolicy(Administrator, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new AccessRequirement(AccessLevel.Administrator)));

            options.AddPolicy(SuperAdministrator, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new AccessRequirement(AccessLevel.SuperAdministrator)));

        });

        return services;
    }
}

public enum AccessLevel
{
    ActiveUser,
    Administrator,
    SuperAdministrator
}

public class AccessRequirement : IAuthorizationRequirement
{
    public AccessRequirement(AccessLevel level) => Level = level;

    public AccessLevel Level { get; }
}

public class AccessRequirementHandler : AuthorizationHandler<AccessRequirement>
{
    private readonly IAccessScopeService _accessScopeService;

    public AccessRequirementHandler(IAccessScopeService accessScopeService)
    {
        _accessScopeService = accessScopeService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AccessRequirement requirement)
    {
        if (context.User.GetUserId() is not Guid userId)
            return;

        var access = await _accessScopeService.GetAsync(userId);

        if (!access.IsActive)
        {
            // Deactivated: fail outright so no other handler can grant access.
            context.Fail(new AuthorizationFailureReason(this, "Account is not active."));
            return;
        }

        var allowed = requirement.Level switch
        {
            AccessLevel.ActiveUser => true,
            AccessLevel.Administrator => access.IsAdministrator,
            AccessLevel.SuperAdministrator => access.IsSuperAdministrator,
            _ => false
        };

        if (allowed)
            context.Succeed(requirement);
    }
}
