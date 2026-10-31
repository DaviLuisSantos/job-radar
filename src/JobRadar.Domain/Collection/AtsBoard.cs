namespace JobRadar.Domain.Collection;

/// <summary>
/// One company's public job board on an ATS (Greenhouse, Lever, Ashby), and how its last sweeps went.
/// The platform is the source name of the listings it yields (<c>greenhouse</c>).
/// </summary>
/// <param name="ETag">What the board answered last time; sent back, an unchanged board costs no body.</param>
/// <param name="LastStatus">HTTP status of the last answer; null before the first sweep.</param>
/// <param name="LastSweptAt">When the board last answered (200, 304 or 404); an error does not move it.</param>
/// <param name="LastHitAt">The last sweep in which the board had a listing in one of our markets.</param>
/// <param name="ConsecutiveFailures">Sweeps in a row that found the board gone (404).</param>
/// <param name="JobsTotal">Listings on the board at the last 200.</param>
/// <param name="JobsKept">Of those, the ones in our markets.</param>
public sealed record AtsBoard(
    string Platform,
    string Slug,
    string? ETag = null,
    int? LastStatus = null,
    DateTimeOffset? LastSweptAt = null,
    DateTimeOffset? LastHitAt = null,
    int ConsecutiveFailures = 0,
    int JobsTotal = 0,
    int JobsKept = 0);

/// <summary>What one sweep learned about one board.</summary>
/// <param name="Status">HTTP status; null when no answer came (timeout, network).</param>
/// <param name="ETag">The board's new tag; only read on a 200.</param>
/// <param name="JobsTotal">Listings on the board; only read on a 200 (a 304 keeps the counts it had).</param>
/// <param name="JobsKept">Of those, the ones in our markets; only read on a 200.</param>
public sealed record AtsBoardSweepOutcome(
    string Platform,
    string Slug,
    int? Status,
    DateTimeOffset At,
    string? ETag = null,
    int JobsTotal = 0,
    int JobsKept = 0)
{
    /// <summary>The board answered and is alive: 200 with its listings, or 304 for "unchanged".</summary>
    public bool Alive => Status is 200 or 304;

    /// <summary>The board does not exist (any more).</summary>
    public bool Gone => Status == 404;
}
