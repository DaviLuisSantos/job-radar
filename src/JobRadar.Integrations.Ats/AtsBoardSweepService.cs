using System.Collections.Concurrent;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Collection;
using JobRadar.Integrations.Ats.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Ats;

/// <summary>What one sweep of a set of boards brought back.</summary>
/// <param name="Complete">Every board answered (200, 304 or 404); a timeout or a server error makes it false. A dead board is not an error.</param>
internal sealed record AtsSweepResult(int Boards, int Answered, int Gone, int Errors, int Listings, bool Complete);

/// <summary>
/// The sweep of the Greenhouse boards, a stage of the daily pass before the collection: asks the slice of boards swept
/// longest ago, keeps in memory the jobs that are in our markets (<see cref="AtsSnapshot"/>) and stores what each board
/// answered (<see cref="IAtsBoardRepository"/>), so the next pass sends the tags back. One row in <c>collection_runs</c>
/// (<see cref="RunKey"/>) tells how it went.
/// </summary>
internal sealed class AtsBoardSweepService(
    GreenhouseBoardClient client,
    IAtsBoardRepository boards,
    ICollectionRunRepository runs,
    AtsSnapshot snapshot,
    IOptions<AtsOptions> options,
    TimeProvider time,
    ILogger<AtsBoardSweepService> log) : IBoardSweep
{
    public const string RunKey = "ats: sweep greenhouse";

    public string Source => GreenhouseListingTranslator.SourceName;

    /// <param name="ignoreETags">Ask every board for its whole body: a profile with new keywords has not seen the boards that say "unchanged".</param>
    public async Task SweepAsync(bool ignoreETags, CancellationToken ct = default)
    {
        var o = options.Value;
        var started = time.GetUtcNow();
        snapshot.Reset();

        var slice = await boards.ListToSweepAsync(
            GreenhouseListingTranslator.SourceName, o.MaxFailures, started.AddDays(-o.IdleRecheckDays), o.MaxBoardsPerSweep, ct);
        if (slice.Count == 0)
            log.LogWarning("No Greenhouse boards to sweep: run the 'ats seed' command to load the list");

        var (outcomes, jobs, result) = await SweepBoardsAsync(slice, ignoreETags, ct);
        await boards.RecordSweepAsync(outcomes, ct);
        snapshot.Publish(jobs, result.Complete);
        await runs.RecordAsync(new CollectionRun(
            GreenhouseListingTranslator.SourceName, RunKey, PostedWithinDays: null, started, time.GetUtcNow(),
            Found: result.Listings, New: 0, result.Complete,
            Error: null), ct);

        log.LogInformation(
            "Greenhouse sweep: {Boards} boards asked, {Answered} answered, {Gone} gone, {Errors} errors; {Listings} listings in our markets",
            result.Boards, result.Answered, result.Gone, result.Errors, result.Listings);

    }

    /// <summary>The requests of a sweep, without touching storage: also what the live test runs on a few boards.</summary>
    internal async Task<(IReadOnlyList<AtsBoardSweepOutcome> Outcomes, IReadOnlyList<AtsSnapshotJob> Jobs, AtsSweepResult Result)>
        SweepBoardsAsync(IReadOnlyList<AtsBoard> slice, bool ignoreETags, CancellationToken ct)
    {
        var o = options.Value;
        var outcomes = new ConcurrentBag<AtsBoardSweepOutcome>();
        var jobs = new ConcurrentBag<AtsSnapshotJob>();
        var tooLong = 0;
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = o.MaxConcurrentRequests, CancellationToken = ct };

        await Parallel.ForEachAsync(slice, parallel, async (board, token) =>
        {
            var at = time.GetUtcNow();
            try
            {
                using var response = await client.GetBoardAsync(board.Slug, ignoreETags ? null : board.ETag, token);
                if (response.Status != 200)
                {
                    outcomes.Add(new AtsBoardSweepOutcome(board.Platform, board.Slug, response.Status, at));
                    return;
                }

                int total = 0, kept = 0;
                foreach (var (job, raw) in response.Jobs())
                {
                    total++;
                    if (GreenhouseListingTranslator.IdFor(board.Slug, job.Id) is null)
                    {
                        Interlocked.Increment(ref tooLong);
                        continue;
                    }
                    if (GreenhouseListingTranslator.ToListing(board.Slug, job) is not { } translated)
                        continue;
                    kept++;
                    jobs.Add(new AtsSnapshotJob(translated.Listing, translated.Market, raw.GetRawText()));
                }
                outcomes.Add(new AtsBoardSweepOutcome(board.Platform, board.Slug, 200, at, response.ETag, total, kept));
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                // A timeout past the retries, a dropped connection or a body that is not JSON: this board waits for the next pass.
                log.LogWarning("Greenhouse board {Slug} failed: {Error}", board.Slug, $"{ex.GetType().Name}: {ex.Message}");
                outcomes.Add(new AtsBoardSweepOutcome(board.Platform, board.Slug, Status: null, at));
            }
        });

        if (tooLong > 0)
            log.LogWarning("{Count} Greenhouse jobs were left out: the slug is too long for a listing id", tooLong);

        var list = outcomes.ToList();
        var errors = list.Count(x => !x.Alive && !x.Gone);
        var result = new AtsSweepResult(slice.Count, list.Count(x => x.Alive), list.Count(x => x.Gone), errors, jobs.Count, errors == 0);
        return (list, jobs.ToList(), result);
    }
}
