using JobRadar.Application.Abstractions;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Integrations.Seek.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Seek;

/// <summary>
/// Seek's keyword search covers the whole ad, so a quoted phrase finds the ads that contain it
/// without opening them (ad pages sit behind an anti-bot challenge). Results stop at 500 per
/// search; past that, the search is repeated state by state.
/// </summary>
internal sealed class SeekFullTextSearch(
    SeekSearchClient client,
    IOptions<SeekOptions> options,
    TimeProvider time,
    ILogger<SeekFullTextSearch> log) : IFullTextSearch
{
    internal const string Everywhere = "All Australia";

    internal static readonly string[] States =
    [
        "Western Australia", "Queensland", "New South Wales", "Victoria",
        "South Australia", "Northern Territory", "Tasmania", "Australian Capital Territory",
    ];

    public string Source => SeekListingTranslator.SourceName;

    public async Task<FullTextResult> FindAsync(FullTextTerm term, int postedWithinDays, CancellationToken ct = default)
    {
        var query = Query(term);
        var (ids, complete) = await IdsAsync(query, Everywhere, postedWithinDays, ct);
        if (complete)
            return new FullTextResult(ids, Complete: true);

        var allStates = true;
        foreach (var state in States)
        {
            await PauseAsync(ct);
            var (stateIds, stateComplete) = await IdsAsync(query, state, postedWithinDays, ct);
            ids.UnionWith(stateIds);
            allStates &= stateComplete;
        }
        if (!allStates)
            log.LogWarning("Full-text term {Term} is capped even per state; some listings are not marked", term.Text);
        return new FullTextResult(ids, allStates);
    }

    /// <summary>Phrases quoted, single words bare: "12 months + working rights" → "12 months" "working rights".</summary>
    internal static string Query(FullTextTerm term) =>
        string.Join(" ", term.Phrases.Select(p => p.Contains(' ') ? $"\"{p}\"" : p));

    private async Task<(HashSet<string> Ids, bool Complete)> IdsAsync(string query, string where, int days, CancellationToken ct)
    {
        var o = options.Value;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var search = new ListingSearch(query, where, days);
        for (var page = 1; page <= o.MaxPages; page++)
        {
            if (page > 1)
                await PauseAsync(ct);
            var result = await client.SearchAsync(search, page, ct);
            ids.UnionWith(result.Jobs.Select(j => j.Job.Id));
            if (result.Jobs.Count < o.PageSize || ids.Count >= result.TotalCount)
                return (ids, true);
        }
        return (ids, false);
    }

    private Task PauseAsync(CancellationToken ct)
    {
        var delay = options.Value.DelayBetweenPages;
        return delay <= TimeSpan.Zero
            ? Task.CompletedTask
            : Task.Delay(delay * (0.7 + Random.Shared.NextDouble() * 0.6), time, ct);
    }
}
