using JobRadar.Application.Market;
using JobRadar.Domain.Market;

namespace JobRadar.Application.Tests.Market;

public class RaioXMarkdownTests
{
    private static MarketReport Report(IReadOnlyList<MarketSnapshot>? skipped = null) => new("2026-10", 18, skipped ?? [],
    [
        new CategoryReport("HVAC-R",
        [
            new CityReport("Perth WA", 1234.4, 0.123, [new Mention("fifo", 61.7, 0.05)],
                new ListingStats(400, 350, 120, new SalaryStats(95_000, 80_000, 110_000, 140))),
            new CityReport("All Australia", 9000, null, [], null),
        ]),
    ]);

    [Fact]
    public void Writes_aggregates_in_portuguese()
    {
        var md = RaioXMarkdown.Render(Report());

        Assert.Contains("# Raio-X do mercado australiano — 2026-10", md);
        Assert.Contains("Média de 18 retrato(s) diário(s) no mês.", md);
        Assert.Contains("## HVAC-R", md);
        Assert.Contains("- **Vagas abertas (média diária):** 1.234 (+12% vs. mês anterior)", md);
        Assert.Contains("- citam \"fifo\": 62 (5,0%)", md);
        Assert.Contains("- Anúncios vistos no mês: 400 → 350 únicos (50 repetidos)", md);
        Assert.Contains("mediana $95k, faixa típica $80k–$110k (140 de 350 anúncios informam salário, 40,0%)", md);
        Assert.Contains("- **Vagas abertas (média diária):** 9.000\n", md.Replace("\r\n", "\n"));
        Assert.Contains("## Metodologia", md);
    }

    [Fact]
    public void A_term_shows_its_configured_phrase()
    {
        var md = RaioXMarkdown.Render(Report(), phrases: new Dictionary<string, string> { ["fifo"] = "fly-in fly-out" });

        Assert.Contains("- citam \"fly-in fly-out\": 62 (5,0%)", md);
    }

    [Fact]
    public void Says_how_many_snapshots_were_left_out()
    {
        var md = RaioXMarkdown.Render(Report([new MarketSnapshot("seek", new DateOnly(2026, 10, 3), SnapshotStatus.Partial, 30, 28, 0, ["x"])]));

        Assert.Contains("; 1 descartado(s) por coleta incompleta ou suspeita.", md);
    }
}
