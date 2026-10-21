using System.Text.Json;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Market;
using JobRadar.Domain.Tracking;
using Microsoft.Data.Sqlite;

namespace JobRadar.Integrations.Prototype.Tests;

/// <summary>Each test writes a small prototype store (same DDL as the prototype) and reads it back.</summary>
public sealed class PrototypeArchiveTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"prototype-{Guid.NewGuid():N}.db");

    private const string CurrentSchema = """
        CREATE TABLE jobs (id TEXT PRIMARY KEY, source TEXT NOT NULL, profile TEXT NOT NULL DEFAULT 'default', profiles TEXT,
            title TEXT, company TEXT, location TEXT, salary TEXT, salary_min REAL, salary_max REAL, salary_period TEXT,
            salary_annual_min INTEGER, salary_annual_max INTEGER, work_type TEXT, description TEXT, url TEXT, listed_at TEXT,
            scraped_at TEXT, applied INTEGER DEFAULT 0, applied_at TEXT, status TEXT DEFAULT 'new', notes TEXT, fit_score INTEGER);
        CREATE TABLE job_terms (job_id TEXT NOT NULL, term TEXT NOT NULL, found_at TEXT, PRIMARY KEY (job_id, term));
        CREATE TABLE job_labels (job_id TEXT PRIMARY KEY, label TEXT NOT NULL, note TEXT, labeled_at TEXT);
        CREATE TABLE market_counts (snapshot_date TEXT NOT NULL, category TEXT NOT NULL, city TEXT NOT NULL, term TEXT NOT NULL DEFAULT '',
            total INTEGER, PRIMARY KEY (snapshot_date, category, city, term));
        CREATE TABLE market_listings (id TEXT NOT NULL, city TEXT NOT NULL, category TEXT NOT NULL, title TEXT, company TEXT,
            location TEXT, work_type TEXT, salary TEXT, salary_period TEXT, salary_annual_min INTEGER, salary_annual_max INTEGER,
            listed_at TEXT, first_seen TEXT NOT NULL, last_seen TEXT NOT NULL, PRIMARY KEY (id, city));
        CREATE TABLE market_runs (snapshot_date TEXT PRIMARY KEY, status TEXT NOT NULL, expected INTEGER, saved INTEGER,
            listings INTEGER, issues TEXT, ran_at TEXT);
        """;

    private void Create(string schema, string data)
    {
        using var db = new SqliteConnection($"Data Source={_path}");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = schema + data;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    [Fact]
    public async Task Reads_a_job_with_its_profiles_terms_label_and_status()
    {
        Create(CurrentSchema, """
            INSERT INTO jobs (id, source, profile, profiles, title, company, location, salary, work_type, description, url, listed_at, scraped_at, applied, applied_at, status, notes)
            VALUES ('seek_81234567', 'seek', 'trades', 'trades,fifo', 'Trade Assistant', 'Acme', 'Osborne Park, Perth WA', '$35 per hour',
                    'Contract/Temp', 'Fictional teaser | Hybrid', 'https://www.seek.com.au/job/81234567', '2026-05-27T06:04:23Z',
                    '2026-06-01 12:54:35', 1, '2026-06-03 09:00:00', 'rejected', 'they said no');
            INSERT INTO job_terms VALUES ('seek_81234567', 'australian citizen', '2026-10-01'), ('seek_81234567', 'working holiday', '2026-10-01');
            INSERT INTO job_labels VALUES ('seek_81234567', 'good', 'liked it', '2026-06-02 10:00:00');
            """);

        var data = await new PrototypeArchive().ReadAsync(_path, Ct);

        var job = Assert.Single(data.Jobs);
        var l = job.Listing.Listing;
        Assert.Equal(new ListingId("seek", "81234567"), l.Id);
        Assert.Equal(("Trade Assistant", "Acme", "$35 per hour", "Fictional teaser | Hybrid"), (l.Title, l.Company, l.SalaryText, l.Summary));
        Assert.Equal(new ListingLocation("Osborne Park", "Perth", AustralianState.WA, null), l.Location);
        Assert.Equal(WorkType.Contract, l.WorkType);
        Assert.Equal(new DateTimeOffset(2026, 5, 27, 6, 4, 23, TimeSpan.Zero), l.PostedAt);
        // SQLite datetime('now') has no zone and is UTC.
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 12, 54, 35, TimeSpan.Zero), job.FirstSeenAt);
        Assert.Equal(["trades", "fifo"], job.Profiles);
        Assert.Equal(["australian citizen", "working holiday"], job.Terms.Order());
        Assert.Equal(("rejected", true, "they said no"), (job.Status, job.Applied, job.Notes));
        Assert.Equal((Verdict.Good, "liked it"), (job.Label, job.LabelNote));

        using var payload = JsonDocument.Parse(job.Listing.RawPayload);
        Assert.Equal("job-tracker", payload.RootElement.GetProperty("importedFrom").GetString());
        Assert.Equal("seek_81234567", payload.RootElement.GetProperty("row").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Reads_market_snapshots_counts_and_listing_ranges()
    {
        Create(CurrentSchema, """
            INSERT INTO market_runs VALUES ('2026-10-01', 'ok', 30, 30, 400, NULL, '2026-10-01'), ('2026-10-02', 'partial', 30, 28, 0, 'contagem falhou: X
            listagem incompleta: Y', '2026-10-02');
            INSERT INTO market_counts VALUES ('2026-10-01', 'HVAC-R', 'Perth WA', '', 72), ('2026-10-01', 'HVAC-R', 'Perth WA', '482', 1),
                                             ('2026-10-01', 'HVAC-R', 'Perth WA', 'citizen', NULL);
            INSERT INTO market_listings (id, city, category, title, company, location, work_type, salary, listed_at, first_seen, last_seen)
            VALUES ('seek_9', 'Perth WA', 'HVAC-R', 'Installer', 'Cool Co', 'Perth WA', 'Full time', '$100k', '2026-09-01T00:00:00Z', '2026-09-02', '2026-09-20');
            """);

        var data = await new PrototypeArchive().ReadAsync(_path, Ct);

        Assert.Equal(2, data.Snapshots.Count);
        var ok = data.Snapshots[0];
        Assert.Equal((new DateOnly(2026, 10, 1), SnapshotStatus.Ok, 400), (ok.Snapshot.Date, ok.Snapshot.Status, ok.Snapshot.Listings));
        Assert.Equal([(null, 72), ("482", 1)], ok.Counts.OrderBy(c => c.Term).Select(c => (c.Term, c.Total!.Value)));
        Assert.Equal(["contagem falhou: X", "listagem incompleta: Y"], data.Snapshots[1].Snapshot.Issues.Select(i => i.Trim()));

        var listing = Assert.Single(data.MarketListings);
        Assert.Equal(("HVAC-R", "Perth WA", new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 20)),
            (listing.Listing.Category, listing.Listing.City, listing.FirstSeen, listing.LastSeen));
    }

    [Fact]
    public async Task An_old_store_without_the_later_tables_and_columns_still_reads()
    {
        // The shape of the first prototype versions: no profiles column, no terms, labels or market.
        Create("""
            CREATE TABLE jobs (id TEXT PRIMARY KEY, source TEXT, title TEXT, company TEXT, location TEXT, salary TEXT,
                work_type TEXT, description TEXT, url TEXT, listed_at TEXT, scraped_at TEXT, profile TEXT);
            """, """
            INSERT INTO jobs VALUES ('seek_1', 'seek', 'Dev', NULL, 'Perth WA', NULL, 'Full time', NULL, NULL, NULL, '2026-06-01 00:00:00', 'tech');
            INSERT INTO jobs VALUES ('broken', 'seek', 'No source prefix', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 'tech');
            """);

        var data = await new PrototypeArchive().ReadAsync(_path, Ct);

        var job = Assert.Single(data.Jobs);
        Assert.Equal(["tech"], job.Profiles);
        Assert.Null(job.Status);
        Assert.Equal(new Uri("https://www.seek.com.au/job/1"), job.Listing.Listing.Url);
        Assert.Empty(data.Snapshots);
        Assert.Empty(data.MarketListings);
    }

    [Fact]
    public async Task A_missing_file_says_so()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => new PrototypeArchive().ReadAsync(_path, Ct));
    }

    [Theory]
    [InlineData("seek_123", "seek", "123")]
    [InlineData("seek_abc_9", "seek", "abc_9")]
    public void Ids_split_on_the_first_underscore(string raw, string source, string id)
    {
        Assert.Equal(new ListingId(source, id), PrototypeArchive.ParseId(raw));
    }
}
