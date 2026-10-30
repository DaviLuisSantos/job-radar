using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Tests.Persistence;

/// <summary>No database needed: the model and the migrations are compared in memory.</summary>
public sealed class DatabaseProviderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Postgres")]
    [InlineData(" postgres ")]
    public void Postgres_is_the_default_and_the_name_ignores_case(string? value) =>
        Assert.Equal(DatabaseProvider.Postgres, DatabaseProviders.Parse(value));

    [Theory]
    [InlineData("Oracle")]
    [InlineData("0")]
    [InlineData("7")]
    public void An_unknown_provider_fails_with_the_valid_names(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => DatabaseProviders.Parse(value));
        Assert.Contains("Postgres", error.Message);
    }

    [Fact]
    public void Every_provider_has_a_migrations_assembly_and_it_matches_the_model()
    {
        foreach (var provider in Enum.GetValues<DatabaseProvider>())
        {
            using var db = Context(provider);

            Assert.NotEmpty(db.Database.GetMigrations());
            Assert.False(db.Database.HasPendingModelChanges(), $"{provider}: the model changed without a migration.");
        }
    }

    [Fact]
    public void The_Postgres_history_keeps_its_migration_ids()
    {
        // Production records these ids in __EFMigrationsHistory: moving the migrations must not change them.
        using var db = Context(DatabaseProvider.Postgres);
        var ids = db.Database.GetMigrations().ToList();

        Assert.Equal("20261021142456_InitialCreate", ids[0]);
        Assert.Contains("20261024153158_AddCandidateExperience", ids);
    }

    private static JobRadarDbContext Context(DatabaseProvider provider) =>
        new(new DbContextOptionsBuilder<JobRadarDbContext>()
            .UseJobRadarDatabase(provider, "Host=unused;Database=unused")
            .Options);
}
