using System.Text.RegularExpressions;

namespace JobRadar.Domain.Matching;

/// <summary>
/// The phrases of Brazilian ads, for <see cref="MarketRules.Brazil"/>. Written for this project, not ported:
/// calibrate them with labels, as the Australian ones were. Texts are matched lower-cased; accents are optional
/// where ads drop them ("hibrido", "experiencia").
/// </summary>
internal static class BrazilPatterns
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static Regex R(string pattern) => new(pattern, Options);

    /// <summary>For a rule a market does not have (an eligibility pattern not configured, a truck licence): never matches.</summary>
    public static readonly Regex Never = R("(?!)");

    public static readonly Regex LeadershipTitle = R(
        @"\b(?:tech\s*lead|l[íi]der|lideran[çc]a|coordenador\w*|gerente|head|diretor\w*|supervisor\w*|manager)\b");

    public static readonly Regex SeniorRank = R(@"\b(?:s[êe]nior|sr)\b");

    public static readonly Regex MidTitle = R(@"\b(?:pleno|pl|mid|intermedi[áa]ri[oa])\b");

    public static readonly Regex JuniorTitle = R(@"\b(?:j[úu]nior|jr|est[áa]gi\w*|trainee|aprendiz)\b");

    public static readonly Regex TrainingTitle = R(@"\b(?:est[áa]gi\w*|trainee|aprendiz\w*)\b");

    public static readonly Regex CommissionOnly = R(@"\b(?:somente|apenas|s[óo])\s+(?:por\s+)?comiss[ãa]o\b");

    /// <summary>"Inglês fluente", "inglês avançado", "fluência em inglês", "English: advanced"... A condition, not a plus.</summary>
    public static readonly Regex EnglishRequired = R(
        @"\b(?:ingl[êe]s|english)\s*:?\s*(?:fluente|avan[çc]ado|fluent|advanced|obrigat[óo]rio)\b|\bflu[êe]ncia\s+(?:em|no)\s+ingl[êe]s\b");

    public static readonly Regex OnSite = R(@"\bpresencial\b");

    public static readonly string[] NoExperience = ["sem experiência", "sem experiencia", "não é necessário experiência"];

    private static readonly Regex YearsOfExperience = R(
        @"\b(\d{1,2})\s*\+?\s*anos?\s+(?:de\s+)?(?:experi[êe]ncia|atua[çc][ãa]o)|\bexperi[êe]ncia\s+(?:m[íi]nima\s+)?de\s+(\d{1,2})\s*\+?\s*anos?\b");

    /// <summary>The fewest years of experience the text asks for, as <see cref="ListingPatterns.YearsAsked"/>.</summary>
    public static int? YearsAsked(string text)
    {
        int? fewest = null;
        foreach (Match m in YearsOfExperience.Matches(text))
        {
            var digits = m.Groups[1].Success ? m.Groups[1].ValueSpan : m.Groups[2].ValueSpan;
            var years = int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
            if (years is >= 1 and <= 20 && (fewest is null || years < fewest))
                fewest = years;
        }
        return fewest;
    }
}
