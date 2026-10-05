namespace HR28.Web.Services;

/// <summary>
/// For roles that open voters on "Add encounter only": the things they may add for a
/// voter. Only "Add encounter" → straight to that form; more than one → a page of cards.
/// </summary>
public static class VoterQuickActions
{
    public record Choice(string Key, string Title, string Text, string Icon, string Tone, string Controller, string Action);

    public static readonly Choice Encounter = new("encounter", "Add encounter",
        "Record a meeting, call or request with this voter.", "message", "is-green", "Encounters", "Create");

    public static readonly Choice Pledge = new("pledge", "Add pledge",
        "Record something promised to this voter.", "handshake", "is-amber", "Pledges", "Create");

    public static readonly Choice Influencer = new("influencer", "Link influencer",
        "Link a community leader or trusted contact to this voter.", "star", "is-violet", "Influencers", "Link");

    /// <summary>The choices this person's rights allow, in a fixed order.</summary>
    public static List<Choice> For(ISession session)
    {
        var choices = new List<Choice>();

        if (Hr28Permissions.Has(session, Hr28Permissions.EncountersAdd)) choices.Add(Encounter);
        if (Hr28Permissions.Has(session, Hr28Permissions.PledgesAdd)) choices.Add(Pledge);
        if (Hr28Permissions.Has(session, Hr28Permissions.InfluencersLink)) choices.Add(Influencer);

        return choices;
    }

    /// <summary>True when the only thing they may add is an encounter (opens that form directly).</summary>
    public static bool OnlyEncounter(ISession session) =>
        For(session) is { Count: 1 } list && list[0].Key == Encounter.Key;
}
