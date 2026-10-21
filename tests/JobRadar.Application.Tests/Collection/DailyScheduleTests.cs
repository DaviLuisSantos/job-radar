using JobRadar.Application.Collection;

namespace JobRadar.Application.Tests.Collection;

public class DailyScheduleTests
{
    private static readonly TimeZoneInfo Perth = TimeZoneInfo.FindSystemTimeZoneById("Australia/Perth");
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
    private static readonly TimeOnly SixAm = new(6, 0);

    [Fact]
    public void Before_the_time_today_runs_today()
    {
        // 05:00 in Perth (UTC+8) = 21:00 UTC the day before.
        var now = new DateTimeOffset(2026, 10, 20, 21, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 10, 20, 22, 0, 0, TimeSpan.Zero), DailySchedule.NextAfter(now, SixAm, Perth));
    }

    [Fact]
    public void After_the_time_today_runs_tomorrow()
    {
        var now = new DateTimeOffset(2026, 10, 21, 1, 0, 0, TimeSpan.Zero); // 09:00 Perth

        Assert.Equal(new DateTimeOffset(2026, 10, 21, 22, 0, 0, TimeSpan.Zero), DailySchedule.NextAfter(now, SixAm, Perth));
    }

    [Fact]
    public void Exactly_at_the_time_runs_tomorrow_so_a_pass_never_repeats()
    {
        var now = new DateTimeOffset(2026, 10, 20, 22, 0, 0, TimeSpan.Zero); // 06:00 Perth

        Assert.Equal(now.AddDays(1), DailySchedule.NextAfter(now, SixAm, Perth));
    }

    [Fact]
    public void Follows_daylight_saving_in_zones_that_have_it()
    {
        // Sydney moves to UTC+11 on 2026-10-04: 06:00 local is 19:00 UTC the day before.
        var now = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 10, 10, 19, 0, 0, TimeSpan.Zero), DailySchedule.NextAfter(now, SixAm, Sydney));
    }

    [Fact]
    public void A_time_skipped_by_daylight_saving_runs_an_hour_later_that_day()
    {
        // 2026-10-04 02:00 does not exist in Sydney (clocks jump to 03:00, UTC+11).
        var now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 10, 3, 16, 0, 0, TimeSpan.Zero), DailySchedule.NextAfter(now, new TimeOnly(2, 0), Sydney));
    }
}
