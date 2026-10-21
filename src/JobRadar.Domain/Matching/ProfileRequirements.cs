namespace JobRadar.Domain.Matching;

/// <summary>What a requirement phrase found in the full ad says the job asks for.</summary>
public enum RequirementKind
{
    /// <summary>Years or proven experience ("minimum 2 years").</summary>
    Experience,

    /// <summary>A trade certificate or a degree ("trade qualified", "cert III", "degree in").</summary>
    Qualification,

    /// <summary>A licence or ticket ("HR licence", "HRWL").</summary>
    Licence,
}

/// <summary>
/// Requirements a profile's candidate cannot meet, looked for in the full ad text. Most ads ask for
/// years of experience somewhere, so a phrase alone passes the 500-result cap even per state;
/// each phrase is searched with each <see cref="Contexts"/> word ("years experience + FIFO"),
/// which keeps the search to the profile's kind of ad and under the cap.
/// </summary>
/// <param name="Contexts">Words the profile's ads carry ("FIFO"); none searches the phrase alone.</param>
public sealed record ProfileRequirements(
    IReadOnlyList<string> Contexts,
    IReadOnlyList<string> Experience,
    IReadOnlyList<string> Qualification,
    IReadOnlyList<string> Licence)
{
    public static ProfileRequirements None { get; } = new([], [], [], []);

    /// <summary>Every full-text term to search, with the kind and the phrase it stands for.</summary>
    public IEnumerable<(RequirementKind Kind, string Phrase, string Term)> Terms()
    {
        foreach (var (kind, phrases) in new[]
                 {
                     (RequirementKind.Experience, Experience),
                     (RequirementKind.Qualification, Qualification),
                     (RequirementKind.Licence, Licence),
                 })
        {
            foreach (var phrase in phrases)
            {
                if (Contexts.Count == 0)
                    yield return (kind, phrase, phrase);
                foreach (var context in Contexts)
                    yield return (kind, phrase, $"{phrase} + {context}");
            }
        }
    }
}
