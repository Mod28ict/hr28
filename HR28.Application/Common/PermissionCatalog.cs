namespace HR28.Application.Common;

/// <summary>
/// Every right an Administrator can grant to a role or a user. The Administrator
/// (stored as "Super Administrator") always has all of them.
/// Add new rights here; the Settings and user screens list them automatically.
/// </summary>
public static class PermissionCatalog
{
    public const string InfluencersEdit = "Influencers.Edit";
    public const string InfluencersDelete = "Influencers.Delete";
    public const string EncountersEdit = "Encounters.Edit";

    public record Entry(string Key, string Name, string Description, string Group);

    public static readonly IReadOnlyList<Entry> All = new[]
    {
        new Entry(
            InfluencersEdit,
            "Edit influencers",
            "Change an influencer's details and area (within the user's own areas).",
            "Influencers"),
        new Entry(
            InfluencersDelete,
            "Delete influencers",
            "Permanently delete an influencer and their links to voters (within the user's own areas).",
            "Influencers"),
        new Entry(
            EncountersEdit,
            "Edit encounters",
            "Correct the date, type, outcome or notes of a recorded encounter (for voters in the user's own areas).",
            "Encounters")
    };

    public static bool IsKnown(string key) => All.Any(p => p.Key == key);

    public static string NameOf(string key) =>
        All.FirstOrDefault(p => p.Key == key)?.Name ?? key;
}
