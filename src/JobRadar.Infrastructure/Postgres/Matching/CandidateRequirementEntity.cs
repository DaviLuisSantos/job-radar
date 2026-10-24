using JobRadar.Domain.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Postgres.Matching;

/// <summary>Row of <c>candidate_requirements</c>: the candidate's state on one catalog item.</summary>
public class CandidateRequirementEntity
{
    public string ItemId { get; set; } = "";
    public string State { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

public class CandidateRequirementEntityConfiguration : IEntityTypeConfiguration<CandidateRequirementEntity>
{
    public void Configure(EntityTypeBuilder<CandidateRequirementEntity> b)
    {
        b.ToTable("candidate_requirements");
        b.HasKey(e => e.ItemId);

        b.Property(e => e.ItemId).HasColumnName("item_id").HasMaxLength(64);
        b.Property(e => e.State).HasColumnName("state").HasMaxLength(16);
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}

/// <summary>Row of <c>candidate_experience</c>: the candidate's years of experience in one profile's line of work.</summary>
public class CandidateExperienceEntity
{
    public string Profile { get; set; } = "";
    public decimal Years { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class CandidateExperienceEntityConfiguration : IEntityTypeConfiguration<CandidateExperienceEntity>
{
    public void Configure(EntityTypeBuilder<CandidateExperienceEntity> b)
    {
        b.ToTable("candidate_experience");
        b.HasKey(e => e.Profile);

        b.Property(e => e.Profile).HasColumnName("profile").HasMaxLength(64);
        b.Property(e => e.Years).HasColumnName("years").HasPrecision(4, 1);
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}

/// <summary>Spelling of requirement states in the database.</summary>
internal static class CandidateRequirementMapper
{
    internal static string ToColumn(RequirementState state) => state switch
    {
        RequirementState.Have => "have",
        RequirementState.Obtainable => "obtainable",
        RequirementState.Missing => "missing",
        RequirementState.OutOfReach => "out_of_reach",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    internal static RequirementState ToState(string column) => column switch
    {
        "have" => RequirementState.Have,
        "obtainable" => RequirementState.Obtainable,
        "missing" => RequirementState.Missing,
        "out_of_reach" => RequirementState.OutOfReach,
        _ => throw new InvalidOperationException($"Unknown requirement state '{column}'."),
    };
}
