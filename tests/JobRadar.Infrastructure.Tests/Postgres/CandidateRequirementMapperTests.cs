using JobRadar.Domain.Matching;
using JobRadar.Infrastructure.Persistence.Matching;

namespace JobRadar.Infrastructure.Tests.Postgres;

public class CandidateRequirementMapperTests
{
    [Fact]
    public void Every_state_round_trips_with_a_stable_spelling()
    {
        Assert.Equal(["have", "obtainable", "missing", "out_of_reach"],
            Enum.GetValues<RequirementState>().Select(CandidateRequirementMapper.ToColumn));
        Assert.All(Enum.GetValues<RequirementState>(),
            s => Assert.Equal(s, CandidateRequirementMapper.ToState(CandidateRequirementMapper.ToColumn(s))));
        Assert.Throws<InvalidOperationException>(() => CandidateRequirementMapper.ToState("maybe"));
    }
}
