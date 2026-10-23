namespace JobRadar.Application.Abstractions;

/// <summary>
/// A source that serves the full text of its own ads. Seek has one: the data endpoint its site loads
/// ad text from (the ad page itself sits behind an anti-bot challenge and is never fetched).
/// </summary>
public interface IDescriptionSource
{
    string Source { get; }

    /// <summary>Plain text of the ad; null when the source no longer has it (expired or removed).</summary>
    /// <exception cref="SourceBlockedException">The source refused the request: stop asking it.</exception>
    Task<string?> FetchAsync(string sourceId, CancellationToken ct = default);
}

/// <summary>
/// The source turned the system away (anti-bot challenge, 403, rate limit past the retries). Every
/// further request would be refused too, and pushing on is what gets an address blocked.
/// </summary>
public sealed class SourceBlockedException(string source, string reason)
    : Exception($"{source} refused the request ({reason}).")
{
    /// <summary>The source that refused (not <see cref="Exception.Source"/>, the throwing assembly).</summary>
    public string SourceName { get; } = source;
    public string Reason { get; } = reason;
}
