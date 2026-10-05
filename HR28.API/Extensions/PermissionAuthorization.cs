using HR28.Application.Common;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace HR28.API.Extensions;

/// <summary>
/// Requires one of the named rights (PermissionCatalog), resolved live from the
/// database like every other policy. Several rights mean "any of these".
/// Usage: [RequirePermission(PermissionCatalog.VotersAdd)].
/// </summary>
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public RequirePermissionAttribute(params string[] anyOf)
    {
        Policy = PolicyPrefix + string.Join('|', anyOf);
    }
}

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(IReadOnlyList<string> anyOf) => AnyOf = anyOf;

    public IReadOnlyList<string> AnyOf { get; }
}

public class PermissionRequirementHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IAccessScopeService _accessScopeService;

    public PermissionRequirementHandler(IAccessScopeService accessScopeService)
    {
        _accessScopeService = accessScopeService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.GetUserId() is not Guid userId)
            return;

        var access = await _accessScopeService.GetAsync(userId);

        if (!access.IsActive)
        {
            context.Fail(new AuthorizationFailureReason(this, "Account is not active."));
            return;
        }

        if (requirement.AnyOf.Any(access.HasPermission))
            context.Succeed(requirement);
    }
}

/// <summary>
/// Builds "Permission:..." policies on demand; every other policy name goes to the
/// normal provider. A misspelt right throws instead of quietly allowing or denying.
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
            return _fallback.GetPolicyAsync(policyName);

        var rights = policyName[RequirePermissionAttribute.PolicyPrefix.Length..]
            .Split('|', StringSplitOptions.RemoveEmptyEntries);

        if (rights.Length == 0 || rights.Any(r => !PermissionCatalog.IsKnown(r)))
            throw new InvalidOperationException($"Unknown right in policy \"{policyName}\".");

        return Task.FromResult<AuthorizationPolicy?>(
            new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(rights))
                .Build());
    }
}
