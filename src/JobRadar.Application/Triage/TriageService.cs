using JobRadar.Application.Abstractions;
using JobRadar.Application.Matching;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Triage;

namespace JobRadar.Application.Triage;

/// <summary>The reads behind the triage screens. Writes go through <c>TrackingService</c>.</summary>
public sealed class TriageService(
    IListingRepository listings,
    IListingScoreRepository scores,
    IListingTermRepository terms,
    ITrackingRepository tracking,
    IListingDescriptionRepository descriptions,
    ProfileCatalog? catalog = null)
{
    public Task<IReadOnlyList<string>> ProfilesAsync(CancellationToken ct = default) => scores.ListProfilesAsync(ct);

    /// <summary>One page of the profile listings, the same job posted several times shown once.</summary>
    public async Task<TriagePage> ListAsync(TriageFilter filter, CancellationToken ct = default)
    {
        var f = filter with
        {
            Page = Math.Max(1, filter.Page),
            PageSize = Math.Clamp(filter.PageSize, 1, 200),
            MinScore = Math.Clamp(filter.MinScore, 0, 100),
            Text = string.IsNullOrWhiteSpace(filter.Text) ? null : filter.Text.Trim(),
        };
        // Excluded rows come along to be counted under the same filters; the screen shows them apart, with the reason.
        var all = Duplicates.Collapse(await scores.ListForTriageAsync(f with { IncludeExcluded = true, MinScore = 0 }, ct));
        var excluded = all.Where(r => r.Score == 0).ToList();
        IReadOnlyList<TriageRow> rows = f.Excluded ? excluded : all.Where(r => r.Score > 0 && r.Score >= f.MinScore).ToList();
        // A remote profile's rows say how the job hires, read from the full ad; filtering on it reads every row's ad.
        var rules = catalog?.Profiles.FirstOrDefault(p => string.Equals(p.Name, f.Profile, StringComparison.OrdinalIgnoreCase)) is { } profile
            && MarketRules.For(profile.Market) is { EligibleScope: not null } remote ? remote : null;
        if (rules is not null && f.Engagement is { } wanted)
        {
            var adTexts = await descriptions.GetTextsAsync(rows.Select(r => r.Id).ToList(), ct);
            rows = rows.Where(r => EngagementOf(r, adTexts.GetValueOrDefault(r.Id), rules) == wanted).ToList();
        }
        if (f.Order == TriageOrder.AcrossScores && !f.Excluded)
            rows = LabelSample.Spread(rows);
        var page = rows.Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToList();

        // Read only for the rows shown: full texts are long, and a page is at most a couple of hundred.
        var texts = await descriptions.GetTextsAsync(page.Select(r => r.Id).ToList(), ct);
        page = page
            .Select(r => texts.TryGetValue(r.Id, out var text) ? r with { Facts = ListingFacts.Read(text, hasPayField: ListingFacts.GivesPay(r.SalaryText)) } : r)
            .Select(r => rules is null ? r : r with { Engagement = EngagementOf(r, texts.GetValueOrDefault(r.Id), rules) })
            .ToList();
        return new TriagePage(page, rows.Count, excluded.Count);
    }

    private static Engagement EngagementOf(TriageRow row, string? text, MarketRules rules) =>
        EngagementDetector.Detect($"{row.Title} {text}", rules);

    /// <summary>Null when the listing was never collected.</summary>
    public async Task<ListingDetail?> DetailAsync(ListingId id, CancellationToken ct = default)
    {
        if (await listings.FindAsync(id, ct) is not { } record)
            return null;

        var byId = await terms.GetTermsAsync([id], ct);
        return new ListingDetail(
            record,
            await scores.GetScoresAsync(id, ct),
            byId.GetValueOrDefault(id) ?? [],
            await tracking.FindApplicationAsync(id, ct),
            await tracking.FindLabelAsync(id, ct),
            await descriptions.FindAsync(id, ct));
    }
}
