using JobRadar.Domain.Matching;
using JobRadar.Infrastructure.Postgres.Matching;

namespace JobRadar.Infrastructure.Tests.Postgres;

[Collection(PostgresCollection.Name)]
public class CandidateRequirementRepositoryContractTests(PostgresFixture db)
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 6, 0, 0, TimeSpan.FromHours(8));
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PostgresCandidateRequirementRepository Repo(Infrastructure.Postgres.JobRadarDbContext ctx) => new(ctx);

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
}
