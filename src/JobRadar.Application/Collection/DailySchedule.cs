namespace JobRadar.Application.Collection;

/// <summary>Next occurrence of a local time of day in a time zone, as UTC.</summary>
public static class DailySchedule
{
    public static DateTimeOffset NextAfter(DateTimeOffset now, TimeOnly at, TimeZoneInfo zone)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var day = DateOnly.FromDateTime(localNow.DateTime);

        for (var i = 0; i < 3; i++, day = day.AddDays(1))
        {
            var local = day.ToDateTime(at, DateTimeKind.Unspecified);
            // A local time skipped by a DST jump does not exist; that day runs an hour later.
            if (zone.IsInvalidTime(local))
                local = local.AddHours(1);

            var candidate = new DateTimeOffset(local, zone.GetUtcOffset(local));
            if (candidate > now)
                return candidate.ToUniversalTime();
        }

        throw new InvalidOperationException("Unreachable: a daily time always recurs within two days.");
    }

    /// <summary>The latest occurrence at or before <paramref name="now"/>, as UTC.</summary>
    public static DateTimeOffset LastAtOrBefore(DateTimeOffset now, TimeOnly at, TimeZoneInfo zone)
    {
        // The next occurrence after (now − 2 days) walks forward to the one just before now.
        var candidate = NextAfter(now.AddDays(-2), at, zone);
        while (NextAfter(candidate, at, zone) is var next && next <= now)
            candidate = next;
        return candidate;
    }

    /// <summary>
    /// True when the last scheduled run was missed: nothing started since it. On a PC that was off
    /// (or asleep) at the scheduled time, the pass runs as soon as the worker is back.
    /// </summary>
    public static bool Missed(DateTimeOffset? lastStart, DateTimeOffset now, TimeOnly at, TimeZoneInfo zone) =>
        lastStart is not { } last || last < LastAtOrBefore(now, at, zone);
}
