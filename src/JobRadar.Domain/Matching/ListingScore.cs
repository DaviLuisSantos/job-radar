using JobRadar.Domain.Listings;

namespace JobRadar.Domain.Matching;

/// <summary>The score of one listing for one profile, and the rules it was computed with.</summary>
/// <param name="RulesHash">Identity of the rules used (scorer version + profile + policy).</param>
public sealed record ListingScore(ListingId Id, string Profile, FitScore Score, string RulesHash, DateTimeOffset ScoredAt);
