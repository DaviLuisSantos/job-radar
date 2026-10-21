using JobRadar.Application.Abstractions;
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
        public Task<IReadOnlyList<JudgedScore>> ListJudgedAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProfileScore>> GetScoresAsync(ListingId id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static TriageRow Row(int i, string company) =>
        new(new ListingId("seek", i.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Same title", company,
            ListingLocation.Unknown, WorkType.Unknown, null, null, DateTimeOffset.UnixEpoch, 100 - i, null, null);

    private static TriageService Service(IListingScoreRepository scores) => new(null!, scores, null!, null!);

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
    public async Task Filter_values_are_kept_in_range()
    {
        var repo = new FixedRows([]);

        await Service(repo).ListAsync(new TriageFilter("tech", MinScore: 500, Text: "   ", Page: -3, PageSize: 10_000), Ct);

        Assert.Equal((100, (string?)null, 1, 200), (repo.Received!.MinScore, repo.Received.Text, repo.Received.Page, repo.Received.PageSize));
    }
}
