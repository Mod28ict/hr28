namespace HR28.Application.Common;

/// <summary>
/// The values an encounter may hold (owner decision, 2026-10-05). Mirror them in the
/// web app's <c>EncounterListFilterModel</c>.
/// </summary>
public static class EncounterValues
{
    public static readonly string[] Types =
        { "Door Visit", "Phone Call", "Meeting", "Campaign Event", "Office Visit", "Other" };

    /// <summary>What the encounter was: a meeting, a call, or a request from the voter.</summary>
    public static readonly string[] Outcomes = { "Meet", "Call", "Request" };

    /// <summary>The voter's response, shown green / yellow / red.</summary>
    public const string Supports = "Supports";
    public const string Undecided = "Undecided";
    public const string DoesNotSupport = "Does not support";

    public static readonly string[] Responses = { Supports, Undecided, DoesNotSupport };
}
