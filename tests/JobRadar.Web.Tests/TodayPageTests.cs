using System.Reflection;
using Bunit;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Matching;
using JobRadar.Application.Triage;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Triage;
using JobRadar.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MudBlazor.Services;

namespace JobRadar.Web.Tests;

/// <summary>
/// "Today" is the page the site opens on and the one each market switch lands on: opening it again must not empty it.
/// </summary>
public class TodayPageTests : BunitContext
{
    private static readonly DateTimeOffset Arrived = DateTimeOffset.UtcNow.AddHours(-2);

    private sealed class Scores : IListingScoreRepository
    {
        public Task<IReadOnlyList<TriageRow>> ListForTriageAsync(TriageFilter filter, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TriageRow>>(filter.Profile != "dotnet-br" || filter.SeenSince > Arrived ? [] :
                [new TriageRow(new ListingId("gupy", "1"), "Desenvolvedor .NET", "Acme", ListingLocation.Unknown(Country.BR),
                    WorkType.FullTime, null, null, Arrived, 82, null, null)]);

        public Task<IReadOnlyList<string>> ListProfilesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(["dotnet-br"]);

        public Task SaveAsync(IReadOnlyCollection<ListingScore> scores, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> DeleteScoredBeforeAsync(DateTimeOffset before, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<JudgedScore>> ListJudgedAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProfileScore>> GetScoresAsync(ListingId id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Visits : IMarketVisitRepository
    {
        public Dictionary<JobMarket, DateTimeOffset> Last { get; } = [];

        public Task<IReadOnlyDictionary<JobMarket, DateTimeOffset>> GetLastVisitsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<JobMarket, DateTimeOffset>>(new Dictionary<JobMarket, DateTimeOffset>(Last));

        public Task RecordVisitAsync(JobMarket market, DateTimeOffset at, CancellationToken ct = default)
        {
            Last[market] = at;
            return Task.CompletedTask;
        }
    }

    private readonly Visits _visits = new();

    public TodayPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new ProfileCatalog(Options.Create(new MatchingOptions
        {
            Profiles = new() { ["dotnet-br"] = new ProfileOptions { Market = "BR", Keywords = ["dev"], Locations = ["Remoto"] } },
        }), ScoringPolicy.Default));
        Services.AddSingleton<IListingScoreRepository>(new Scores());
        Services.AddSingleton<IMarketVisitRepository>(_visits);
        Services.AddSingleton(Empty<IListingRepository>());
        Services.AddSingleton(Empty<IListingTermRepository>());
        Services.AddSingleton(Empty<ITrackingRepository>());
        Services.AddSingleton(Empty<IListingDescriptionRepository>());
        Services.AddSingleton(TimeProvider.System);
        Services.AddScoped<TriageService>();
        Services.AddScoped<TodayService>();
        Services.AddSingleton<Backend>();
    }

    private static T Empty<T>() where T : class => DispatchProxy.Create<T, MainLayoutTests.EmptyProxy>();

    [Fact]
    public void Opening_it_again_keeps_the_listings_to_triage_and_only_drops_the_new_mark()
    {
        var first = Render<TodayPage>(p => p.Add(x => x.Slug, "br"));
        first.WaitForAssertion(() => Assert.Contains("Desenvolvedor .NET", first.Markup));
        Assert.Equal("nova", first.Find(".today-best .chip.fresh").TextContent.Trim());
        first.WaitForAssertion(() => Assert.True(_visits.Last.ContainsKey(JobMarket.BR)));

        var again = Render<TodayPage>(p => p.Add(x => x.Slug, "br"));
        again.WaitForAssertion(() => Assert.Contains("Desenvolvedor .NET", again.Markup));

        Assert.Empty(again.FindAll(".chip.fresh"));
        Assert.Contains("1 a triar", again.Find(".today-profile .new").TextContent);
        Assert.Contains("Última visita em", again.Find(".head-stats").TextContent);
    }
}
