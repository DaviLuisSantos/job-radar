using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Tracking;

/// <summary>The candidate's verdict on a listing, used to check the score against their judgement.</summary>
public enum Verdict
{
    Good,
    Bad,
}

public sealed record ListingLabel(ListingId ListingId, Verdict Verdict, string? Note, DateTimeOffset LabeledAt);

public static class Judgement
{
    /// <summary>
    /// The verdict that counts: an explicit label wins; otherwise going for a listing (applying,
    /// even if the employer declined) makes it good and dismissing it makes it bad.
    /// </summary>
    public static Verdict? Effective(Verdict? label, ApplicationStatus? status) => label ?? status switch
    {
        ApplicationStatus.Dismissed => Verdict.Bad,
        { } s when JobApplication.WentFor(s) => Verdict.Good,
        _ => null,
    };
}
