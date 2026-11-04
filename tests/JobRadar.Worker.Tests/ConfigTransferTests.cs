using System.Text.Json.Nodes;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Settings;
using JobRadar.Domain.Settings;
using JobRadar.Worker.Cli;
using Microsoft.Extensions.Configuration;

namespace JobRadar.Worker.Tests;

public class ConfigTransferTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string File = """
        {
          "Matching": { "Profiles": { "dev": { "Market": "AU", "Keywords": ["dotnet"], "Locations": ["Perth"] } }, "Markets": { "AU": { "Eligibility": { "RestrictedPattern": "citizens only" } } } },
          "Market": { "Terms": { "x": 1 } },
          "ConnectionStrings": { "JobRadar": "secret" },
          "Logging": { "A": 1 }
        }
        """;

    [Fact]
    public void Import_and_export_parse() =>
        Assert.Equal([new CliCommand.ImportConfig("a.json"), new CliCommand.ExportConfig("b.json")],
            new[] { CliCommand.Parse(["import-config", "a.json"], out _), CliCommand.Parse(["export-config", "b.json"], out _) });

    [Fact]
    public void Split_makes_one_row_per_second_level_section_and_skips_forbidden_ones()
    {
        var (rows, skipped) = ConfigTransfer.Split(File);
        Assert.Equal(["Matching:Markets", "Matching:Profiles", "Market:Terms"], rows.Select(r => r.Key));
        Assert.Equal(["ConnectionStrings", "Logging"], skipped);
    }

    [Fact]
    public async Task Import_saves_the_rows_through_the_service_and_export_gives_them_back()
    {
        var repository = new FakeSettings();
        
        var path = Path.GetTempFileName();
        try
        {
            await System.IO.File.WriteAllTextAsync(path, File, Ct);
            Assert.Equal(0, await ConfigTransfer.ImportAsync(path, repository, new ConfigurationBuilder().Build(), TimeProvider.System, new StringWriter(), Ct));
            Assert.Equal(["Market:Terms", "Matching:Markets", "Matching:Profiles"], repository.Rows.Select(r => r.Key).Order());

            var exported = JsonNode.Parse(ConfigTransfer.Export(repository.Rows))!;
            Assert.Equal("AU", (string?)exported["Matching"]!["Profiles"]!["dev"]!["Market"]);
            Assert.Equal(1, (int)exported["Market"]!["Terms"]!["x"]!);
            Assert.Null(exported["ConnectionStrings"]);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
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
