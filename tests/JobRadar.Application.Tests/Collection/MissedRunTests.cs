using JobRadar.Application.Collection;

namespace JobRadar.Application.Tests.Collection;

public class MissedRunTests
{
    private static readonly TimeZoneInfo Perth = TimeZoneInfo.FindSystemTimeZoneById("Australia/Perth");
    private static readonly TimeOnly SixAm = new(6, 0);

    // 06:00 Perth on the 21st = 22:00 UTC on the 20th.
    private static readonly DateTimeOffset Scheduled = new(2026, 10, 20, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Last_occurrence_is_today_after_the_time_and_yesterday_before_it()
    {
        Assert.Equal(Scheduled, DailySchedule.LastAtOrBefore(Scheduled.AddHours(3), SixAm, Perth));
        Assert.Equal(Scheduled.AddDays(-1), DailySchedule.LastAtOrBefore(Scheduled.AddMinutes(-1), SixAm, Perth));
        Assert.Equal(Scheduled, DailySchedule.LastAtOrBefore(Scheduled, SixAm, Perth));
    }

    [Fact]
    public void Nothing_ever_ran_means_missed()
    {
        Assert.True(DailySchedule.Missed(null, Scheduled.AddHours(1), SixAm, Perth));
    }

    [Fact]
    public void A_pass_started_after_the_last_occurrence_is_not_missed()
    {
        Assert.False(DailySchedule.Missed(Scheduled.AddMinutes(1), Scheduled.AddHours(5), SixAm, Perth));
    }

    [Fact]
    public void The_pc_was_off_at_the_scheduled_time_so_it_is_missed()
    {
        // Last pass yesterday morning; it is now 3 hours after today's scheduled time.
        Assert.True(DailySchedule.Missed(Scheduled.AddDays(-1).AddMinutes(2), Scheduled.AddHours(3), SixAm, Perth));
    }

    [Fact]
    public void Before_today_time_only_yesterday_counts()
    {
        // Yesterday's pass ran; today's time has not come yet.
        Assert.False(DailySchedule.Missed(Scheduled.AddDays(-1).AddMinutes(2), Scheduled.AddMinutes(-30), SixAm, Perth));
    }
}
