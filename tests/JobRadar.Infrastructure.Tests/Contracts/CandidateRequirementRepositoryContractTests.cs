using JobRadar.Domain.Matching;
using JobRadar.Infrastructure.Persistence.Matching;
using JobRadar.Infrastructure.Tests.Databases;

namespace JobRadar.Infrastructure.Tests.Contracts;

public abstract class CandidateRequirementRepositoryContractTests(DatabaseFixture db)
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 6, 0, 0, TimeSpan.FromHours(8));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static EfCandidateRequirementRepository Repo(Infrastructure.Persistence.JobRadarDbContext ctx) => new(ctx);

    [DatabaseFact]
    public async Task Nothing_set_means_no_states()
    {
        await db.ResetAsync();

        await using var ctx = db.NewContext();
        Assert.Empty((await Repo(ctx).GetAsync(Ct)).States);
    }

    [DatabaseFact]
    public async Task A_state_is_stored_replaced_and_cleared()
    {
        await db.ResetAsync();

        await using (var ctx = db.NewContext())
        {
            await Repo(ctx).SetAsync("hrwl", RequirementState.Obtainable, Monday, Ct);
            await Repo(ctx).SetAsync(" HRWL ", RequirementState.Have, Monday.AddDays(1), Ct);
            await Repo(ctx).SetAsync("truck-hr", RequirementState.OutOfReach, Monday, Ct);
        }
        await using (var ctx = db.NewContext())
            Assert.Equal(
                [KeyValuePair.Create("hrwl", RequirementState.Have), KeyValuePair.Create("truck-hr", RequirementState.OutOfReach)],
                (await Repo(ctx).GetAsync(Ct)).States.OrderBy(s => s.Key, StringComparer.Ordinal));

        await using (var ctx = db.NewContext())
        {
            await Repo(ctx).SetAsync("truck-hr", null, Monday, Ct);
            await Repo(ctx).SetAsync("degree", null, Monday, Ct);   // never set: nothing to clear
        }
        await using var read = db.NewContext();
        var states = (await Repo(read).GetAsync(Ct)).States;
        Assert.Equal(RequirementState.Have, Assert.Single(states).Value);
        Assert.True(states.ContainsKey("HRWL"));                    // ids compare without case
    }

    [DatabaseFact]
    public async Task Years_of_experience_are_stored_per_profile_rounded_to_a_tenth_and_forgotten()
    {
        await db.ResetAsync();

        await using (var ctx = db.NewContext())
        {
            await Repo(ctx).SetExperienceAsync("Tech", 7.96, Monday, Ct);
            await Repo(ctx).SetExperienceAsync("fifo", 0.5, Monday, Ct);
            await Repo(ctx).SetExperienceAsync("fifo", 1, Monday.AddDays(1), Ct);
        }
        await using (var ctx = db.NewContext())
        {
            var years = (await Repo(ctx).GetAsync(Ct)).ExperienceYears;
            Assert.Equal([("fifo", 1.0), ("tech", 8.0)], years.OrderBy(y => y.Key, StringComparer.Ordinal).Select(y => (y.Key, y.Value)));
            Assert.True(years.ContainsKey("TECH"));
        }

        await using (var ctx = db.NewContext())
            await Repo(ctx).SetExperienceAsync("fifo", null, Monday, Ct);
        await using var read = db.NewContext();
        Assert.Equal(["tech"], (await Repo(read).GetAsync(Ct)).ExperienceYears.Keys);
    }
}

[Collection(PostgresCollection.Name)]
public sealed class PostgresCandidateRequirementRepositoryContractTests(PostgresFixture db) : CandidateRequirementRepositoryContractTests(db);

[Collection(SqlServerCollection.Name)]
public sealed class SqlServerCandidateRequirementRepositoryContractTests(SqlServerFixture db) : CandidateRequirementRepositoryContractTests(db);
