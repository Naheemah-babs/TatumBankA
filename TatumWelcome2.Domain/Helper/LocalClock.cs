namespace TatumWelcome2.Domain;

/// <summary>
/// Current time in Nigeria (West Africa Time, UTC+1, no daylight saving).
/// Use this for every timestamp a customer can see, so the result does not
/// depend on the time zone of the machine the app runs on.
/// Internal bookkeeping that SQL Server compares with SYSUTCDATETIME()
/// (PIN lockout) must stay in UTC and must not use this.
/// </summary>
public static class LocalClock
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    /// <summary>Lagos wall-clock time (Kind = Unspecified, as stored in datetime2).</summary>
    public static DateTime Now =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    private static TimeZoneInfo ResolveZone()
    {
        // IANA id on Linux/macOS, Windows id on Windows (newer .NET accepts both).
        foreach (string id in new[] { "Africa/Lagos", "W. Central Africa Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        // Nigeria has no daylight saving, so a fixed +01:00 offset is always correct.
        return TimeZoneInfo.CreateCustomTimeZone(
            "WAT", TimeSpan.FromHours(1), "West Africa Time", "WAT");
    }
}
