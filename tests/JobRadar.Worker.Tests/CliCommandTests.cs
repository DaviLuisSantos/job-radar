using JobRadar.Domain.Listings;
using JobRadar.Domain.Tracking;
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

    [Theory]
    [InlineData("market-report", "outubro")]
    [InlineData("market-report", "2026-13")]
    [InlineData("market", "now")]
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
