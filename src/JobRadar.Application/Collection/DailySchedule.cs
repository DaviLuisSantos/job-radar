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
}
