using System.Text.Json.Nodes;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Settings;
using JobRadar.Domain.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace JobRadar.Application.Tests.Settings;

public class SettingsServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeSettings _repository = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero));

    private SettingsService Service(Dictionary<string, string?>? current = null) =>
        new(_repository, new ConfigurationBuilder().AddInMemoryCollection(current ?? []).Build(), _time);

    private const string AuProfile = """{"dev":{"Market":"AU","Keywords":["dotnet"],"Locations":["Perth"]}}""";
    private const string BrProfile = """{"dev":{"Market":"BR","Keywords":["dotnet"],"Locations":["Recife"]}}""";

    [Fact]
    public void Nest_wraps_the_value_in_one_object_per_segment()
    {
        var nested = JsonNode.Parse(SettingsJson.Nest("Matching:Profiles", """{"a":1}"""))!;
        Assert.Equal(1, (int)nested["Matching"]!["Profiles"]!["a"]!);
    }

    [Fact]
    public void Nest_rejects_invalid_json() =>
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => SettingsJson.Nest("Market:Terms", "{nope"));

    [Fact]
    public async Task A_forbidden_key_is_refused()
    {
        var ex = await Assert.ThrowsAsync<SettingsValidationException>(() => Service().SaveAsync("ConnectionStrings:JobRadar", "\"x\"", Ct));
        Assert.Contains("not allowed", ex.Message);
        Assert.Empty(_repository.Rows);
        await Assert.ThrowsAsync<SettingsValidationException>(() => Service().DeleteAsync("Database:Provider", Ct));
    }

    [Fact]
    public async Task Invalid_json_is_refused()
    {
        await Assert.ThrowsAsync<SettingsValidationException>(() => Service().SaveAsync("Market:Terms", "{nope", Ct));
        Assert.Empty(_repository.Rows);
    }

    [Fact]
    public async Task A_profile_in_a_market_without_eligibility_is_refused()
    {
        var ex = await Assert.ThrowsAsync<SettingsValidationException>(() => Service().SaveAsync("Matching:Profiles", AuProfile, Ct));
        Assert.Contains("eligibility", ex.Message);
        Assert.Empty(_repository.Rows);
    }

    [Fact]
    public async Task A_profile_in_a_market_that_needs_no_eligibility_is_saved()
    {
        await Service().SaveAsync("Matching:Profiles", BrProfile, Ct);
        var saved = Assert.Single(_repository.Rows);
        Assert.Equal("Matching:Profiles", saved.Key);
        Assert.Equal(_time.GetUtcNow(), saved.UpdatedAt);
    }

    [Fact]
    public async Task A_profile_is_saved_when_the_current_configuration_has_the_eligibility()
    {
        var current = new Dictionary<string, string?> { ["Matching:Markets:AU:Eligibility:RestrictedPattern"] = "citizens only" };
        await Service(current).SaveAsync("Matching:Profiles", AuProfile, Ct);
        Assert.Single(_repository.Rows);
    }

    [Fact]
    public async Task Replacing_the_eligibility_section_with_an_empty_one_is_refused()
    {
        // The section is replaced at the next start, not merged: {} would leave the AU profile without its block.
        var current = new Dictionary<string, string?>
        {
            ["Matching:Markets:AU:Eligibility:RestrictedPattern"] = "citizens only",
            ["Matching:Profiles:dev:Market"] = "AU",
            ["Matching:Profiles:dev:Keywords:0"] = "dotnet",
            ["Matching:Profiles:dev:Locations:0"] = "Perth",
        };
        await Assert.ThrowsAsync<SettingsValidationException>(() => Service(current).SaveAsync("Matching:Markets", "{}", Ct));
        Assert.Empty(_repository.Rows);
    }

    [Fact]
    public async Task Deleting_the_eligibility_section_in_use_is_refused()
    {
        var current = new Dictionary<string, string?>
        {
            ["Matching:Markets:AU:Eligibility:RestrictedPattern"] = "citizens only",
            ["Matching:Profiles:dev:Market"] = "AU",
            ["Matching:Profiles:dev:Keywords:0"] = "dotnet",
            ["Matching:Profiles:dev:Locations:0"] = "Perth",
        };
        await Assert.ThrowsAsync<SettingsValidationException>(() => Service(current).DeleteAsync("Matching:Markets", Ct));
    }

    [Fact]
    public void The_effective_configuration_hides_forbidden_sections_and_rebuilds_lists()
    {
        var current = new Dictionary<string, string?>
        {
            ["Matching:Profiles:dev:Keywords:0"] = "a",
            ["Matching:Profiles:dev:Keywords:1"] = "b",
            ["ConnectionStrings:JobRadar"] = "secret",
        };
        var json = JsonNode.Parse(Service(current).GetEffective())!;
        Assert.Null(json["ConnectionStrings"]);
        Assert.Equal(2, json["Matching"]!["Profiles"]!["dev"]!["Keywords"]!.AsArray().Count);
    }

    private sealed class FakeSettings : ISettingsRepository
    {
        public List<Setting> Rows { get; } = [];

        public Task<IReadOnlyList<Setting>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Setting>>(Rows.ToList());

        public Task<Setting?> GetAsync(string key, CancellationToken ct) => Task.FromResult(Rows.FirstOrDefault(r => r.Key == key));

        public Task SaveAsync(string key, string json, DateTimeOffset now, CancellationToken ct)
        {
            Rows.RemoveAll(r => r.Key == key);
            Rows.Add(new Setting(key, json, now));
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(string key, CancellationToken ct) => Task.FromResult(Rows.RemoveAll(r => r.Key == key) > 0);

        public Task<string> GetRevisionAsync(CancellationToken ct) => Task.FromResult($"{Rows.Count}|");
    }
}
