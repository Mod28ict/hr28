namespace HR28.Web.Services;

/// <summary>
/// Maldives time (UTC+05:00, no daylight saving) for everything shown on screen,
/// whatever time zone the web server runs in. System timestamps from the API
/// (audit entries, sign-ins, links, completed dates, pledge dates) arrive in UTC
/// and are converted with <see cref="FromUtc(DateTime)"/>. Dates people type in
/// (encounter date, due date) are already Maldives dates and are shown as they are.
/// Mirrors HR28.Application.Common.MaldivesTime.
/// </summary>
public static class Hr28Time
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone(
        "Maldives", TimeSpan.FromHours(5), "Maldives Time (UTC+05:00)", "Maldives Time");

    public static DateTime Now => FromUtc(DateTime.UtcNow);

    public static DateTime Today => Now.Date;

    public static DateTime FromUtc(DateTime utc)
    {
        var value = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(value, Zone), DateTimeKind.Unspecified);
    }

    public static DateTime? FromUtc(DateTime? utc) => utc.HasValue ? FromUtc(utc.Value) : null;
}
