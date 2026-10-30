using System.Text.RegularExpressions;

namespace JobRadar.Domain.Market;

/// <summary>A technology an ad may ask for, by the names ads give it.</summary>
/// <param name="Name">As the report shows it.</param>
/// <param name="Pattern">Matched on the lower-cased title and ad, as whole tokens.</param>
public sealed record Technology(string Name, string Group, Regex Pattern);

/// <summary>How many of the month's listings ask for one technology.</summary>
public sealed record TechnologyShare(string Name, string Group, int Listings, double Share);

/// <summary>The technologies the listings of a month ask for, most asked first. Personal use: counts only.</summary>
public sealed record TechnologyReport(string Month, int Listings, int Described, IReadOnlyList<TechnologyShare> Technologies);

/// <summary>
/// Counts the technologies ads ask for, to see the market from the ads instead of by hand. Each listing counts once
/// per technology, however often it names it. Only listings with their full ad are counted: a title alone names one
/// stack at most, and would make the shares lie.
/// </summary>
public static class TechnologyCount
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    // Lookarounds instead of \b, which fails on tokens that start or end with a symbol (C#, .NET, Node.js).
    private static Technology T(string name, string group, params string[] names) =>
        new(name, group, new Regex(@"(?<![\w.])(?:" + string.Join("|", names) + @")(?![\w#+])", Options));

    /// <summary>What the author's interview plan tracks: the stack, the front end, data, cloud, messaging and practice.</summary>
    public static IReadOnlyList<Technology> Catalog { get; } =
    [
        T("C#", "Linguagem", @"c\#"),
        T(".NET", "Linguagem", @"\.net(?:\s*(?:core|framework|\d+))?", "dotnet"),
        T("ASP.NET", "Linguagem", @"asp\.net(?:\s*core|\s*mvc)?"),
        T("Java", "Linguagem", "java"),
        T("Python", "Linguagem", "python"),
        T("Go", "Linguagem", "golang", @"go\s+lang"),
        T("PHP", "Linguagem", "php"),
        T("Node.js", "Linguagem", @"node(?:\.?js)?"),
        T("TypeScript", "Front-end", "typescript"),
        T("React", "Front-end", @"react(?:\.?js)?"),
        T("Angular", "Front-end", "angular"),
        T("Vue", "Front-end", @"vue(?:\.?js)?"),
        T("Blazor", "Front-end", "blazor"),
        T("SQL Server", "Dados", @"sql\s*server", "mssql", @"t-?sql"),
        T("PostgreSQL", "Dados", "postgres(?:ql)?"),
        T("MySQL", "Dados", "mysql"),
        T("MongoDB", "Dados", "mongo(?:db)?"),
        T("Redis", "Dados", "redis"),
        T("Entity Framework", "Dados", @"entity\s*framework(?:\s*core)?", @"ef\s*core"),
        T("Dapper", "Dados", "dapper"),
        T("Azure", "Nuvem", "azure"),
        T("AWS", "Nuvem", "aws", @"amazon\s+web\s+services"),
        T("GCP", "Nuvem", "gcp", @"google\s+cloud"),
        T("Docker", "Nuvem", "docker"),
        T("Kubernetes", "Nuvem", "kubernetes", "k8s"),
        T("RabbitMQ", "Mensageria", "rabbitmq", @"rabbit\s*mq"),
        T("Kafka", "Mensageria", "kafka"),
        T("Service Bus", "Mensageria", @"service\s*bus"),
        T("Mensageria", "Mensageria", "mensageria", "messaging", @"filas?\s+de\s+mensagens"),
        T("Microsserviços", "Arquitetura", @"micro-?s?servi[çc]os", "microservices"),
        T("Testes automatizados", "Prática", @"testes?\s+(?:unit[áa]rios?|automatizados?|de\s+integra[çc][ãa]o)", "xunit", "nunit", "tdd"),
        T("CI/CD", "Prática", @"ci\s*/\s*cd", "github\\s+actions", @"azure\s+devops", "jenkins"),
        T("Observabilidade", "Prática", "observabilidade", "observability", "opentelemetry", "grafana", "prometheus", @"application\s+insights"),
        T("Inglês", "Idioma", @"ingl[êe]s", "english"),
    ];

    public static TechnologyReport Count(string month, IReadOnlyCollection<(string Title, string? Description)> listings)
    {
        var described = listings.Where(l => !string.IsNullOrWhiteSpace(l.Description))
            .Select(l => $"{l.Title} {l.Description}".ToLowerInvariant())
            .ToList();
        var shares = Catalog
            .Select(t => (t, Count: described.Count(text => t.Pattern.IsMatch(text))))
            .Where(c => c.Count > 0)
            .OrderByDescending(c => c.Count).ThenBy(c => c.t.Name, StringComparer.Ordinal)
            .Select(c => new TechnologyShare(c.t.Name, c.t.Group, c.Count, (double)c.Count / described.Count))
            .ToList();
        return new TechnologyReport(month, listings.Count, described.Count, shares);
    }
}
