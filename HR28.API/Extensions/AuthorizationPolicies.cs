namespace HR28.API.Extensions;

public static class AuthorizationPolicies
{
    /// <summary>Super and National Administrators: full data access and master data.</summary>
    public const string Administrator = "Administrator";

    /// <summary>Super Administrators only: user accounts, roles and scopes.</summary>
    public const string SuperAdministrator = "SuperAdministrator";

    public static IServiceCollection AddHr28AuthorizationPolicies(
        this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Administrator, policy =>
                policy.RequireRole(
                    "Super Administrator",
                    "National Administrator"));

            options.AddPolicy(SuperAdministrator, policy =>
                policy.RequireRole("Super Administrator"));
        });

        return services;
    }
}
