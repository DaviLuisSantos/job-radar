using JobRadar.Domain.Collection;

namespace JobRadar.Application.Abstractions;

/// <summary>
/// The ATS job boards known to the system and the state of their last sweeps (today in the <c>ats_boards</c> table).
/// Always in batches: a sweep touches thousands of boards.
/// </summary>
public interface IAtsBoardRepository
{
    /// <summary>
    /// The boards of <paramref name="platform"/> worth asking now, at most <paramref name="limit"/>: never swept first,
    /// then the ones swept longest ago. Boards found gone <paramref name="maxFailures"/> times in a row are left out,
    /// and so are boards that never had a listing in our markets unless they were last swept before
    /// <paramref name="idleBefore"/>.
    /// </summary>
    Task<IReadOnlyList<AtsBoard>> ListToSweepAsync(
        string platform, int maxFailures, DateTimeOffset idleBefore, int limit, CancellationToken ct = default);

    /// <summary>Adds the boards not yet known (same platform and slug are the same board); returns how many were added.</summary>
    Task<int> AddMissingAsync(IReadOnlyCollection<AtsBoard> boards, CancellationToken ct = default);

    /// <summary>
    /// Stores what a sweep learned. A 200 replaces the tag and the counts; a 304 keeps them; both reset the failures.
    /// A 404 adds one failure. No answer or a server error leaves the board as it was,
    /// so a platform outage does not retire its boards.
    /// </summary>
    Task RecordSweepAsync(IReadOnlyCollection<AtsBoardSweepOutcome> outcomes, CancellationToken ct = default);
}
