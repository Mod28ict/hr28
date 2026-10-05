namespace HR28.Web.Services;

/// <summary>
/// Role checks for showing or hiding UI. The API enforces the same rules;
/// these only keep users from seeing options they cannot use.
///
/// A user can have several roles. The session stores them all, separated by
/// "|", and permissions are the combination of every role the user has.
/// </summary>
public static class Hr28Roles
{
    public const string SuperAdministrator = "Super Administrator";
    public const string NationalAdministrator = "National Administrator";
    public const string Reporter = "Reporter";

    private const char Separator = '|';

    /// <summary>Order used to show the most senior role first.</summary>
    private static readonly string[] ByAuthority =
    {
        SuperAdministrator,
        NationalAdministrator,
        "Constituency Administrator",
        "Island Administrator",
        "Collector",
        Reporter
    };

    /// <summary>The value to keep in the session for a list of roles.</summary>
    public static string ToSession(IEnumerable<string>? roles) =>
        string.Join(Separator, Sort(roles ?? Array.Empty<string>()));

    /// <summary>All roles from the session value (also accepts a single role name).</summary>
    public static List<string> Parse(string? sessionValue) =>
        Sort((sessionValue ?? string.Empty)
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>Administrators see every record regardless of scope.</summary>
    public static bool IsAdministrator(string? roles) =>
        Parse(roles).Any(r => r == SuperAdministrator || r == NationalAdministrator);

    public static bool IsSuperAdministrator(string? roles) =>
        Parse(roles).Contains(SuperAdministrator);

    /// <summary>
    /// The name shown on screen. "Super Administrator" is the client's own
    /// Administrator (the platform Owner works in Azure, not in the app);
    /// the stored name is kept so existing data and checks keep working.
    /// </summary>
    public static string DisplayName(string? role) =>
        string.IsNullOrWhiteSpace(role)
            ? "No role"
            : role == SuperAdministrator ? "Administrator" : role;

    /// <summary>Short label for tight spaces, e.g. "Administrator +1".</summary>
    public static string Summary(string? roles)
    {
        var list = Parse(roles);

        return list.Count switch
        {
            0 => "No role",
            1 => DisplayName(list[0]),
            _ => $"{DisplayName(list[0])} +{list.Count - 1}"
        };
    }

    private static List<string> Sort(IEnumerable<string> roles) =>
        roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct()
            .OrderBy(r => Array.IndexOf(ByAuthority, r) is var i && i < 0 ? ByAuthority.Length : i)
            .ThenBy(r => r)
            .ToList();
}
