using JobRadar.Domain.Matching;

namespace JobRadar.Integrations.Seek.Tests;

public class SeekFullTextSearchTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IEnumerable<string> Ids(int from, int count) =>
        Enumerable.Range(from, count).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));

    [Theory]
    [InlineData("guild member", "\"guild member\"")]
    [InlineData("newcomers", "newcomers")]
    [InlineData("24 months + guild standing", "\"24 months\" \"guild standing\"")]
    public void Phrases_are_quoted_and_joined_with_and(string term, string query)
    {
        Assert.Equal(query, SeekFullTextSearch.Query(new FullTextTerm(term)));
    }

    [Fact]
    public async Task Searches_all_of_australia_in_the_date_window()
    {
        var api = new FakeSeekApi { Respond = _ => FakeSeekApi.PageOf(2, ["1", "2"]) };

        var result = await api.FullText().FindAsync(new FullTextTerm("seasonal member"), 5, Ct);

        Assert.Equal(["1", "2"], result.SourceIds.Order());
        Assert.True(result.Complete);
        var q = Assert.Single(api.Queries);
        Assert.Equal(("\"seasonal member\"", "All Australia", "5"), (q["keywords"], q["where"], q["dateRange"]));
    }

    [Fact]
    public async Task Pages_until_every_listing_is_in()
    {
        var api = new FakeSeekApi
        {
            Respond = q => q["page"] == "1" ? FakeSeekApi.PageOf(5, Ids(1, 3)) : FakeSeekApi.PageOf(5, Ids(4, 2)),
        };

        var result = await api.FullText(pageSize: 3).FindAsync(new FullTextTerm("commission only"), 31, Ct);

        Assert.Equal(5, result.SourceIds.Count);
        Assert.Equal(2, api.Requests.Count);
    }

    [Fact]
    public async Task Past_the_cap_it_repeats_the_search_state_by_state()
    {
        // All Australia reports 30 but stops at one page; each state has its own share.
        var api = new FakeSeekApi
        {
            Respond = q => q["where"] switch
            {
                "All Australia" => FakeSeekApi.PageOf(30, Ids(1, 3)),
                "Western Australia" => FakeSeekApi.PageOf(2, ["1", "100"]),
                "Queensland" => FakeSeekApi.PageOf(1, ["200"]),
                _ => FakeSeekApi.PageOf(0, []),
            },
        };

        var result = await api.FullText(pageSize: 3, maxPages: 1).FindAsync(new FullTextTerm("guild pass"), 31, Ct);

        Assert.Equal(["1", "100", "2", "200", "3"], result.SourceIds.Order(StringComparer.Ordinal));
        Assert.True(result.Complete);
        Assert.Equal(1 + SeekFullTextSearch.States.Length, api.Requests.Count);
    }

    [Fact]
    public async Task A_state_still_past_the_cap_makes_the_result_incomplete()
    {
        var api = new FakeSeekApi
        {
            Respond = q => q["where"] == "New South Wales" ? FakeSeekApi.PageOf(9, Ids(50, 3)) : FakeSeekApi.PageOf(9, Ids(1, 3)),
        };

        var result = await api.FullText(pageSize: 3, maxPages: 1).FindAsync(new FullTextTerm("guild pass"), 31, Ct);

        Assert.False(result.Complete);
    }
}
