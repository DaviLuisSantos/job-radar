using JobRadar.Domain.Collection;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;
using JobRadar.Domain.Triage;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Infrastructure.Persistence.Collection;
using JobRadar.Infrastructure.Persistence.Listings;
using JobRadar.Infrastructure.Persistence.Matching;
using JobRadar.Infrastructure.Persistence.Tracking;
using JobRadar.Infrastructure.Tests.Databases;
using JobRadar.Infrastructure.Tests.Persistence;

namespace JobRadar.Infrastructure.Tests.Contracts;

/// <summary>Both databases at once: <c>copy-database</c> from Postgres, as in production, to SQL Server.</summary>
[CollectionDefinition(Name)]
public sealed class BothDatabasesCollection : ICollectionFixture<PostgresFixture>, ICollectionFixture<SqlServerFixture>
{
    public const string Name = "BothDatabases";
}

[Collection(BothDatabasesCollection.Name)]
public sealed class DatabaseCopyContractTests(PostgresFixture postgres, SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly ListingId One = new("seek", "1");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task Every_row_reaches_the_other_database_and_reads_back_the_same()
    {
        await postgres.ResetAsync();
        await sqlServer.ResetAsync();
        await SeedAsync(postgres);

        IReadOnlyList<TableCopy> copied;
        await using (var from = postgres.NewContext())
        await using (var to = sqlServer.NewContext())
            copied = await DatabaseCopy.CopyAsync(from, to, Ct);

        Assert.Equal(1_001, copied.Single(t => t.Table == "listings").Rows);
        Assert.Equal(2, copied.Single(t => t.Table == "collection_runs").Rows);
        Assert.Equal(2, copied.Single(t => t.Table == "application_events").Rows);
        await using var source = postgres.NewContext();
        await using var target = sqlServer.NewContext();
        Assert.Equal(await new EfListingRepository(source).FindAsync(One, Ct), await new EfListingRepository(target).FindAsync(One, Ct));
        Assert.Equal(
            (await new EfListingScoreRepository(source).GetScoresAsync(One, Ct)).Single(),
            (await new EfListingScoreRepository(target).GetScoresAsync(One, Ct)).Single(),
            ProfileScoreComparer.Instance);
        var app = await new EfTrackingRepository(target).FindApplicationAsync(One, Ct);
        Assert.Equal(ApplicationStatus.Applied, app?.Status);
        Assert.Equal("Sent CV", app?.Notes);
        Assert.Equal(Monday.AddDays(1), (await new EfCollectionRunRepository(target).GetLastStartAsync(Ct)));
    }

    [DatabaseFact]
    public async Task A_target_with_rows_is_refused_before_anything_is_written()
    {
        await postgres.ResetAsync();
        await sqlServer.ResetAsync();
        await SeedAsync(postgres);
        await using (var ctx = sqlServer.NewContext())
            await new EfCollectionRunRepository(ctx).RecordAsync(Run(Monday), Ct);

        await using var from = postgres.NewContext();
        await using var to = sqlServer.NewContext();
        var error = await Assert.ThrowsAsync<TargetNotEmptyException>(() => DatabaseCopy.CopyAsync(from, to, Ct));

        Assert.Contains("collection_runs", error.Message);
        Assert.Null(await new EfListingRepository(to).FindAsync(One, Ct));
    }

    /// <summary>More listings than a page, so the copy pages; one of each other kind of row.</summary>
    private static async Task SeedAsync(DatabaseFixture db)
    {
        var listings = Enumerable.Range(1, DatabaseCopy.PageSize + 1)
            .Select(i => new SourcedListing(ListingMapperTests.Sample(i.ToString()), $$"""{"id":"{{i}}"}"""))
            .ToArray();
        await using var ctx = db.NewContext();
        await new EfListingRepository(ctx).RecordObservationsAsync(listings, "copy @ perth", Monday, Ct);
        await new EfListingScoreRepository(ctx).SaveAsync(
            [new ListingScore(One, "tech", new FitScore(60, [new ScoreItem(ScoreRule.PositiveSignal, "C#", ScoreWhere.Title, 16)]), "h", Monday)], Ct);

        var app = JobApplication.Start(One, ApplicationStatus.Reviewed, Monday);
        app.ChangeStatus(ApplicationStatus.Applied, Monday.AddHours(1));
        app.SetNotes("Sent CV");
        var tracking = new EfTrackingRepository(ctx);
        await tracking.SaveApplicationAsync(app, Ct);

        var runs = new EfCollectionRunRepository(ctx);
        await runs.RecordAsync(Run(Monday), Ct);
        await runs.RecordAsync(Run(Monday.AddDays(1)), Ct);
    }

    private static CollectionRun Run(DateTimeOffset at) =>
        new("seek", "copy @ perth", PostedWithinDays: 2, at, at.AddMinutes(1), Found: 10, New: 3, Complete: true, Error: null);

    /// <summary>Scores hold their explanation in a list, which records compare by reference.</summary>
    private sealed class ProfileScoreComparer : IEqualityComparer<ProfileScore>
    {
        public static readonly ProfileScoreComparer Instance = new();

        public bool Equals(ProfileScore? x, ProfileScore? y) =>
            x is not null && y is not null && x.Profile == y.Profile && x.ScoredAt == y.ScoredAt
            && x.Score.Final == y.Score.Final && x.Score.Items.SequenceEqual(y.Score.Items);

        public int GetHashCode(ProfileScore obj) => obj.Profile.GetHashCode();
    }
}
