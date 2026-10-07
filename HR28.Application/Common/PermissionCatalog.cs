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

    /// <summary>Set or change a voter's support status (Supporter / Undecided / Opponent / Neutral).</summary>
    public const string VotersStatus = "Voters.Status";

    public const string VotersPhotoView = "Voters.Photo.View";
    public const string VotersPhotoEdit = "Voters.Photo.Edit";

    public const string EncountersView = "Encounters.View";
    public const string EncountersAdd = "Encounters.Add";
    public const string EncountersEdit = "Encounters.Edit";
    public const string EncountersDelete = "Encounters.Delete";

    /// <summary>Set or change an encounter's response (supports / undecided / does not support).</summary>
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

    /// <summary>Open the Reports pages (limited to the user's areas).</summary>
    public const string ReportsView = "Reports.View";

    /// <summary>Download reports as CSV and print them (every download is audited).</summary>
    public const string ReportsDownload = "Reports.Download";

    /// <summary>Columns of the rights grid, in order.</summary>
    public static readonly string[] Actions = { "View", "Add", "Edit", "Delete", "Link", "Response", "Status", "Download" };

    /// <param name="Group">The record type (a row of the rights grid).</param>
    /// <param name="Action">View, Add, Edit, Delete, Link, Response, Status or Download (a column of the grid).</param>
    public record Entry(string Key, string Name, string Description, string Group, string Action);

    public static readonly IReadOnlyList<Entry> All = new[]
    {
        new Entry(VotersView, "View voters",
            "Search the Voters list, open a voter's profile and look a voter up by ID card on Quick entry.", "Voters", "View"),
        new Entry(VotersAdd, "Add voters",
            "Add new voters.", "Voters", "Add"),
        new Entry(VotersEdit, "Edit voters",
            "Change a voter's name, ID card, mobile, address, gender, date of birth, party and remarks. Moving a voter to another constituency or island is for administrators only.", "Voters", "Edit"),
        new Entry(VotersDelete, "Delete voters",
            "Permanently delete a voter with their encounters and pledges. This cannot be undone.", "Voters", "Delete"),
        new Entry(VotersStatus, "Change support status",
            "Set or change a voter's support status (Supporter, Undecided, Opponent, Neutral): click the status on the Voters list, Quick entry or the profile, or choose it on the Add and Edit forms. Without it, new voters start as Undecided and the status stays as it is.", "Voters", "Status"),

        new Entry(VotersPhotoView, "View voter photos",
            "See voters' photos on the Voters list, the profile and Quick entry.", "Voter photos", "View"),
        new Entry(VotersPhotoEdit, "Add or remove voter photos",
            "Add a photo to a voter (JPG or PNG, up to 2 MB) or remove it.", "Voter photos", "Edit"),

        new Entry(EncountersView, "View encounters",
            "See the Encounters list and the encounter history on a voter's profile.", "Encounters", "View"),
        new Entry(EncountersAdd, "Add encounters",
            "Record a new encounter (meet, call or request) with a voter, including on Quick entry.", "Encounters", "Add"),
        new Entry(EncountersEdit, "Edit encounters",
            "Correct the date, type, outcome or notes of a recorded encounter.", "Encounters", "Edit"),
        new Entry(EncountersDelete, "Delete encounters",
            "Permanently delete a recorded encounter.", "Encounters", "Delete"),
        new Entry(EncountersResponse, "Set encounter response",
            "Record or change whether the voter supports, is undecided or does not support (green / yellow / red). Without it, encounters are saved without a response.", "Encounters", "Response"),

        new Entry(PledgesView, "View pledges",
            "See the Pledges list and the pledges on a voter's profile.", "Pledges", "View"),
        new Entry(PledgesAdd, "Add pledges",
            "Record a new pledge for a voter, including on Quick entry.", "Pledges", "Add"),
        new Entry(PledgesEdit, "Edit pledges",
            "Change a pledge's status, for example mark it fulfilled.", "Pledges", "Edit"),
        new Entry(PledgesDelete, "Delete pledges",
            "Permanently delete a pledge.", "Pledges", "Delete"),

        new Entry(InfluencersView, "View influencers",
            "See the Influencers list and which voters each influencer is linked to (only voters in the person's areas are listed).", "Influencers", "View"),
        new Entry(InfluencersAdd, "Add influencers",
            "Add new influencers.", "Influencers", "Add"),
        new Entry(InfluencersEdit, "Edit influencers",
            "Change any influencer's details, category and area. Influencers are shared by everyone.", "Influencers", "Edit"),
        new Entry(InfluencersDelete, "Delete influencers",
            "Permanently delete any influencer and all their links to voters.", "Influencers", "Delete"),
        new Entry(InfluencersLink, "Link influencers to voters",
            "Link an influencer to a voter and change the relationship, including on Quick entry.", "Influencers", "Link"),

        new Entry(ReportsView, "View reports",
            "Open the Reports pages: constituency summary, pledges and top influencers. Figures cover only the person's areas.", "Reports", "View"),
        new Entry(ReportsDownload, "Download and print reports",
            "Download reports as CSV files and print them. Every download is recorded in the audit trail.", "Reports", "Download")
    };

    public static bool IsKnown(string key) => All.Any(p => p.Key == key);

    public static string NameOf(string key) =>
        All.FirstOrDefault(p => p.Key == key)?.Name ?? key;
}

/// <summary>Where someone with the role lands after signing in (Settings → Roles &amp; rights).</summary>
public static class StartPages
{
    public const string Dashboard = "Dashboard";

    /// <summary>Find a voter by ID card and add an encounter / pledge / influencer link on one page.</summary>
    public const string QuickEntry = "QuickEntry";

    public static readonly string[] All = { Dashboard, QuickEntry };

    public static string Label(string page) =>
        page == QuickEntry ? "Quick entry" : "Dashboard";
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
