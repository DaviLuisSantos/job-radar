using System.Reflection;
using Bunit;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Matching;
using JobRadar.Application.Triage;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Web.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MudBlazor.Services;

namespace JobRadar.Web.Tests;

/// <summary>
/// The frame of every page. A runtime error here takes the whole site down, and the site cannot run here without a
/// database, so the layout is rendered for real against ports that answer empty.
/// </summary>
public class MainLayoutTests : BunitContext
{
    private IRenderedComponent<MainLayout> RenderWith(params (string Name, ProfileOptions Options)[] profiles)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new ProfileCatalog(Options.Create(new MatchingOptions
        {
            Profiles = profiles.ToDictionary(p => p.Name, p => p.Options),
        }), ScoringPolicy.Default));
        Services.AddSingleton(EmptyPort<IListingRepository>());
        Services.AddSingleton(EmptyPort<IListingScoreRepository>());
        Services.AddSingleton(EmptyPort<IListingTermRepository>());
        Services.AddSingleton(EmptyPort<ITrackingRepository>());
        Services.AddSingleton(EmptyPort<IListingDescriptionRepository>());
        Services.AddScoped<TriageService>();
        Services.AddSingleton<Backend>();
        return Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddContent(0, "page body"))));
    }

    private static ProfileOptions In(string market) => new() { Market = market, Keywords = ["dev"], Locations = ["x"] };

    [Fact]
    public void With_two_markets_the_bar_offers_both_and_the_profiles_of_the_current_one()
    {
        var cut = RenderWith(("dotnet", In("AU")), ("fifo", In("AU")), ("dotnet-br", In("BR")));

        Assert.Contains("page body", cut.Markup);
        Assert.Equal(["Austrália", "Brasil"], cut.FindAll(".mud-toggle-item").Select(i => i.TextContent.Trim()));
        Assert.Equal(["dotnet", "fifo"], cut.FindAll(".profile-bar a").Select(a => a.TextContent.Trim()));
    }

    [Fact]
    public void With_one_market_there_is_nothing_to_choose()
    {
        var cut = RenderWith(("dotnet", In("AU")));

        Assert.Empty(cut.FindAll(".mud-toggle-item"));
        Assert.Equal(["dotnet"], cut.FindAll(".profile-bar a").Select(a => a.TextContent.Trim()));
    }

    /// <summary>An implementation of any port whose every call answers empty: no rows, no listing, nothing done.</summary>
    private static T EmptyPort<T>() where T : class => DispatchProxy.Create<T, EmptyProxy>();

    public class EmptyProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var type = method!.ReturnType;
            if (type == typeof(Task))
                return Task.CompletedTask;
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Task<>))
                return null;
            var result = type.GetGenericArguments()[0];
            var value = Empty(result);
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(result).Invoke(null, [value]);
        }

        private static object? Empty(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() is var g
                && (g == typeof(IReadOnlyList<>) || g == typeof(IReadOnlyCollection<>) || g == typeof(IEnumerable<>)))
                return Array.CreateInstance(type.GetGenericArguments()[0], 0);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
                return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments()));
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
