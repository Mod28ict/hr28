namespace HR28.Web.Services;

/// <summary>
/// Rights granted by the Administrator (to a role or a user), as kept in the
/// session by SessionRoleRefreshFilter. Only used to show or hide menus and buttons;
/// the API checks the same rights on every request. Mirrors the API's PermissionCatalog.
/// </summary>
public static class Hr28Permissions
{
    public const string SessionKey = "UserPermissions";

    /// <summary>"Full" or "AddEncounter": what opens when the user opens a voter.</summary>
    public const string ProfileViewSessionKey = "VoterProfileView";

    /// <summary>"Dashboard" or "QuickEntry": where the user lands after signing in.</summary>
    public const string StartPageSessionKey = "StartPage";

    /// <summary>Where voter searches run: "Areas", "All" (administrator without areas) or "None".</summary>
    public const string SearchAreaSessionKey = "SearchArea";

    public const string VotersView = "Voters.View";
    public const string VotersAdd = "Voters.Add";
    public const string VotersEdit = "Voters.Edit";
    public const string VotersDelete = "Voters.Delete";

    public const string VotersPhotoView = "Voters.Photo.View";
    public const string VotersPhotoEdit = "Voters.Photo.Edit";

    public const string EncountersView = "Encounters.View";
    public const string EncountersAdd = "Encounters.Add";
    public const string EncountersEdit = "Encounters.Edit";
    public const string EncountersDelete = "Encounters.Delete";
    public const string EncountersResponse = "Encounters.Response";

    public const string PledgesView = "Pledges.View";
    public const string PledgesAdd = "Pledges.Add";
    public const string PledgesEdit = "Pledges.Edit";
    public const string PledgesDelete = "Pledges.Delete";

    public const string InfluencersView = "Influencers.View";
    public const string InfluencersAdd = "Influencers.Add";
    public const string InfluencersLink = "Influencers.Link";
    public const string InfluencersEdit = "Influencers.Edit";
    public const string InfluencersDelete = "Influencers.Delete";

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

    /// <summary>True when the user's roles skip the Dashboard and start on Quick entry.</summary>
    public static bool StartsOnQuickEntry(ISession session) =>
        !Hr28Roles.IsSuperAdministrator(session.GetString("UserRole")) &&
        session.GetString(StartPageSessionKey) == "QuickEntry" &&
        CanUseQuickEntry(session);

    /// <summary>Quick entry needs "View voters" and at least one thing to add for a voter.</summary>
    public static bool CanUseQuickEntry(ISession session) =>
        Has(session, VotersView) &&
        (Has(session, EncountersAdd) || Has(session, PledgesAdd) || Has(session, InfluencersLink));

    /// <summary>No areas and not an administrator: there are no voters to search.</summary>
    public static bool HasNoSearchArea(ISession session) =>
        session.GetString(SearchAreaSessionKey) == "None";

    /// <summary>"in your areas" or, for an administrator without areas, "in the registry".</summary>
    public static string SearchAreaText(ISession session) =>
        session.GetString(SearchAreaSessionKey) == "All" ? "in the registry" : "in your areas";

    /// <summary>True when the user's roles open voters straight on "Add encounter".</summary>
    public static bool OpensVotersOnAddEncounter(ISession session) =>
        !Hr28Roles.IsSuperAdministrator(session.GetString("UserRole")) &&
        session.GetString(ProfileViewSessionKey) == "AddEncounter";
}
