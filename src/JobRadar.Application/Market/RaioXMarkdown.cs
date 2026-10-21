using System.Globalization;
using System.Text;
using JobRadar.Domain.Market;

namespace JobRadar.Application.Market;

/// <summary>
/// The monthly report as Markdown, in Portuguese: the newsletter's language. Only aggregates
/// reach this text; no listing, title or company is ever written.
/// </summary>
public static class RaioXMarkdown
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly Dictionary<string, string> TermLabels = new(StringComparer.Ordinal)
    {
        ["sponsorship"] = "citam \"sponsorship\"",
        ["482"] = "citam \"482\"",
        ["working_rights"] = "citam \"working rights\"",
        ["citizen"] = "citam \"citizen\"",
    };

    public static string Render(MarketReport report, string sourceName = "Seek")
    {
        var md = new StringBuilder();
        md.AppendLine(Pt, $"# Raio-X do mercado australiano — {report.Month}").AppendLine();
        md.Append(Pt, $"Fonte: {sourceName} (subclassificações oficiais). Média de {report.DaysCovered} retrato(s) diário(s) no mês");
        md.AppendLine(report.Skipped.Count > 0
            ? string.Create(Pt, $"; {report.Skipped.Count} descartado(s) por coleta incompleta ou suspeita.")
            : ".").AppendLine();

        foreach (var category in report.Categories)
        {
            md.AppendLine(Pt, $"## {category.Category}").AppendLine();
            foreach (var city in category.Cities)
            {
                md.AppendLine(Pt, $"### {city.City}").AppendLine();
                md.Append(Pt, $"- **Vagas abertas (média diária):** {Count(city.AverageOpen)}");
                md.AppendLine(city.ChangeVsPreviousMonth is { } change ? string.Create(Pt, $" ({Signed(change)} vs. mês anterior)") : "");

                foreach (var m in city.Mentions)
                    md.AppendLine(Pt, $"- {TermLabels.GetValueOrDefault(m.Term, m.Term)}: {Count(m.Average)} ({Share(m.Share)})");

                if (city.Listings is { } l)
                {
                    md.AppendLine(Pt, $"- Anúncios vistos no mês: {l.Seen:N0} → {l.Unique:N0} únicos ({l.Seen - l.Unique:N0} repetidos)");
                    md.AppendLine(Pt, $"- Publicados no mês: {l.PostedInMonth:N0}");
                    if (l.Salary is { } s)
                    {
                        md.AppendLine(Pt,
                            $"- Salário anual (ponto médio da faixa anunciada): mediana {Money(s.Median)}, faixa típica {Money(s.P25)}–{Money(s.P75)} ({s.WithSalary} de {l.Unique} anúncios informam salário, {Share((double)s.WithSalary / l.Unique)})");
                    }
                }
                md.AppendLine();
            }
        }

        md.AppendLine("## Metodologia").AppendLine();
        md.AppendLine("- **Vagas abertas**: total de anúncios ativos na subclassificação, média dos retratos diários do mês.");
        md.AppendLine("- **Menções**: anúncios cujo texto contém o termo, segundo a busca da própria fonte. Mede menção, não oferta — \"no sponsorship\" também conta em \"sponsorship\".");
        md.AppendLine("- **Repetidos**: mesmo título e empresa na mesma cidade, com IDs diferentes.");
        md.AppendLine("- **Salário**: só anúncios que informam valor; por hora × 2.080 h, por dia × 260 dias.");
        md.AppendLine("- Retratos com contagem falha, listagem incompleta ou variação suspeita ficam fora da média.");
        md.AppendLine("- Dados agregados; nenhuma vaga individual é publicada.");
        return md.ToString();
    }

    private static string Count(double value) => value.ToString("N0", Pt);

    private static string Signed(double change) => (change >= 0 ? "+" : "") + (change * 100).ToString("N0", Pt) + "%";

    private static string Share(double? share) => share is { } s ? (s * 100).ToString("N1", Pt) + "%" : "—";

    private static string Money(double value) => "$" + (value / 1000).ToString("N0", Pt) + "k";
}
