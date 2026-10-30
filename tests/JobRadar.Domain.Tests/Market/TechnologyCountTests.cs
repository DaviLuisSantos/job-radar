using JobRadar.Domain.Market;

namespace JobRadar.Domain.Tests.Market;

/// <summary>Fictional ads.</summary>
public class TechnologyCountTests
{
    private static TechnologyReport Count(params (string Title, string? Description)[] ads) => TechnologyCount.Count("2026-10", ads);

    private static int Of(TechnologyReport report, string name) => report.Technologies.SingleOrDefault(t => t.Name == name)?.Listings ?? 0;

    [Fact]
    public void Each_ad_counts_once_per_technology_and_shares_are_of_the_ads_with_text()
    {
        var report = Count(
            ("Desenvolvedor .NET", "C#, .NET 8 e SQL Server. Mais C#."),
            ("Desenvolvedor C#", "C# com Azure e RabbitMQ."),
            ("Desenvolvedor Java", null));

        Assert.Equal(3, report.Listings);
        Assert.Equal(2, report.Described);
        Assert.Equal(2, Of(report, "C#"));
        Assert.Equal(1.0, report.Technologies.Single(t => t.Name == "C#").Share);
        Assert.Equal(0, Of(report, "Java"));   // the title alone is not counted
        Assert.Equal("C#", report.Technologies[0].Name);
    }

    [Theory]
    [InlineData("Experiência com ASP.NET Core", "ASP.NET", ".NET")]
    [InlineData("Conhecimento em Node.js", "Node.js", "Java")]
    [InlineData("Banco SQL Server e T-SQL", "SQL Server", "PostgreSQL")]
    [InlineData("Arquitetura de microsserviços e mensageria", "Microsserviços", "Kafka")]
    [InlineData("Testes unitários com xUnit", "Testes automatizados", "CI/CD")]
    public void Names_are_whole_tokens(string ad, string counted, string notCounted)
    {
        var report = Count(("Vaga", ad));

        Assert.Equal(1, Of(report, counted));
        Assert.Equal(0, Of(report, notCounted));
    }

    [Fact]
    public void A_month_without_full_ads_counts_nothing()
    {
        var report = Count(("Desenvolvedor .NET", null));

        Assert.Equal(0, report.Described);
        Assert.Empty(report.Technologies);
    }
}
