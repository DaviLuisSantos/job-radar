using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Tracking;

/// <summary>Where the candidate stands with a listing. A listing never touched has no application.</summary>
public enum ApplicationStatus
{
    /// <summary>Looked at, no decision yet.</summary>
    Reviewed,
    /// <summary>The candidate decided not to apply.</summary>
    Dismissed,
    Applied,
    Interview,
    Offer,
    /// <summary>The employer said no. Not the same as <see cref="Dismissed"/>: the candidate still wanted it.</summary>
    Declined,
}

public sealed record StatusChange(ApplicationStatus? From, ApplicationStatus To, DateTimeOffset At);

/// <summary>
/// The candidate's track record with one listing: current status, when they applied, notes, and
/// every status change in order.
/// </summary>
public sealed class JobApplication
{
    private readonly List<StatusChange> _history;

    private JobApplication(ListingId listingId, ApplicationStatus status, DateTimeOffset? appliedAt, string? notes, List<StatusChange> history)
    {
        ListingId = listingId;
        Status = status;
        AppliedAt = appliedAt;
        Notes = notes;
        _history = history;
    }

    public ListingId ListingId { get; }
    public ApplicationStatus Status { get; private set; }

    /// <summary>First time the status reached <see cref="ApplicationStatus.Applied"/> or beyond.</summary>
    public DateTimeOffset? AppliedAt { get; private set; }

    public string? Notes { get; private set; }
    public IReadOnlyList<StatusChange> History => _history;

    public static JobApplication Start(ListingId listingId, ApplicationStatus status, DateTimeOffset at)
    {
        var application = new JobApplication(listingId, status, null, null, [new StatusChange(null, status, at)]);
        application.StampApplied(at);
        return application;
    }

    /// <summary>Rebuilds a stored application; no rules run.</summary>
    public static JobApplication Restore(
        ListingId listingId, ApplicationStatus status, DateTimeOffset? appliedAt, string? notes, IEnumerable<StatusChange> history) =>
        new(listingId, status, appliedAt, notes, history.ToList());

    /// <summary>Moves to <paramref name="status"/>. Same status again is a no-op, not a history entry.</summary>
    public bool ChangeStatus(ApplicationStatus status, DateTimeOffset at)
    {
        if (status == Status)
            return false;
        _history.Add(new StatusChange(Status, status, at));
        Status = status;
        StampApplied(at);
        return true;
    }

    public void SetNotes(string? notes) => Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    /// <summary>Applied, interviewing, offered or declined after applying: the candidate went for it.</summary>
    public static bool WentFor(ApplicationStatus status) =>
        status is ApplicationStatus.Applied or ApplicationStatus.Interview or ApplicationStatus.Offer or ApplicationStatus.Declined;

    private void StampApplied(DateTimeOffset at)
    {
        if (AppliedAt is null && WentFor(Status))
            AppliedAt = at;
    }
}
