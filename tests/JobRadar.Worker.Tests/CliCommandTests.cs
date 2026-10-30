using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Worker.Cli;

namespace JobRadar.Worker.Tests;

public class CliCommandTests
{
    private static CliCommand? Parse(params string[] args) => CliCommand.Parse(args, out _);

    [Fact]
    public void Status()
    {
        Assert.Equal(new CliCommand.SetStatus(new ListingId("seek", "123"), ApplicationStatus.Interview), Parse("status", "seek:123", "interview"));
    }

    [Fact]
    public void Notes_take_the_rest_of_the_line()
    {
        Assert.Equal(new CliCommand.SetNotes(new ListingId("seek", "1"), "call back Friday, ask = salary"),
            Parse("notes", "seek:1", "call", "back", "Friday,", "ask", "=", "salary"));
    }

    [Fact]
    public void Label_with_and_without_note_and_clear()
    {
        Assert.Equal(new CliCommand.SetLabel(new ListingId("seek", "1"), Verdict.Good, "my stack"), Parse("label", "seek:1", "good", "my", "stack"));
        Assert.Equal(new CliCommand.SetLabel(new ListingId("seek", "1"), Verdict.Bad, null), Parse("label", "seek:1", "BAD"));
        Assert.Equal(new CliCommand.SetLabel(new ListingId("seek", "1"), null, null), Parse("label", "seek:1", "clear"));
    }

    [Fact]
    public void Calibrate()
    {
        Assert.Equal(new CliCommand.Calibrate(), Parse("calibrate"));
        Assert.Equal(new CliCommand.Calibrate(Rescore: true), Parse("calibrate", "--rescore"));
        Assert.Null(Parse("calibrate", "--other"));

        var assume = Assert.IsType<CliCommand.Calibrate>(
            Parse("calibrate", "--rescore", "--assume", "hrwl=have, truck-hr = default, years:tech=8, years:fifo=default"));
        Assert.True(assume.Rescore);
        Assert.Equal([("hrwl", (RequirementState?)RequirementState.Have), ("truck-hr", null)],
            assume.Assume!.States.Select(a => (a.Key, a.Value)));
        Assert.Equal([("tech", (double?)8), ("fifo", null)], assume.Assume.Years.Select(a => (a.Key, a.Value)));
    }

    [Fact]
    public void Collect_now_with_or_without_the_full_history()
    {
        Assert.Equal(new CliCommand.Collect(false), Parse("collect"));
        Assert.Equal(new CliCommand.Collect(true), Parse("collect", "--full"));
        Assert.Null(Parse("collect", "--all"));
    }

    [Fact]
    public void Market_commands()
    {
        Assert.IsType<CliCommand.TakeMarketSnapshot>(Parse("market"));
        Assert.Equal(new CliCommand.MarketReport(null, null), Parse("market-report"));
        Assert.Equal(new CliCommand.MarketReport("2026-10", "raio-x.md"), Parse("market-report", "2026-10", "raio-x.md"));
    }

    [Fact]
    public void Tech_count_defaults_to_Brazil_and_this_month()
    {
        Assert.Equal(new CliCommand.TechCount(Country.BR, null), Parse("tech-count"));
        Assert.Equal(new CliCommand.TechCount(Country.AU, null), Parse("tech-count", "au"));
        Assert.Equal(new CliCommand.TechCount(Country.BR, "2026-10"), Parse("tech-count", "2026-10"));
        Assert.Equal(new CliCommand.TechCount(Country.AU, "2026-09"), Parse("tech-count", "AU", "2026-09"));
    }

    [Fact]
    public void Copy_database_names_the_target_provider_by_name()
    {
        Assert.Equal(new CliCommand.CopyDatabase(DatabaseProvider.SqlServer, "Server=x"), Parse("copy-database", "sqlserver", "Server=x"));
        Assert.Equal(new CliCommand.CopyDatabase(DatabaseProvider.Postgres, "Host=x"), Parse("copy-database", "Postgres", "Host=x"));
    }

    [Fact]
    public void Requirement_commands()
    {
        Assert.IsType<CliCommand.ListRequirements>(Parse("requirements"));
        Assert.Equal(new CliCommand.SetRequirement("hrwl", RequirementState.Have), Parse("requirement", "hrwl", "have"));
        Assert.Equal(new CliCommand.SetRequirement("truck-hr", RequirementState.OutOfReach), Parse("requirement", "truck-hr", "Out-Of-Reach"));
        Assert.Equal(new CliCommand.SetRequirement("degree", null), Parse("requirement", "degree", "default"));
        Assert.Equal(new CliCommand.ImportCandidate("candidate.json", false), Parse("import-candidate", "candidate.json"));
        Assert.Equal(new CliCommand.ImportCandidate("candidate.json", true), Parse("import-candidate", "candidate.json", "--dry-run"));
        Assert.Equal(new CliCommand.SetExperience("tech", 8), Parse("experience", "tech", "8"));
        Assert.Equal(new CliCommand.SetExperience("fifo", 0.5), Parse("experience", "fifo", "0.5"));
        Assert.Equal(new CliCommand.SetExperience("fifo", null), Parse("experience", "fifo", "default"));
    }

    [Theory]
    [InlineData("requirement", "hrwl", "maybe")]
    [InlineData("experience", "tech", "eight")]
    [InlineData("experience", "tech")]
    [InlineData("calibrate", "--assume", "hrwl=have")]
    [InlineData("calibrate", "--rescore", "--assume", "hrwl")]
    [InlineData("calibrate", "--rescore", "--assume", "hrwl=sometimes")]
    [InlineData("calibrate", "--rescore", "--assume", "years:tech=many")]
    [InlineData("requirement", "hrwl")]
    [InlineData("import-candidate", "--dry-run")]
    [InlineData("market-report", "outubro")]
    [InlineData("market-report", "2026-13")]
    [InlineData("market", "now")]
    [InlineData("copy-database", "Oracle", "Server=x")]
    [InlineData("tech-count", "NZ", "2026-10")]
    [InlineData("tech-count", "outubro")]
    [InlineData("copy-database", "", "Server=x")]
    [InlineData("copy-database", "SqlServer")]
    [InlineData("status", "seek:1", "hired")]
    [InlineData("status", "seek_123", "applied")]
    [InlineData("label", "seek:1", "maybe")]
    [InlineData("status", "seek:1")]
    [InlineData("calibrate", "now")]
    public void Bad_input_is_refused_with_a_reason(params string[] args)
    {
        Assert.Null(CliCommand.Parse(args, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Only_known_words_turn_the_worker_into_a_command()
    {
        Assert.True(CliCommand.IsCommand(["label", "seek:1", "good"]));
        Assert.False(CliCommand.IsCommand([]));
        Assert.False(CliCommand.IsCommand(["--Collection:RunOnStartup=true"]));
    }
}
