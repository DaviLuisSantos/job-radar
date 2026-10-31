using JobRadar.Domain.Listings;

namespace JobRadar.Integrations.Ats;

/// <summary>A listing the sweep found in one of our markets, with the JSON the board sent for it.</summary>
internal sealed record AtsSnapshotJob(Listing Listing, JobMarket Market, string RawPayload);

/// <summary>
/// What the last sweep of this process found, for the searches to filter. The sweep is a stage of its own, before
/// the collection: a search that waited for it would hold one of the collector's parallel slots for minutes.
/// Only the boards that answered with a body are here; a board that said 304 has nothing new to report.
/// </summary>
internal sealed class AtsSnapshot
{
    private volatile Published? _current;

    /// <summary>Null until a sweep of this pass has finished (or after one failed): the searches then report themselves incomplete.</summary>
    internal Published? Current => _current;

    internal void Reset() => _current = null;

    internal void Publish(IReadOnlyList<AtsSnapshotJob> jobs, bool complete) => _current = new Published(jobs, complete);

    internal sealed record Published(IReadOnlyList<AtsSnapshotJob> Jobs, bool Complete);
}
