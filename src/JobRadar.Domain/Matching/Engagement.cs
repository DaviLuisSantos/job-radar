namespace JobRadar.Domain.Matching;

/// <summary>How a remote job hires someone living in Brazil.</summary>
public enum Engagement
{
    Unknown,

    /// <summary>Paid against invoices (PJ).</summary>
    Contractor,

    /// <summary>Employed through an employer of record (CLT through a third party).</summary>
    Eor,

    /// <summary>Employed by the company itself.</summary>
    Direct,
}

/// <summary>
/// Reads the <see cref="Engagement"/> from an ad's words (<see cref="MarketRules.ContractorPhrases"/> and the like).
/// A hiring platform ("Deel") counts next to "contractor" or "employee", within <see cref="PlatformWindow"/>
/// characters. When the ad says several, EOR wins over contractor, and contractor over direct: the narrower first.
/// </summary>
public static class EngagementDetector
{
    public const int PlatformWindow = 60;

    public static Engagement Detect(string text, MarketRules rules)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Engagement.Unknown;
        var t = text.ToLowerInvariant();
        bool Says(IEnumerable<string> phrases) => phrases.Any(p => ListingPatterns.MatchesUnnegated(p, t));

        var platform = PlatformNextTo(t, rules);
        if (Says(rules.EorPhrases) || platform == Engagement.Eor)
            return Engagement.Eor;
        if (Says(rules.ContractorPhrases) || platform == Engagement.Contractor)
            return Engagement.Contractor;
        return Says(rules.DirectHirePhrases) ? Engagement.Direct : Engagement.Unknown;
    }

    private static Engagement PlatformNextTo(string t, MarketRules rules)
    {
        foreach (var platform in rules.HiringPlatforms)
        {
            for (var at = t.IndexOf(platform, StringComparison.Ordinal); at >= 0; at = t.IndexOf(platform, at + 1, StringComparison.Ordinal))
            {
                var from = Math.Max(0, at - PlatformWindow);
                var near = t.Substring(from, Math.Min(t.Length, at + platform.Length + PlatformWindow) - from);
                if (near.Contains("employee", StringComparison.Ordinal))
                    return Engagement.Eor;
                if (near.Contains("contractor", StringComparison.Ordinal))
                    return Engagement.Contractor;
            }
        }
        return Engagement.Unknown;
    }
}
