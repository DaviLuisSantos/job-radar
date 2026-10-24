using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;

namespace JobRadar.Worker.Cli;

/// <summary>A one-off command run instead of the daily worker: <c>JobRadar.Worker status seek:123 applied</c>.</summary>
public abstract record CliCommand
{
    public const string Usage = """
        Usage (without a command the worker runs the daily pipeline):
          status    <id> reviewed|dismissed|applied|interview|offer|declined
          notes     <id> <text...>
          label     <id> good|bad|clear [note...]
          calibrate [--rescore [--assume <item>=<state>,...]]  how scores agree with your labels (--rescore: this
                                          build's rules, nothing saved; --assume: as if you set those requirement states)
          requirements                    the requirement catalog and where you stand on each item
          requirement <item> have|obtainable|missing|out-of-reach|default
          import-candidate <candidate.json> [--dry-run]  requirement states from your candidate facts
          collect [--full]                run the daily pass now (--full: ignore the incremental windows)
          market                          take the market snapshot (Raio-X) now
          market-report [YYYY-MM] [file]  monthly Raio-X as Markdown (default: this month, to the screen)
          import-prototype <jobs.db> [--dry-run]  bring the Python prototype history in (safe to repeat)
        <id> is source:id, e.g. seek:81234567
        """;

    public sealed record SetStatus(ListingId Id, ApplicationStatus Status) : CliCommand;
    public sealed record SetNotes(ListingId Id, string? Notes) : CliCommand;
    public sealed record SetLabel(ListingId Id, Verdict? Verdict, string? Note) : CliCommand;
    /// <param name="Assume">Requirement states to measure as if set (null state: the default); only with a rescore.</param>
    public sealed record Calibrate(bool Rescore = false, IReadOnlyDictionary<string, RequirementState?>? Assume = null) : CliCommand;
    public sealed record TakeMarketSnapshot : CliCommand;
    public sealed record Collect(bool FullHistory) : CliCommand;
    public sealed record MarketReport(string? Month, string? OutputFile) : CliCommand;
    public sealed record ImportPrototype(string Path, bool DryRun) : CliCommand;
    public sealed record ListRequirements : CliCommand;
    /// <param name="State">Null puts the item back to the default of its kind.</param>
    public sealed record SetRequirement(string Item, RequirementState? State) : CliCommand;
    public sealed record ImportCandidate(string Path, bool DryRun) : CliCommand;

    /// <summary>The words that make the program a command instead of the worker.</summary>
    public static readonly string[] Names = ["status", "notes", "label", "calibrate", "collect", "market", "market-report", "import-prototype",
        "requirements", "requirement", "import-candidate"];

    public static bool IsCommand(string[] args) =>
        args.Length > 0 && Names.Contains(args[0], StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses the arguments; on failure returns null and says why.</summary>
    public static CliCommand? Parse(string[] args, out string? error)
    {
        error = null;
        var name = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        try
        {
            return name switch
            {
                "status" when args.Length == 3 => new SetStatus(ParseId(args[1]), ParseStatus(args[2])),
                "notes" when args.Length >= 2 => new SetNotes(ParseId(args[1]), Rest(args, 2)),
                "label" when args.Length >= 3 => new SetLabel(ParseId(args[1]), ParseLabel(args[2]), Rest(args, 3)),
                "calibrate" when args.Length == 1 => new Calibrate(),
                "calibrate" when args.Length == 2 && args[1] == "--rescore" => new Calibrate(Rescore: true),
                "calibrate" when args.Length == 4 && args[1] == "--rescore" && args[2] == "--assume" =>
                    new Calibrate(Rescore: true, ParseAssume(args[3])),
                "market" when args.Length == 1 => new TakeMarketSnapshot(),
                "collect" when args.Length == 1 => new Collect(FullHistory: false),
                "collect" when args.Length == 2 && args[1] == "--full" => new Collect(FullHistory: true),
                "import-prototype" when args.Length == 2 && args[1] != "--dry-run" => new ImportPrototype(args[1], DryRun: false),
                "import-prototype" when args.Length == 3 && args[2] == "--dry-run" => new ImportPrototype(args[1], DryRun: true),
                "requirements" when args.Length == 1 => new ListRequirements(),
                "requirement" when args.Length == 3 => new SetRequirement(args[1], ParseRequirementState(args[2])),
                "import-candidate" when args.Length == 2 && args[1] != "--dry-run" => new ImportCandidate(args[1], DryRun: false),
                "import-candidate" when args.Length == 3 && args[2] == "--dry-run" => new ImportCandidate(args[1], DryRun: true),
                "market-report" when args.Length <= 3 => new MarketReport(
                    args.Length > 1 ? ParseMonth(args[1]) : null, args.Length > 2 ? args[2] : null),
                _ => Fail($"Wrong arguments for '{name}'.", out error),
            };
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return null;
        }
    }

    private static CliCommand? Fail(string message, out string? error)
    {
        error = message;
        return null;
    }

    private static ListingId ParseId(string value) => ListingId.Parse(value);

    private static ApplicationStatus ParseStatus(string value) =>
        Enum.TryParse<ApplicationStatus>(value, ignoreCase: true, out var s) && Enum.IsDefined(s)
            ? s
            : throw new FormatException($"Unknown status '{value}'.");

    private static Verdict? ParseLabel(string value) => value.ToLowerInvariant() switch
    {
        "good" => Verdict.Good,
        "bad" => Verdict.Bad,
        "clear" => null,
        _ => throw new FormatException($"Label must be good, bad or clear, not '{value}'."),
    };

    private static RequirementState? ParseRequirementState(string value) => value.ToLowerInvariant() switch
    {
        "have" => RequirementState.Have,
        "obtainable" => RequirementState.Obtainable,
        "missing" => RequirementState.Missing,
        "out-of-reach" => RequirementState.OutOfReach,
        "default" => null,
        _ => throw new FormatException($"State must be have, obtainable, missing, out-of-reach or default, not '{value}'."),
    };

    private static Dictionary<string, RequirementState?> ParseAssume(string value)
    {
        var states = new Dictionary<string, RequirementState?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[0].Length == 0)
                throw new FormatException($"--assume takes item=state pairs separated by commas, not '{pair}'.");
            states[parts[0]] = ParseRequirementState(parts[1]);
        }
        return states.Count > 0 ? states : throw new FormatException("--assume needs at least one item=state.");
    }

    private static string ParseMonth(string value) =>
        DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _)
            ? value
            : throw new FormatException($"Month must be YYYY-MM, not '{value}'.");

    private static string? Rest(string[] args, int from) =>
        args.Length > from ? string.Join(' ', args[from..]) : null;
}
