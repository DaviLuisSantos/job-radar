using JobRadar.Application.Abstractions;
using JobRadar.Integrations.LinkedIn.Api;

namespace JobRadar.Integrations.LinkedIn;

/// <summary>The ad text from the guest posting page, with the criteria and pay LinkedIn shows beside it.</summary>
internal sealed class LinkedInDescriptionSource(LinkedInGuestClient client) : IDescriptionSource
{
    public string Source => LinkedInListingTranslator.SourceName;

    public async Task<string?> FetchAsync(string sourceId, CancellationToken ct = default) =>
        (await client.GetPostingAsync(sourceId, ct))?.ToText();
}
