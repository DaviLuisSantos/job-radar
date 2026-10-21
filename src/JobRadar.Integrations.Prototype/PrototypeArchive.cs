using System.Globalization;
using System.Text.Json;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Import;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Market;
using JobRadar.Domain.Tracking;
using JobRadar.Integrations.Seek;
using Microsoft.Data.Sqlite;

namespace JobRadar.Integrations.Prototype;

/// <summary>
/// Reads the Python prototype store (<c>data/jobs.db</c>, SQLite). Its listings were Seek ads with Seek's
/// labels ("Osborne Park, Perth WA", "Contract/Temp"), so they are read with the Seek translator.
/// Older copies may lack tables or columns added later; whatever is missing reads as empty.
/// </summary>
internal sealed class PrototypeArchive : IPrototypeArchive
{
    public async Task<PrototypeData> ReadAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Prototype database not found: {path}", path);

        var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly };
        await using var db = new SqliteConnection(builder.ToString());
        await db.OpenAsync(ct);

        var tables = await TablesAsync(db, ct);
        var terms = tables.Contains("job_terms") ? await TermsAsync(db, ct) : [];
        var labels = tables.Contains("job_labels") ? await LabelsAsync(db, ct) : [];
        var jobs = tables.Contains("jobs") ? await JobsAsync(db, terms, labels, ct) : [];
        var snapshots = tables.Contains("market_runs") ? await SnapshotsAsync(db, tables.Contains("market_counts"), ct) : [];
        var market = tables.Contains("market_listings") ? await MarketListingsAsync(db, ct) : [];
        return new PrototypeData(jobs, snapshots, market);
    }

    // ── jobs ──────────────────────────────────────────────────────────────

    private static async Task<List<PrototypeJob>> JobsAsync(
        SqliteConnection db, Dictionary<string, List<string>> terms, Dictionary<string, (Verdict, string?, DateTimeOffset?)> labels, CancellationToken ct)
    {
        var columns = await ColumnsAsync(db, "jobs", ct);
        string Col(string name) => columns.Contains(name) ? name : $"NULL AS {name}";

        var sql = $"""
            SELECT id, title, company, location, salary, work_type, description, url, listed_at, scraped_at,
                   {Col("profile")}, {Col("profiles")}, {Col("status")}, {Col("applied")}, {Col("applied_at")}, {Col("notes")}
            FROM jobs
            """;
        var jobs = new List<PrototypeJob>();
        await using var cmd = new SqliteCommand(sql, db);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var rawId = r.GetString(0);
            if (ParseId(rawId) is not { } id)
                continue;

            var listing = new Listing(
                id,
                Text(r, 1) ?? "",
                Text(r, 2),
                SeekListingTranslator.ParseLocation(Text(r, 3)),
                SeekListingTranslator.ParseWorkType(Text(r, 5)),
                Text(r, 4),
                Instant(Text(r, 8)),
                Uri.TryCreate(Text(r, 7), UriKind.Absolute, out var url) ? url : new Uri($"https://www.seek.com.au/job/{id.SourceId}"),
                SourceCategory: null,
                Summary: Text(r, 6));

            var firstSeen = Instant(Text(r, 9)) ?? DateTimeOffset.UnixEpoch;
            var profiles = (Text(r, 11) ?? Text(r, 10) ?? "")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p != "default")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var label = labels.GetValueOrDefault(rawId);

            jobs.Add(new PrototypeJob(
                new SourcedListing(listing, Payload(r)),
                firstSeen,
                profiles,
                terms.GetValueOrDefault(rawId) ?? [],
                Text(r, 12),
                r.IsDBNull(13) ? false : r.GetInt64(13) != 0,
                Instant(Text(r, 14)),
                Text(r, 15),
                labels.ContainsKey(rawId) ? label.Item1 : null,
                label.Item2,
                label.Item3));
        }
        return jobs;
    }

    private static async Task<Dictionary<string, List<string>>> TermsAsync(SqliteConnection db, CancellationToken ct)
    {
        var terms = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        await using var cmd = new SqliteCommand("SELECT job_id, term FROM job_terms", db);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            if (!terms.TryGetValue(r.GetString(0), out var list))
                terms[r.GetString(0)] = list = [];
            list.Add(r.GetString(1));
        }
        return terms;
    }

    private static async Task<Dictionary<string, (Verdict, string?, DateTimeOffset?)>> LabelsAsync(SqliteConnection db, CancellationToken ct)
    {
        var labels = new Dictionary<string, (Verdict, string?, DateTimeOffset?)>(StringComparer.Ordinal);
        await using var cmd = new SqliteCommand("SELECT job_id, label, note, labeled_at FROM job_labels", db);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            Verdict? verdict = r.GetString(1) switch { "good" => Verdict.Good, "bad" => Verdict.Bad, _ => null };
            if (verdict is { } v)
                labels[r.GetString(0)] = (v, Text(r, 2), Instant(Text(r, 3)));
        }
        return labels;
    }

    // ── market ────────────────────────────────────────────────────────────

    private static async Task<List<PrototypeSnapshot>> SnapshotsAsync(SqliteConnection db, bool hasCounts, CancellationToken ct)
    {
        var counts = new Dictionary<string, List<MarketCount>>(StringComparer.Ordinal);
        if (hasCounts)
        {
            await using var c = new SqliteCommand("SELECT snapshot_date, category, city, term, total FROM market_counts WHERE total IS NOT NULL", db);
            await using var cr = await c.ExecuteReaderAsync(ct);
            while (await cr.ReadAsync(ct))
            {
                if (!counts.TryGetValue(cr.GetString(0), out var list))
                    counts[cr.GetString(0)] = list = [];
                var term = Text(cr, 3);
                list.Add(new MarketCount(cr.GetString(1), cr.GetString(2), string.IsNullOrEmpty(term) ? null : term, (int)cr.GetInt64(4)));
            }
        }

        var snapshots = new List<PrototypeSnapshot>();
        await using var cmd = new SqliteCommand("SELECT snapshot_date, status, expected, saved, listings, issues FROM market_runs ORDER BY snapshot_date", db);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            if (!DateOnly.TryParseExact(r.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                continue;
            SnapshotStatus? status = r.GetString(1) switch
            {
                "ok" => SnapshotStatus.Ok,
                "partial" => SnapshotStatus.Partial,
                "suspect" => SnapshotStatus.Suspect,
                _ => null,
            };
            if (status is not { } s)
                continue;
            var issues = Text(r, 5)?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [];
            snapshots.Add(new PrototypeSnapshot(
                new MarketSnapshot("seek", day, s, Int(r, 2), Int(r, 3), Int(r, 4), issues),
                counts.GetValueOrDefault(r.GetString(0)) ?? []));
        }
        return snapshots;
    }

    private static async Task<List<ImportedMarketListing>> MarketListingsAsync(SqliteConnection db, CancellationToken ct)
    {
        var listings = new List<ImportedMarketListing>();
        await using var cmd = new SqliteCommand(
            "SELECT id, city, category, title, company, location, work_type, salary, listed_at, first_seen, last_seen FROM market_listings", db);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            if (ParseId(r.GetString(0)) is not { } id
                || !DateOnly.TryParse(r.GetString(9)[..10], CultureInfo.InvariantCulture, out var first)
                || !DateOnly.TryParse(r.GetString(10)[..10], CultureInfo.InvariantCulture, out var last))
                continue;

            var listing = new Listing(id, Text(r, 3) ?? "", Text(r, 4),
                SeekListingTranslator.ParseLocation(Text(r, 5)), SeekListingTranslator.ParseWorkType(Text(r, 6)),
                Text(r, 7), Instant(Text(r, 8)), new Uri($"https://www.seek.com.au/job/{id.SourceId}"), null);
            listings.Add(new ImportedMarketListing(new MarketListing(r.GetString(2), r.GetString(1), listing), first, last));
        }
        return listings;
    }

    // ── helpers ───────────────────────────────────────────────────────────

    /// <summary>"seek_81234567" → seek:81234567. The prototype prefixed every id with its source.</summary>
    internal static ListingId? ParseId(string raw)
    {
        var i = raw.IndexOf('_');
        return i <= 0 || i == raw.Length - 1 ? null : new ListingId(raw[..i], raw[(i + 1)..]);
    }

    /// <summary>
    /// Prototype timestamps: ISO with Z from Seek ("2026-05-27T06:04:23Z") or SQLite's datetime('now'),
    /// which is UTC without a zone ("2026-06-01 12:54:35").
    /// </summary>
    internal static DateTimeOffset? Instant(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at)
            ? at
            : null;

    /// <summary>The prototype row itself, kept as the payload: there is no source JSON for these listings.</summary>
    private static string Payload(SqliteDataReader r)
    {
        var row = new Dictionary<string, object?>();
        for (var i = 0; i < r.FieldCount; i++)
            row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
        return JsonSerializer.Serialize(new { importedFrom = "job-tracker", row });
    }

    private static string? Text(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetValue(i).ToString() is { Length: > 0 } s ? s : null;

    private static int Int(SqliteDataReader r, int i) => r.IsDBNull(i) ? 0 : (int)r.GetInt64(i);

    private static async Task<HashSet<string>> TablesAsync(SqliteConnection db, CancellationToken ct)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        await using var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type = 'table'", db);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            names.Add(r.GetString(0));
        return names;
    }

    private static async Task<HashSet<string>> ColumnsAsync(SqliteConnection db, string table, CancellationToken ct)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        await using var cmd = new SqliteCommand($"PRAGMA table_info({table})", db);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            names.Add(r.GetString(1));
        return names;
    }
}
