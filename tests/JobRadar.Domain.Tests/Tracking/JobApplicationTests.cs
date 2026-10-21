using JobRadar.Domain.Listings;
using JobRadar.Domain.Tracking;

namespace JobRadar.Domain.Tests.Tracking;

public class JobApplicationTests
{
    private static readonly ListingId Id = new("seek", "1");
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 9, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void Every_status_change_is_kept_in_order()
    {
        var app = JobApplication.Start(Id, ApplicationStatus.Reviewed, Monday);
        app.ChangeStatus(ApplicationStatus.Applied, Monday.AddDays(1));
        app.ChangeStatus(ApplicationStatus.Interview, Monday.AddDays(5));

        Assert.Equal(ApplicationStatus.Interview, app.Status);
        Assert.Equal(
            [(null, ApplicationStatus.Reviewed), (ApplicationStatus.Reviewed, ApplicationStatus.Applied), (ApplicationStatus.Applied, ApplicationStatus.Interview)],
            app.History.Select(h => (h.From, h.To)));
    }

    [Fact]
    public void Applied_at_is_the_first_time_the_candidate_went_for_it()
    {
        var app = JobApplication.Start(Id, ApplicationStatus.Reviewed, Monday);
        Assert.Null(app.AppliedAt);

        app.ChangeStatus(ApplicationStatus.Applied, Monday.AddDays(1));
        app.ChangeStatus(ApplicationStatus.Interview, Monday.AddDays(5));

        Assert.Equal(Monday.AddDays(1), app.AppliedAt);
    }

    [Fact]
    public void Starting_straight_at_interview_still_records_when_they_applied()
    {
        Assert.Equal(Monday, JobApplication.Start(Id, ApplicationStatus.Interview, Monday).AppliedAt);
    }

    [Fact]
    public void The_same_status_again_changes_nothing()
    {
        var app = JobApplication.Start(Id, ApplicationStatus.Applied, Monday);

        Assert.False(app.ChangeStatus(ApplicationStatus.Applied, Monday.AddDays(1)));
        Assert.Single(app.History);
    }

    [Fact]
    public void Blank_notes_are_no_notes()
    {
        var app = JobApplication.Start(Id, ApplicationStatus.Reviewed, Monday);

        app.SetNotes("  Recruiter: Ana, call back Friday  ");
        Assert.Equal("Recruiter: Ana, call back Friday", app.Notes);
        app.SetNotes("   ");
        Assert.Null(app.Notes);
    }

    [Theory]
    [InlineData(null, ApplicationStatus.Dismissed, Verdict.Bad)]
    [InlineData(null, ApplicationStatus.Applied, Verdict.Good)]
    [InlineData(null, ApplicationStatus.Interview, Verdict.Good)]
    [InlineData(null, ApplicationStatus.Offer, Verdict.Good)]
    [InlineData(null, ApplicationStatus.Declined, Verdict.Good)]
    [InlineData(null, ApplicationStatus.Reviewed, null)]
    [InlineData(null, null, null)]
    [InlineData(Verdict.Bad, ApplicationStatus.Applied, Verdict.Bad)]
    [InlineData(Verdict.Good, ApplicationStatus.Dismissed, Verdict.Good)]
    public void Explicit_label_wins_then_the_status_decides(Verdict? label, ApplicationStatus? status, Verdict? expected)
    {
        Assert.Equal(expected, Judgement.Effective(label, status));
    }
}
