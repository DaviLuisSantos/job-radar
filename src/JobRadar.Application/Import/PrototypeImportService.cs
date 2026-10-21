using JobRadar.Application.Abstractions;
using JobRadar.Application.Matching;
using JobRadar.Domain.Import;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Tracking;

namespace JobRadar.Application.Import;

/// <summary>What an import found and did. In a dry run only the "in file" numbers are filled.</summary>
public sealed record ImportReport(
    int JobsInFile, int ListingsNew, int ListingsKnown,
    int TermsInFile, int TermsNew,
    int LabelsInFile, int LabelsAdded, int LabelsSkipped,
    int ApplicationsInFile, int ApplicationsAdded, int ApplicationsSkipped,
    IReadOnlyDictionary<ApplicationStatus, int> ApplicationsByStatus,
    int SnapshotsInFile, int SnapshotsAdded, int SnapshotsSkipped,
    int MarketListingsInFile, int MarketListingsMerged);

/// <summary>
/// Brings the prototype's history in. Safe to run again: what this system already holds wins (an
/// application, a label or a market day already here is left alone), and listings only merge dates.
/// </summary>
public sealed class PrototypeImportService(
    IPrototypeArchive archive,
    IListingRepository listings,
    IListingTermRepository terms,
    ITrackingRepository tracking,
    IMarketRepository market,
    TimeProvider time)
{
    private const int Chunk = 1000;

    public async Task<ImportReport> ImportAsync(string path, bool dryRun, CancellationToken ct = default)
    {
        var data = await archive.ReadAsync(path, ct);
        var applications = data.Jobs
            .Select(j => (Job: j, Status: PrototypeMapping.Status(j.Status, j.Applied)))
            .Where(a => a.Status is not null || !string.IsNullOrWhiteSpace(a.Job.Notes))
            .ToList();
        var byStatus = applications
            .GroupBy(a => a.Status ?? ApplicationStatus.Reviewed)
            .ToDictionary(g => g.Key, g => g.Count());
        var labelled = data.Jobs.Where(j => j.Label is not null).ToList();
        var termCount = data.Jobs.Sum(j => j.Terms.Count);

        if (dryRun)
        {
            return new ImportReport(data.Jobs.Count, 0, 0, termCount, 0, labelled.Count, 0, 0,
                applications.Count, 0, 0, byStatus, data.Snapshots.Count, 0, 0, data.MarketListings.Count, 0);
        }

        // 1. Listings first: terms, labels and applications need them stored.
        var (newListings, knownListings) = (0, 0);
        foreach (var chunk in data.Jobs.Chunk(Chunk))
        {
            var result = await listings.ImportAsync(chunk.Select(j => new ImportedListing(
                j.Listing, j.FirstSeenAt, j.FirstSeenAt, j.Profiles.Select(ProfileCatalog.DirectKey).ToList())).ToList(), ct);
            (newListings, knownListings) = (newListings + result.New, knownListings + result.AlreadyKnown);
        }

        // 2. Full-text terms, grouped per term as the term search records them.
        var termsNew = 0;
        foreach (var group in data.Jobs.SelectMany(j => j.Terms.Select(t => (Term: t, j.Listing.Listing.Id))).GroupBy(x => (x.Id.Source, x.Term)))
            termsNew += await terms.RecordAsync(group.Key.Source, group.Key.Term, group.Select(x => x.Id.SourceId).ToList(),
                time.GetUtcNow(), ct);

        // 3. Labels and applications: only where this system has none yet.
        var (labelsAdded, labelsSkipped) = (0, 0);
        foreach (var job in labelled)
        {
            var id = job.Listing.Listing.Id;
            if (await tracking.FindLabelAsync(id, ct) is not null)
            {
                labelsSkipped++;
                continue;
            }
            await tracking.SetLabelAsync(id, new ListingLabel(id, job.Label!.Value, job.LabelNote, job.LabeledAt ?? job.FirstSeenAt), ct);
            labelsAdded++;
        }

        var (appsAdded, appsSkipped) = (0, 0);
        foreach (var (job, status) in applications)
        {
            var id = job.Listing.Listing.Id;
            if (await tracking.FindApplicationAsync(id, ct) is not null)
            {
                appsSkipped++;
                continue;
            }
            var to = status ?? ApplicationStatus.Reviewed;   // notes without a status
            var at = job.AppliedAt ?? job.FirstSeenAt;
            var appliedAt = JobApplication.WentFor(to) ? job.AppliedAt ?? job.FirstSeenAt : (DateTimeOffset?)null;
            await tracking.SaveApplicationAsync(
                JobApplication.Restore(id, to, appliedAt, string.IsNullOrWhiteSpace(job.Notes) ? null : job.Notes.Trim(),
                    [new StatusChange(null, to, at)]), ct);
            appsAdded++;
        }

        // 4. Market history: days not taken here yet, and the listing ranges merged.
        var (snapsAdded, snapsSkipped) = (0, 0);
        foreach (var s in data.Snapshots)
        {
            if (await market.ImportSnapshotAsync(s.Snapshot, s.Counts, ct))
                snapsAdded++;
            else
                snapsSkipped++;
        }
        var merged = 0;
        foreach (var chunk in data.MarketListings.Chunk(Chunk))
            merged += await market.ImportListingHistoryAsync("seek", chunk, ct);

        return new ImportReport(data.Jobs.Count, newListings, knownListings, termCount, termsNew,
            labelled.Count, labelsAdded, labelsSkipped, applications.Count, appsAdded, appsSkipped, byStatus,
            data.Snapshots.Count, snapsAdded, snapsSkipped, data.MarketListings.Count, merged);
    }
}
