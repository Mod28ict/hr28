namespace HR28.Application.Common;

/// <summary>
/// Every right an Administrator can grant to a role or a user. The Administrator
/// (stored as "Super Administrator") always has all of them. Rights only ever apply
/// inside the user's own areas (influencers are global).
/// Add new rights here; the Settings and user screens list them automatically.
/// Mirror the keys in the web app's Hr28Permissions.
/// </summary>
public static class PermissionCatalog
{
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

    public const string PledgesView = "Pledges.View";
    public const string PledgesAdd = "Pledges.Add";
    public const string PledgesEdit = "Pledges.Edit";
    public const string PledgesDelete = "Pledges.Delete";

    public const string InfluencersView = "Influencers.View";
    public const string InfluencersAdd = "Influencers.Add";
    public const string InfluencersLink = "Influencers.Link";
    public const string InfluencersEdit = "Influencers.Edit";
    public const string InfluencersDelete = "Influencers.Delete";

    /// <summary>Columns of the rights grid, in order.</summary>
    public static readonly string[] Actions = { "View", "Add", "Edit", "Delete", "Link" };

    /// <param name="Group">The record type (a row of the rights grid).</param>
    /// <param name="Action">View, Add, Edit, Delete or Link (a column of the grid).</param>
    public record Entry(string Key, string Name, string Description, string Group, string Action);

    public static readonly IReadOnlyList<Entry> All = new[]
    {
        new Entry(VotersView, "View voters",
            "Search and open voters, including their profile.", "Voters", "View"),
        new Entry(VotersAdd, "Add voters",
            "Add new voters.", "Voters", "Add"),
        new Entry(VotersEdit, "Edit voters",
            "Change a voter's details, party and support status.", "Voters", "Edit"),
        new Entry(VotersDelete, "Delete voters",
            "Permanently delete a voter with their encounters and pledges.", "Voters", "Delete"),

        new Entry(VotersPhotoView, "View voter photos",
            "See a voter's photo on their profile.", "Voter photos", "View"),
        new Entry(VotersPhotoEdit, "Add or remove voter photos",
            "Upload, replace or remove a voter's photo.", "Voter photos", "Edit"),

        new Entry(EncountersView, "View encounters",
            "See the Encounters list and the encounter history on a voter's profile.", "Encounters", "View"),
        new Entry(EncountersAdd, "Add encounters",
            "Record a new encounter with a voter.", "Encounters", "Add"),
        new Entry(EncountersEdit, "Edit encounters",
            "Correct the date, type, outcome, response or notes of a recorded encounter.", "Encounters", "Edit"),
        new Entry(EncountersDelete, "Delete encounters",
            "Permanently delete a recorded encounter.", "Encounters", "Delete"),

        new Entry(PledgesView, "View pledges",
            "See the Pledges list and the pledges on a voter's profile.", "Pledges", "View"),
        new Entry(PledgesAdd, "Add pledges",
            "Record a new pledge for a voter.", "Pledges", "Add"),
        new Entry(PledgesEdit, "Edit pledges",
            "Change a pledge's status (for example mark it fulfilled).", "Pledges", "Edit"),
        new Entry(PledgesDelete, "Delete pledges",
            "Permanently delete a pledge.", "Pledges", "Delete"),

        new Entry(InfluencersView, "View influencers",
            "See influencers and who they are linked to.", "Influencers", "View"),
        new Entry(InfluencersAdd, "Add influencers",
            "Add new influencers.", "Influencers", "Add"),
        new Entry(InfluencersEdit, "Edit influencers",
            "Change any influencer's details, category and area (influencers are shared by everyone).", "Influencers", "Edit"),
        new Entry(InfluencersDelete, "Delete influencers",
            "Permanently delete any influencer and all their links to voters.", "Influencers", "Delete"),
        new Entry(InfluencersLink, "Link influencers to voters",
            "Link an influencer to a voter and change the relationship.", "Influencers", "Link")
    };

    public static bool IsKnown(string key) => All.Any(p => p.Key == key);

    public static string NameOf(string key) =>
        All.FirstOrDefault(p => p.Key == key)?.Name ?? key;
}

/// <summary>What opens when someone with the role opens a voter (Settings → Roles &amp; rights).</summary>
public static class VoterProfileViews
{
    /// <summary>The whole profile (each section still needs its view right).</summary>
    public const string Full = "Full";

    /// <summary>Straight to "Add encounter" with a short voter summary.</summary>
    public const string AddEncounter = "AddEncounter";

    public static readonly string[] All = { Full, AddEncounter };

    public static string Label(string view) =>
        view == AddEncounter ? "Add encounter only" : "Full profile";
}
