using JobRadar.Domain.Listings;
using JobRadar.Integrations.Gupy.Api;

namespace JobRadar.Integrations.Gupy;

/// <summary>Gupy's job → neutral <see cref="Listing"/>. Everything Gupy-specific stops here.</summary>
internal static class GupyListingTranslator
{
    public const string SourceName = "gupy";

    public static Listing ToListing(GupyJob job) => new(
        Id: new ListingId(SourceName, job.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Title: job.Name?.Trim() is { Length: > 0 } name ? name : "(untitled)",
        Company: Blank(job.CareerPageName),
        Location: new ListingLocation(Country.BR, null, Blank(job.City), StateCode(job.State), null),
        WorkType: WorkType(job.Type),
        SalaryText: null,
        PostedAt: job.PublishedDate,
        Url: new Uri(job.JobUrl ?? $"https://portal.gupy.io/job/{job.Id}"),
        SourceCategory: null,
        Summary: Summary(job));

    /// <summary>
    /// The work arrangement and the contract, in the ad's words ("Remoto · CLT"): the scorer reads arrangements in
    /// the text (<c>MarketRules.Brazil</c>), and the contract is worth seeing in the list.
    /// </summary>
    internal static string? Summary(GupyJob job)
    {
        var parts = new[] { Arrangement(job.WorkplaceType), Contract(job.Type) }.Where(p => p is not null);
        var summary = string.Join(" · ", parts);
        return summary.Length > 0 ? summary : null;
    }

    private static string? Arrangement(string? workplaceType) => workplaceType switch
    {
        "remote" => "Remoto",
        "hybrid" => "Híbrido",
        "on-site" => "Presencial",
        _ => null,
    };

    private static string? Contract(string? type) => type switch
    {
        "vacancy_type_effective" => "CLT",
        "vacancy_legal_entity" => "PJ",
        "vacancy_type_associate" => "Associado",
        _ => null,
    };

    /// <summary>CLT is a permanent job; PJ and associates invoice like contractors.</summary>
    internal static WorkType WorkType(string? type) => type switch
    {
        "vacancy_type_effective" => Domain.Listings.WorkType.FullTime,
        "vacancy_legal_entity" or "vacancy_type_associate" => Domain.Listings.WorkType.Contract,
        _ => Domain.Listings.WorkType.Unknown,
    };

    /// <summary>Gupy names the state in full ("São Paulo"); listings carry the two-letter code, as Brazilians write it.</summary>
    internal static string? StateCode(string? state) =>
        state is null ? null : States.GetValueOrDefault(state.Trim());

    private static readonly Dictionary<string, string> States = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Acre"] = "AC", ["Alagoas"] = "AL", ["Amapá"] = "AP", ["Amazonas"] = "AM", ["Bahia"] = "BA", ["Ceará"] = "CE",
        ["Distrito Federal"] = "DF", ["Espírito Santo"] = "ES", ["Goiás"] = "GO", ["Maranhão"] = "MA",
        ["Mato Grosso"] = "MT", ["Mato Grosso do Sul"] = "MS", ["Minas Gerais"] = "MG", ["Pará"] = "PA",
        ["Paraíba"] = "PB", ["Paraná"] = "PR", ["Pernambuco"] = "PE", ["Piauí"] = "PI", ["Rio de Janeiro"] = "RJ",
        ["Rio Grande do Norte"] = "RN", ["Rio Grande do Sul"] = "RS", ["Rondônia"] = "RO", ["Roraima"] = "RR",
        ["Santa Catarina"] = "SC", ["São Paulo"] = "SP", ["Sergipe"] = "SE", ["Tocantins"] = "TO",
    };

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
