namespace HR28.Web.Services;

/// <summary>
/// Role checks for showing or hiding UI. The API enforces the same rules;
/// these only keep users from seeing options they cannot use.
/// </summary>
public static class Hr28Roles
{
    public const string SuperAdministrator = "Super Administrator";
    public const string NationalAdministrator = "National Administrator";
    public const string Reporter = "Reporter";

    /// <summary>Administrators see every record regardless of scope.</summary>
    public static bool IsAdministrator(string? role) =>
        role == SuperAdministrator || role == NationalAdministrator;

    public static bool IsSuperAdministrator(string? role) =>
        role == SuperAdministrator;

    /// <summary>
    /// The name shown on screen. "Super Administrator" is the client's own
    /// Administrator (the platform Owner works in Azure, not in the app);
    /// the stored name is kept so existing data and checks keep working.
    /// </summary>
    public static string DisplayName(string? role) =>
        string.IsNullOrWhiteSpace(role)
            ? "No role"
            : role == SuperAdministrator ? "Administrator" : role;

    /// <summary>Reporters only read reports; they do not work with voter records.</summary>
    public static bool CanManageRecords(string? role) =>
        role != Reporter;
}
