using JobRadar.Application.Abstractions;
using JobRadar.Application.Collection;
using JobRadar.Application.Matching;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using Microsoft.Extensions.Options;

namespace JobRadar.Application.Tracking;

/// <summary>The captured text was empty or too short to be an ad.</summary>
public sealed class DescriptionTooShortException()
    : Exception($"The text is too short to be an ad (under {ListingDescription.MinLength} characters).");

/// <param name="Scores">The listing's new score in every profile whose searches found it.</param>
public sealed record CaptureResult(ListingDescription Description, IReadOnlyList<ListingScore> Scores);

/// <summary>
/// The full text of an ad, captured by the candidate from a page they opened (bookmarklet or paste).
/// Stored, then the listing is scored again at once: the point of capturing is to see the new score.
/// </summary>
public sealed class DescriptionService(
    IListingRepository listings,
    IListingDescriptionRepository descriptions,
    ListingScoringService scoring,
    IOptions<CollectionOptions> collection,
    TimeProvider time)
{
    public async Task<CaptureResult> CaptureAsync(ListingId id, string? text, DescriptionOrigin origin, CancellationToken ct = default)
    {
        var normalized = ListingDescription.Normalize(text) ?? throw new DescriptionTooShortException();
        if (await listings.FindAsync(id, ct) is null)
            throw new ListingNotFoundException(id);

        var description = new ListingDescription(id, normalized, origin, time.GetUtcNow());
        await descriptions.SaveAsync(description, ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(collection.Value.TimeZone);
        return new CaptureResult(description, await scoring.ScoreOneAsync(id, zone, ct));
    }
}
