namespace HR28.Web.Services;

/// <summary>
/// Rights granted by the Administrator (to a role or a user), as kept in the
/// session by SessionRoleRefreshFilter. Only used to show or hide buttons; the
/// API checks the same rights on every request.
/// </summary>
public static class Hr28Permissions
{
    public const string SessionKey = "UserPermissions";

    public const string InfluencersEdit = "Influencers.Edit";
    public const string InfluencersDelete = "Influencers.Delete";
    public const string EncountersEdit = "Encounters.Edit";

    public static string ToSession(IEnumerable<string>? permissions) =>
        string.Join('|', (permissions ?? Array.Empty<string>()).Distinct());

    public static bool Has(ISession session, string permission)
    {
        // The Administrator always has every right.
        if (Hr28Roles.IsSuperAdministrator(session.GetString("UserRole")))
            return true;

        return (session.GetString(SessionKey) ?? string.Empty)
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Contains(permission);
    }
}
