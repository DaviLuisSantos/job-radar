using JobRadar.Domain.Listings;
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
          calibrate
        <id> is source:id, e.g. seek:81234567
        """;

    public sealed record SetStatus(ListingId Id, ApplicationStatus Status) : CliCommand;
    public sealed record SetNotes(ListingId Id, string? Notes) : CliCommand;
    public sealed record SetLabel(ListingId Id, Verdict? Verdict, string? Note) : CliCommand;
    public sealed record Calibrate : CliCommand;

    /// <summary>The words that make the program a command instead of the worker.</summary>
    public static readonly string[] Names = ["status", "notes", "label", "calibrate"];

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

    private static string? Rest(string[] args, int from) =>
        args.Length > from ? string.Join(' ', args[from..]) : null;
}
