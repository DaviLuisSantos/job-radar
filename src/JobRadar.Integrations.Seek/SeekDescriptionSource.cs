using JobRadar.Application.Abstractions;
using JobRadar.Integrations.Seek.Api;

namespace JobRadar.Integrations.Seek;

/// <summary>Seek's ad text, as plain text (see <see cref="SeekJobDetailsClient"/>).</summary>
internal sealed class SeekDescriptionSource(SeekJobDetailsClient client) : IDescriptionSource
{
    public string Source => SeekListingTranslator.SourceName;

    public async Task<string?> FetchAsync(string sourceId, CancellationToken ct = default) =>
        await client.GetContentAsync(sourceId, ct) is { } html ? SeekHtml.ToText(html) : null;
}
