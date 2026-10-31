using System.Reflection;
using System.Text.Json;
using JobRadar.Domain.Collection;

namespace JobRadar.Integrations.Ats;

/// <summary>
/// The Greenhouse slugs shipped with the program (<c>Data/greenhouse-boards.json</c>): the company slugs from the Common
/// Crawl harvest in Riley-Dorrington/job-board-aggregator (MIT), cleaned to the characters a slug can have. About half
/// are dead boards; the sweep finds that out and stops asking. The <c>ats seed</c> command loads them into the table.
/// </summary>
public static class AtsBoardList
{
    private const string Resource = "JobRadar.Integrations.Ats.greenhouse-boards.json";

    public static IReadOnlyList<AtsBoard> Greenhouse()
    {
        using var stream = typeof(AtsBoardList).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"The embedded list '{Resource}' is missing.");
        var slugs = JsonSerializer.Deserialize<string[]>(stream) ?? [];
        return slugs.Select(s => new AtsBoard(GreenhouseListingTranslator.SourceName, s)).ToList();
    }
}
