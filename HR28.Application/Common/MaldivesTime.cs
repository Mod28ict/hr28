namespace HR28.Application.Common;

/// <summary>
/// HR28 runs in the Maldives: every date and time shown to users, and every
/// "today" / "now" used in business rules, is Maldives time (UTC+05:00, no
/// daylight saving). Timestamps the system records itself (CreatedAt, LinkedAt,
/// LastLoginAt, FulfilledDate, PledgeDate...) are stored in UTC and converted for
/// display. Dates a person types in (encounter date, due date) are Maldives
/// dates and are stored as typed.
/// The zone is built in code, so it does not depend on the server's time zone
/// (Azure App Service runs on UTC).
/// </summary>
public static class MaldivesTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone(
        "Maldives", TimeSpan.FromHours(5), "Maldives Time (UTC+05:00)", "Maldives Time");

    /// <summary>Current Maldives date and time.</summary>
    public static DateTime Now => FromUtc(DateTime.UtcNow);

    /// <summary>Current Maldives date.</summary>
    public static DateTime Today => Now.Date;

    /// <summary>A UTC timestamp (as stored) in Maldives time.</summary>
    public static DateTime FromUtc(DateTime utc)
    {
        var value = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(value, Zone), DateTimeKind.Unspecified);
    }

    /// <summary>A Maldives date/time (e.g. typed by a user) as UTC for storage or comparison.</summary>
    public static DateTime ToUtc(DateTime maldives) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(maldives, DateTimeKind.Unspecified), Zone);
}
