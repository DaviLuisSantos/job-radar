using JobRadar.Application.Matching;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;
using JobRadar.Infrastructure.Persistence;

namespace JobRadar.Worker.Cli;

/// <summary>A one-off command run instead of the daily worker: <c>JobRadar.Worker status seek:123 applied</c>.</summary>
public abstract record CliCommand
{
    public const string Usage = """
        Usage (without a command the worker runs the daily pipeline):
          status    <id> reviewed|dismissed|applied|interview|offer|declined
          notes     <id> <text...>
          label     <id> good|bad|clear [note...]
          calibrate [--rescore [--assume <item>=<state>,years:<profile>=<n>,...]]  how scores agree with your labels
                                          (--rescore: this build's rules, nothing saved; --assume: as if you set those
                                          requirement states or years of experience)
          requirements                    the requirement catalog and where you stand on each item
          requirement <item> have|obtainable|missing|out-of-reach|default
          experience <profile> <years>|default  your years of experience in a profile's line of work
          import-candidate <candidate.json> [--dry-run]  requirement states from your candidate facts
          collect [--full]                run the daily pass now (--full: ignore the incremental windows)
          market                          take the market snapshot (Raio-X) now
          market-report [YYYY-MM] [file]  monthly Raio-X as Markdown (default: this month, to the screen)
          tech-count [AU|BR|Remote] [YYYY-MM]  technologies the month's full ads asked for (default: BR, this month)
          import-prototype <jobs.db> [--dry-run]  bring the Python prototype history in (safe to repeat)
          copy-database Postgres|SqlServer <connection-string>  every row of this database into another, empty and
                                          migrated (dotnet ef database update)
        <id> is source:id, e.g. seek:81234567
        """;

    public sealed record SetStatus(ListingId Id, ApplicationStatus Status) : CliCommand;
    public sealed record SetNotes(ListingId Id, string? Notes) : CliCommand;
    public sealed record SetLabel(ListingId Id, Verdict? Verdict, string? Note) : CliCommand;
    /// <param name="Assume">Requirement states and years to measure as if set; only with a rescore.</param>
    public sealed record Calibrate(bool Rescore = false, CandidateAssumption? Assume = null) : CliCommand;
    public sealed record TakeMarketSnapshot : CliCommand;
    public sealed record Collect(bool FullHistory) : CliCommand;
    public sealed record MarketReport(string? Month, string? OutputFile) : CliCommand;
    public sealed record TechCount(Country Country, string? Month) : CliCommand;
    public sealed record ImportPrototype(string Path, bool DryRun) : CliCommand;
    public sealed record ListRequirements : CliCommand;
    /// <param name="State">Null puts the item back to the default of its kind.</param>
    public sealed record SetRequirement(string Item, RequirementState? State) : CliCommand;
    public sealed record ImportCandidate(string Path, bool DryRun) : CliCommand;
    /// <param name="Years">Null forgets them: experience requirements weigh by their state again.</param>
    public sealed record SetExperience(string Profile, double? Years) : CliCommand;
    public sealed record CopyDatabase(DatabaseProvider Provider, string ConnectionString) : CliCommand;

    /// <summary>The words that make the program a command instead of the worker.</summary>
    public static readonly string[] Names = ["status", "notes", "label", "calibrate", "collect", "market", "market-report", "import-prototype",
        "requirements", "requirement", "import-candidate", "experience", "copy-database", "tech-count"];

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
                "experience" when args.Length == 3 => new SetExperience(args[1], ParseYears(args[2])),
                "import-candidate" when args.Length == 2 && args[1] != "--dry-run" => new ImportCandidate(args[1], DryRun: false),
                "import-candidate" when args.Length == 3 && args[2] == "--dry-run" => new ImportCandidate(args[1], DryRun: true),
                "copy-database" when args.Length == 3 => new CopyDatabase(ParseProvider(args[1]), args[2]),
                "tech-count" when args.Length == 1 => new TechCount(Country.BR, null),
                "tech-count" when args.Length == 2 && TryCountry(args[1]) is { } only => new TechCount(only, null),
                "tech-count" when args.Length == 2 => new TechCount(Country.BR, ParseMonth(args[1])),
                "tech-count" when args.Length == 3 => new TechCount(TryCountry(args[1]) ?? throw new FormatException($"Unknown country '{args[1]}': AU or BR."), ParseMonth(args[2])),
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

    private static Country? TryCountry(string value) =>
        Enum.GetValues<Country>().Cast<Country?>().FirstOrDefault(c => c.ToString()!.Equals(value, StringComparison.OrdinalIgnoreCase));

    private static DatabaseProvider ParseProvider(string value)
    {
        try
        {
            return string.IsNullOrWhiteSpace(value) ? throw new FormatException("Name the target provider.") : DatabaseProviders.Parse(value);
        }
        catch (InvalidOperationException ex)
        {
            throw new FormatException(ex.Message);
        }
    }

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

    private static CandidateAssumption ParseAssume(string value)
    {
        var states = new Dictionary<string, RequirementState?>(StringComparer.OrdinalIgnoreCase);
        var years = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[0].Length == 0)
                throw new FormatException($"--assume takes item=state or years:profile=n pairs separated by commas, not '{pair}'.");
            if (parts[0].StartsWith("years:", StringComparison.OrdinalIgnoreCase) && parts[0].Length > "years:".Length)
                years[parts[0]["years:".Length..]] = ParseYears(parts[1]);
            else
                states[parts[0]] = ParseRequirementState(parts[1]);
        }
        return states.Count + years.Count > 0
            ? new CandidateAssumption(states, years)
            : throw new FormatException("--assume needs at least one item=state or years:profile=n.");
    }

    /// <summary>Years of experience, rounded to one decimal ("1.5"); "default" for none known.</summary>
    private static double? ParseYears(string value) =>
        value.Equals("default", StringComparison.OrdinalIgnoreCase) ? null
        : double.TryParse(value, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var y) && y <= 60
            ? Math.Round(y, 1)
            : throw new FormatException($"Years must be a number like 2 or 1.5, or default, not '{value}'.");

    private static string ParseMonth(string value) =>
        DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _)
            ? value
            : throw new FormatException($"Month must be YYYY-MM, not '{value}'.");

    private static string? Rest(string[] args, int from) =>
        args.Length > from ? string.Join(' ', args[from..]) : null;
}
