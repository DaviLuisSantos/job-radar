using JobRadar.Domain.Matching;

namespace JobRadar.Domain.Tests.Matching;

public class RequirementCatalogTests
{
    [Fact]
    public void Ids_are_unique_and_kebab_case()
    {
        var ids = RequirementCatalog.Defaults.Select(i => i.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(ids, id => Assert.Matches("^[a-z][a-z0-9-]*$", id));
    }

    [Fact]
    public void A_phrase_belongs_to_one_item()
    {
        var phrases = RequirementCatalog.Defaults.SelectMany(i => i.Phrases).ToList();

        Assert.Equal(phrases.Count, phrases.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(RequirementCatalog.Defaults, i => Assert.NotEmpty(i.Phrases));
    }

    [Fact]
    public void Truck_licence_items_are_told_apart_from_tickets_by_the_pattern()
    {
        var licences = RequirementCatalog.Defaults.Where(i => i.Kind == RequirementKind.Licence).ToList();

        Assert.All(licences.Where(i => i.Id.StartsWith("truck-", StringComparison.Ordinal)).SelectMany(i => i.Phrases),
            p => Assert.Matches(ListingPatterns.TruckLicence, p));
        Assert.All(licences.Where(i => !i.Id.StartsWith("truck-", StringComparison.Ordinal)).SelectMany(i => i.Phrases),
            p => Assert.DoesNotMatch(ListingPatterns.TruckLicence, p));
    }
}
