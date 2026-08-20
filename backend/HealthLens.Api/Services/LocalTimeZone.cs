namespace HealthLens.Api.Services;

/// <summary>
/// Fitbit/Pixel Watch data for this account is logged in Central European time. "Daily" buckets are
/// grouped by that zone so totals match what the Fitbit app itself shows as a day, and the legacy
/// export's zone-less local timestamps are resolved against it. Falls back to the Windows id when the
/// IANA time zone database isn't available.
/// </summary>
public static class LocalTimeZone
{
    public static TimeZoneInfo Instance { get; } = Resolve();

    public static DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Instance));

    public static DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Instance);

    private static TimeZoneInfo Resolve()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        }
    }
}
