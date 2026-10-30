using System.Text.RegularExpressions;

namespace JobRadar.Domain.Matching;

/// <summary>
/// The phrases of remote ads, in English, read for someone living in Brazil (<see cref="MarketRules.LatamRemote"/>,
/// <see cref="MarketRules.GlobalRemote"/>). Written for this project, not ported from the prototype.
/// </summary>
public static class RemotePatterns
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    // Places that leave Brazil out, as ads name them.
    private const string Elsewhere =
        @"(?:the\s+)?(?:us|u\.s\.|usa|united\s+states|canada|uk|u\.k\.|united\s+kingdom|eu|europe|emea|apac|india|australia|germany|spain|portugal|philippines)";

    /// <summary>
    /// Residence or work authorisation elsewhere as a condition: "US only", "must reside in the United States",
    /// "authorized to work in the UK", "must be based in Europe".
    /// </summary>
    public static readonly Regex ResidencyRestricted = new(
        $@"\b{Elsewhere}[\s-]+(?:only|based\s+only|residents\s+only)\b"
        + $@"|\bonly\s+(?:open\s+to\s+)?(?:candidates|applicants|residents)\s+(?:in|from|of)\s+{Elsewhere}\b"
        + $@"|\bmust\s+(?:be\s+)?(?:reside|residing|live|living|located|based|a\s+resident)\s+(?:in|of)\s+{Elsewhere}\b"
        + $@"|\b(?:legally\s+)?authori[sz]ed\s+to\s+work\s+in\s+{Elsewhere}\b"
        + $@"|\b{Elsewhere}\s+(?:citizens?|citizenship)\s+(?:only|required)\b",
        Options);

    /// <summary>Paid against invoices: the candidate's own company or tax registration (PJ, in Brazil).</summary>
    public static readonly IReadOnlyList<string> Contractor =
    [
        "independent contractor", "as a contractor", "contractor position", "contractor role", "contractor basis",
        "contract-based", "b2b contract", "b2b", "freelance", "1099",
    ];

    /// <summary>Employed through an employer of record in Brazil (CLT through a third party).</summary>
    public static readonly IReadOnlyList<string> Eor = ["employer of record", "eor"];

    /// <summary>Employed by the company itself.</summary>
    public static readonly IReadOnlyList<string> DirectHire =
    [
        "full-time employee", "full time employee", "direct hire", "permanent employee", "w-2", "w2 employee",
    ];

    /// <summary>Platforms that hire both ways: their name says only which, next to "contractor" or "employee".</summary>
    public static readonly IReadOnlyList<string> HiringPlatforms = ["deel", "remote.com", "oyster"];
}
