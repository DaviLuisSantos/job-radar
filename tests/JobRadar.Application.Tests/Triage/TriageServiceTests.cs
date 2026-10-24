using JobRadar.Application.Abstractions;
using JobRadar.Application.Tests.Collection;
using JobRadar.Application.Triage;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Triage;

namespace JobRadar.Application.Tests.Triage;

public class TriageServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FixedRows(IReadOnlyList<TriageRow> rows) : IListingScoreRepository
    {
        public TriageFilter? Received { get; private set; }

        public Task<IReadOnlyList<TriageRow>> ListForTriageAsync(TriageFilter filter, CancellationToken ct = default)
        {
            Received = filter;
            return Task.FromResult(rows);
        }

        public Task SaveAsync(IReadOnlyCollection<ListingScore> scores, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> DeleteScoredBeforeAsync(DateTimeOffset before, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<JudgedScore>> ListJudgedAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProfileScore>> GetScoresAsync(ListingId id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static TriageRow Row(int i, string company) =>
        new(new ListingId("seek", i.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Same title", company,
            ListingLocation.Unknown, WorkType.Unknown, null, null, DateTimeOffset.UnixEpoch, 100 - i, null, null);

    private static TriageService Service(IListingScoreRepository scores, InMemoryDescriptions? descriptions = null) =>
        new(null!, scores, null!, null!, descriptions ?? new InMemoryDescriptions());

    [Fact]
    public async Task Rows_shown_carry_the_facts_of_their_full_ad()
    {
        var descriptions = new InMemoryDescriptions();
        var described = new ListingId("seek", "1");
        descriptions.Saved[described] = new ListingDescription(described, "FIFO 2:1 roster, $48/hr, accommodation provided.",
            DescriptionOrigin.Source, DateTimeOffset.UnixEpoch);
        var paid = Row(2, "B") with { SalaryText = "$50 per hour" };
        descriptions.Saved[paid.Id] = new ListingDescription(paid.Id, "$48/hr, immediate start", DescriptionOrigin.Source, DateTimeOffset.UnixEpoch);

        var page = await Service(new FixedRows([Row(0, "A"), Row(1, "C"), paid]), descriptions).ListAsync(new TriageFilter("fifo"), Ct);
        var facts = page.Rows.ToDictionary(r => r.Id.SourceId, r => r.Facts.Select(f => f.Text).ToList());

        Assert.Equal(["$48/hr", "FIFO", "2:1 roster", "Accommodation"], facts["1"]);
        Assert.Empty(facts["0"]);                    // no full ad: nothing read
        Assert.Equal(["Immediate start"], facts["2"]);  // the source gives the pay: not read again
    }

    [Fact]
    public async Task Pages_after_collapsing_duplicates_so_counts_and_pages_are_right()
    {
        // 6 rows, but rows 0–2 are one job: 4 distinct.
        var repo = new FixedRows([Row(0, "A"), Row(1, "A"), Row(2, "A"), Row(3, "B"), Row(4, "C"), Row(5, "D")]);

        var page2 = await Service(repo).ListAsync(new TriageFilter("tech", Page: 2, PageSize: 3), Ct);

        Assert.Equal(4, page2.Total);
        Assert.Equal(["5"], page2.Rows.Select(r => r.Id.SourceId));
    }

    [Fact]
    public async Task Across_scores_deals_out_the_bands_before_paging()
    {
        var repo = new FixedRows([Row(0, "A"), Row(1, "B"), Row(2, "C"), Row(60, "D")]);

        var page1 = await Service(repo).ListAsync(new TriageFilter("tech", Order: TriageOrder.AcrossScores, PageSize: 2), Ct);

        Assert.Equal(4, page1.Total);
        Assert.Contains(page1.Rows, r => r.Score == 40);
        Assert.Equal(TriageOrder.AcrossScores, repo.Received!.Order);
    }

    [Fact]
    public async Task Filter_values_are_kept_in_range()
    {
        var repo = new FixedRows([]);

        await Service(repo).ListAsync(new TriageFilter("tech", MinScore: 500, Text: "   ", Page: -3, PageSize: 10_000), Ct);

        Assert.Equal((100, (string?)null, 1, 200), (repo.Received!.MinScore, repo.Received.Text, repo.Received.Page, repo.Received.PageSize));
    }
}
