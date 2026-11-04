using JobRadar.Infrastructure.Persistence.Settings;
using JobRadar.Infrastructure.Tests.Databases;

namespace JobRadar.Infrastructure.Tests.Contracts;

public abstract class SettingsRepositoryContractTests(DatabaseFixture db)
{
    private static readonly DateTimeOffset T1 = new(2026, 11, 3, 9, 0, 0, TimeSpan.FromHours(10));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task SaveAsync(string key, string json, DateTimeOffset now)
    {
        await using var ctx = db.NewContext();
        await new EfSettingsRepository(ctx).SaveAsync(key, json, now, Ct);
    }

    [DatabaseFact]
    public async Task An_empty_table_has_the_empty_revision_and_no_rows()
    {
        await db.ResetAsync();
        await using var ctx = db.NewContext();
        var repo = new EfSettingsRepository(ctx);

        Assert.Equal("0|", await repo.GetRevisionAsync(Ct));
        Assert.Empty(await repo.ListAsync(Ct));
        Assert.Null(await repo.GetAsync("Matching:Profiles", Ct));
    }

    [DatabaseFact]
    public async Task Save_inserts_then_replaces_the_value_and_the_stamp()
    {
        await db.ResetAsync();
        await SaveAsync("Matching:Profiles", """{"a":1}""", T1);
        await SaveAsync("Matching:Profiles", """{"a":2}""", T1.AddHours(1));

        await using var ctx = db.NewContext();
        var repo = new EfSettingsRepository(ctx);
        var row = await repo.GetAsync("Matching:Profiles", Ct);
        Assert.NotNull(row);
        Assert.Equal("""{"a":2}""", row.Json);
        Assert.Equal(T1.AddHours(1).ToUniversalTime(), row.UpdatedAt.ToUniversalTime());
        Assert.Single(await repo.ListAsync(Ct));
    }

    [DatabaseFact]
    public async Task Keys_that_differ_only_in_case_are_different_sections()
    {
        await db.ResetAsync();
        await SaveAsync("Market:Terms", "{}", T1);
        await SaveAsync("market:terms", "[]", T1);

        await using var ctx = db.NewContext();
        Assert.Equal(2, (await new EfSettingsRepository(ctx).ListAsync(Ct)).Count);
    }

    [DatabaseFact]
    public async Task Delete_reports_whether_the_section_existed()
    {
        await db.ResetAsync();
        await SaveAsync("Market:Terms", "{}", T1);

        await using var ctx = db.NewContext();
        var repo = new EfSettingsRepository(ctx);
        Assert.True(await repo.DeleteAsync("Market:Terms", Ct));
        Assert.False(await repo.DeleteAsync("Market:Terms", Ct));
        Assert.Equal("0|", await repo.GetRevisionAsync(Ct));
    }

    [DatabaseFact]
    public async Task Revision_is_the_count_and_the_latest_stamp_in_utc()
    {
        await db.ResetAsync();
        await SaveAsync("A", "{}", T1);
        await SaveAsync("B", "{}", T1.AddMinutes(5));

        await using var ctx = db.NewContext();
        Assert.Equal($"2|{T1.AddMinutes(5).UtcDateTime:O}", await new EfSettingsRepository(ctx).GetRevisionAsync(Ct));
    }
}

[Collection(PostgresCollection.Name)]
public sealed class PostgresSettingsRepositoryContractTests(PostgresFixture db) : SettingsRepositoryContractTests(db);

[Collection(SqlServerCollection.Name)]
public sealed class SqlServerSettingsRepositoryContractTests(SqlServerFixture db) : SettingsRepositoryContractTests(db);
