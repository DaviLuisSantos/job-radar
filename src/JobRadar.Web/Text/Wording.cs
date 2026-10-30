using System.Globalization;
using JobRadar.Application.Tracking;
using JobRadar.Domain.Listings;
using JobRadar.Domain.Market;
using JobRadar.Domain.Matching;
using JobRadar.Domain.Tracking;

namespace JobRadar.Web.Text;

/// <summary>
/// Every user-facing word about scores and statuses. The domain explains a score with typed rules;
/// this is the only place that turns them into prose, so a translation starts and ends here.
/// </summary>
public static class Wording
{
    // Navigation and the frame around every page.
    public const string Menu = "Menu";
    public const string Markets = "Mercados";
    public const string Settings = "Ajustes";
    public const string CapturePage = "Captura";
    public const string AboutYouPage = "Você";
    public const string RequirementsPage = "Requisitos";
    public const string CalibrationPage = "Calibragem";
    public const string StatusPage = "Status";
    public const string SomethingWentWrong = "Algo deu errado.";
    public const string Reload = "Recarregar";

    public static string ProfilesOf(string market) => $"Perfis de {market}";

    // The "Today" page.
    public const string Today = "Hoje";
    public const string NothingNew = "Nada novo desde a última visita.";
    public const string NoChanges = "Nenhuma candidatura mudou.";
    public const string ApplicationsMoved = "Candidaturas";

    public static string NewListings(int count) => count == 1 ? "1 vaga nova" : $"{count.ToString("N0", Culture)} vagas novas";

    public static string SinceVisit(DateTimeOffset since, bool firstVisit) =>
        firstVisit ? "Primeira visita: o que chegou nos últimos 7 dias." : $"Desde a sua última visita, em {When(since)}.";

    public static string NewInMarket(string market, int count) => count == 0 ? market : $"{market} ({count.ToString("N0", Culture)})";

    public static string StatusMove(ApplicationStatus? from, ApplicationStatus to) =>
        from is { } f ? $"{Status(f)} → {Status(to)}" : Status(to);

    /// <summary>
    /// pt-BR for everything shown: "1.234", "0,5", "21 out 2026". The abbreviated month and day names lose
    /// their trailing dot ("out." becomes "out"), which reads as clutter in a table cell.
    /// </summary>
    public static readonly CultureInfo Culture = BuildCulture();

    private static CultureInfo BuildCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("pt-BR").Clone();
        var format = culture.DateTimeFormat;
        format.AbbreviatedMonthNames = [.. format.AbbreviatedMonthNames.Select(n => n.TrimEnd('.'))];
        format.AbbreviatedMonthGenitiveNames = [.. format.AbbreviatedMonthGenitiveNames.Select(n => n.TrimEnd('.'))];
        format.AbbreviatedDayNames = [.. format.AbbreviatedDayNames.Select(n => n.TrimEnd('.'))];
        return CultureInfo.ReadOnly(culture);
    }

    public static string Describe(ScoreItem item) => item.Rule switch
    {
        ScoreRule.CitizenshipRequired => $"Exige cidadania ou PR: “{item.Detail}”",
        ScoreRule.WorkRightsIncompatible => $"Exige direito de trabalho que o visto working holiday não dá: “{item.Detail}”",
        ScoreRule.IdentifiedRole => "Vaga reservada a candidatos aborígenes e das ilhas do Estreito de Torres",
        ScoreRule.LeadershipTitle => $"Título de liderança: {item.Detail}",
        ScoreRule.SeniorTitle => $"Título sênior: {item.Detail}",
        ScoreRule.LocationExcluded => $"Local que este perfil descarta: {item.Detail}",
        ScoreRule.TitleExcluded => $"Palavra do título que este perfil descarta: {item.Detail}",
        ScoreRule.OnSiteElsewhere => $"Presencial fora de onde este perfil aceita: {item.Detail}",
        ScoreRule.RegionRestricted => $"Só para quem mora em outro lugar: “{item.Detail}”",
        ScoreRule.OtherRemoteMarket => $"É do outro mercado remoto (aceita: {item.Detail})",
        ScoreRule.Engagement => $"Regime: {EngagementName(item.Detail)}",
        ScoreRule.PayAtTarget => $"Salário no alvo do perfil: {item.Detail}",
        ScoreRule.PayBelowFloor => $"Salário abaixo do piso do perfil: {item.Detail}",
        ScoreRule.PayUnreadable => $"Salário que não deu para comparar: {item.Detail}",
        ScoreRule.TimeZoneOutOfReach => $"Fuso fora do alcance do Brasil: {TimeZones(item.Detail)}",
        ScoreRule.SeniorPenalty => "Título sênior",
        ScoreRule.PositiveSignal => $"Sinal do perfil “{item.Detail}”",
        ScoreRule.NegativeSignal => $"Sinal negativo “{item.Detail}”",
        ScoreRule.SponsorshipMentioned => "Cita patrocínio de visto",
        ScoreRule.SponsorshipDenied => "Diz que não há patrocínio de visto",
        ScoreRule.Remote => "Remota",
        ScoreRule.Hybrid => "Híbrida",
        ScoreRule.Fifo => "FIFO",
        ScoreRule.EntryLevel => $"Nível inicial: “{item.Detail}”",
        ScoreRule.WorkingHolidayFriendly => $"Aceita quem está de working holiday: “{item.Detail}”",
        ScoreRule.FlexibleWorkType => $"{WorkTypeLabel(item.Detail)} cabe nos seis meses por empregador",
        ScoreRule.PreferredLocation => $"Local preferido: {item.Detail}",
        ScoreRule.RequirementHeld => $"Pede algo que você tem: “{item.Detail}”",
        ScoreRule.Compression => "Nota comprimida acima de 80 para manter as melhores em ordem",
        ScoreRule.FullTextCitizenship => $"O anúncio inteiro cita “{item.Detail}”",
        ScoreRule.FullTextWhvWithResidency => $"O anúncio inteiro cita “{item.Detail.Replace(" + ", "” e “", StringComparison.Ordinal)}”: costuma recusar o visto working holiday",
        ScoreRule.FullTextAmbiguousRights => $"O anúncio inteiro pede “{item.Detail}” (pode excluir vistos)",
        ScoreRule.FullTextTwelveMonthRights => $"O anúncio inteiro pede “{item.Detail}”",
        ScoreRule.FullTextCommission => $"O anúncio inteiro cita “{item.Detail}”",
        ScoreRule.FullTextTruckLicence => $"O anúncio inteiro pede carteira de caminhão: “{item.Detail}”",
        ScoreRule.FullTextExperience => $"O anúncio inteiro pede experiência: “{item.Detail}”",
        ScoreRule.FullTextQualification => $"O anúncio inteiro pede uma formação: “{item.Detail}”",
        ScoreRule.FullTextLicence => $"O anúncio inteiro pede uma licença ou ticket: “{item.Detail}”",
        ScoreRule.Apprenticeship => "Apprenticeship formal: costuma exigir PR e dura anos",
        ScoreRule.LongContract => $"Mais de seis meses por empregador: “{item.Detail}”",
        ScoreRule.CommissionOnly => $"Só comissão: “{item.Detail}”",
        ScoreRule.EnglishRequired => $"Inglês como condição: “{item.Detail}”",
        ScoreRule.TruckLicence => $"Carteira de caminhão: “{item.Detail}”",
        ScoreRule.NoSponsorship => "Sem patrocínio de visto confirmado",
        ScoreRule.Aging => $"Publicada há {item.Detail} dias",
        ScoreRule.Stale => $"Publicada há {item.Detail} dias, provavelmente encerrada",
        ScoreRule.QualificationRequired => $"Exige formação: {item.Detail}",
        ScoreRule.Unknown => $"Regra de uma versão anterior: “{item.Detail}”",
        _ => $"{item.Rule}: {item.Detail}",
    };

    public static string Where(ScoreWhere where) => where switch
    {
        ScoreWhere.Title => "título",
        ScoreWhere.Body => "resumo",
        ScoreWhere.Text => "texto",
        ScoreWhere.FullText => "anúncio inteiro",
        ScoreWhere.WorkType => "tipo de contrato",
        ScoreWhere.Location => "local",
        ScoreWhere.PostedDate => "data",
        ScoreWhere.Description => "anúncio capturado",
        _ => "",
    };

    public static string Points(int points) => points switch
    {
        > 0 => $"+{points.ToString(Culture)}",
        _ => points.ToString(Culture),
    };

    public static string Status(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Reviewed => "Vista",
        ApplicationStatus.Dismissed => "Descartada",
        ApplicationStatus.Applied => "Candidatei",
        ApplicationStatus.Interview => "Entrevista",
        ApplicationStatus.Offer => "Proposta",
        ApplicationStatus.Declined => "Recusada pela empresa",
        _ => status.ToString(),
    };

    public static string Origin(DescriptionOrigin origin) => origin switch
    {
        DescriptionOrigin.Browser => "da página do anúncio",
        DescriptionOrigin.Pasted => "colado",
        DescriptionOrigin.Source => "buscado na fonte",
        _ => origin.ToString(),
    };

    public static string State(RequirementState state) => state switch
    {
        RequirementState.Have => "Tenho",
        RequirementState.Obtainable => "Consigo tirar",
        RequirementState.Missing => "Não tenho",
        RequirementState.OutOfReach => "Fora de alcance",
        _ => state.ToString(),
    };

    /// <summary>A catalog requirement in plain words: what it is and how one gets it. Unknown ids show as they are.</summary>
    public static (string Title, string Hint) Requirement(string id) => id switch
    {
        "hrwl" => ("Licença de trabalho de alto risco (HRWL)", "Empilhadeira, plataforma elevatória, dogging e afins: um curso de poucos dias."),
        "voc" => ("Verificação de competência (VOC)", "Prova de que você opera uma máquina específica, em geral avaliada no próprio trabalho."),
        "truck-mr" => ("Carteira de caminhão MR (rígido médio)", "Exige antes um ano ou mais de carteira de carro definitiva."),
        "truck-hr" => ("Carteira de caminhão HR (rígido pesado)", "Exige antes um ano ou mais de carteira de carro definitiva; inclui a MR."),
        "truck-hc" => ("Carteira de caminhão HC (combinação pesada)", "Exige antes um ano ou mais de HR; inclui a HR e a MR."),
        "truck-mc" => ("Carteira de caminhão MC (combinação múltipla)", "Road trains; exige antes a HC; inclui todas as classes abaixo."),
        "trade-qualification" => ("Certificado de ofício (Cert III, trade qualified)", "Uma qualificação de ofício australiana; curso de fora só conta depois de avaliado."),
        "degree" => ("Diploma universitário", "Pedido como “degree in”, “tertiary qualification”."),
        "refrigerant-licence" => ("Licença para manusear refrigerante (ARCtick)", "Exige o ofício de refrigeração mais tempo supervisionado."),
        "electrical-licence" => ("Licença de eletricista", "Exige o ofício de eletricista mais tempo supervisionado."),
        "experience-years" => ("Anos de experiência, quando o anúncio não dá número", "Com número, decidem os seus anos, acima."),
        "experience-proven" => ("Experiência comprovada em função parecida", "“Proven” ou “demonstrated experience”."),
        "experience-operating" => ("Experiência operando máquinas", "“Experience operating” escavadeiras, rolos compactadores e afins."),
        _ => (id, ""),
    };

    /// <summary>The tooltip of a fact label: what kind of fact it is and where it came from.</summary>
    public static string FactTitle(FactKind kind) => kind switch
    {
        FactKind.Pay => "Pagamento, como o anúncio diz",
        FactKind.Travel => "Como você chega ao local",
        FactKind.Roster => "Escala: dias ou semanas de trabalho e de folga",
        FactKind.Accommodation => "Acomodação oferecida",
        FactKind.Meals => "Refeições oferecidas",
        FactKind.Flights => "Passagens pagas pelo empregador",
        FactKind.Start => "Data de início",
        FactKind.Shift => "Turnos",
        FactKind.Duration => "Duração do contrato",
        _ => kind.ToString(),
    } + " (lido do anúncio)";

    /// <summary>Short labels for the four states, as buttons.</summary>
    public static string StateButton(RequirementState state) => state switch
    {
        RequirementState.Have => "Tenho",
        RequirementState.Obtainable => "Consigo tirar",
        RequirementState.Missing => "Não tenho",
        RequirementState.OutOfReach => "Fora de alcance",
        _ => state.ToString(),
    };

    public static string Years(double years) =>
        years == 1 ? "1 ano" : $"{years.ToString("0.#", Culture)} anos";

    /// <summary>A quick-pick button: the top option reads "10+".</summary>
    public static string YearsShort(double years) => years >= 10 ? "10+" : years.ToString("0.#", Culture);

    public static string Listings(int count) =>
        count == 1 ? "1 vaga" : $"{Number(count)} vagas";

    /// <summary>A count for display: "1.234".</summary>
    public static string Number(int count) => count.ToString("N0", Culture);

    public static string Kind(RequirementKind kind) => kind switch
    {
        RequirementKind.Experience => "Experiência",
        RequirementKind.Qualification => "Formação",
        RequirementKind.Licence => "Licença ou ticket",
        _ => kind.ToString(),
    };

    public static string Verdict(Verdict verdict) => verdict == Domain.Tracking.Verdict.Good ? "Boa" : "Ruim";

    public static string Snapshot(SnapshotStatus status) => status switch
    {
        SnapshotStatus.Ok => "ok",
        SnapshotStatus.Partial => "parcial",
        SnapshotStatus.Suspect => "suspeito",
        _ => status.ToString(),
    };

    public static string WorkType(WorkType workType) =>
        ScoringInput.FormatWorkType(workType) is { } label ? WorkTypeLabel(label) : "";

    /// <summary>The Seek work type labels the domain formats, in Portuguese; any other label shows as it is.</summary>
    private static string WorkTypeLabel(string label) => label switch
    {
        "Full time" => "Tempo integral",
        "Part time" => "Meio período",
        "Casual/Vacation" => "Casual/temporário",
        "Contract/Temp" => "Contrato/temporário",
        _ => label,
    };

    /// <summary>The engagement as the score item names it ("Contractor"), in Portuguese.</summary>
    public static string EngagementName(string engagement) => Enum.TryParse<Engagement>(engagement, out var e) ? EngagementName(e) : engagement;

    public static string EngagementName(Engagement engagement) => engagement switch
    {
        Engagement.Contractor => "contractor (PJ)",
        Engagement.Eor => "EOR (CLT por uma empresa terceira)",
        Engagement.Direct => "contratação direta",
        _ => "não informado",
    };

    /// <summary>"UTC-5 to UTC-2" as "fuso UTC-5 a UTC-2".</summary>
    public static string TimeZones(string window) => $"fuso {window.Replace(" to ", " a ", StringComparison.Ordinal)}";

    /// <summary>Who may apply to a remote job, in Portuguese where it is a word, as the board wrote it otherwise.</summary>
    public static string Eligibility(string eligibility) => RemoteEligibility.Classify(eligibility) switch
    {
        EligibilityScope.Worldwide => "Aberta ao mundo",
        _ => eligibility.Length > 40 ? $"Aceita: {eligibility[..37].TrimEnd()}…" : $"Aceita: {eligibility}",
    };

    public static string Arrangement(WorkArrangement arrangement) => arrangement switch
    {
        WorkArrangement.Remote => "Remota",
        WorkArrangement.Hybrid => "Híbrida",
        WorkArrangement.OnSite => "Presencial",
        _ => "",
    };

    public static string Excluded(int count) => count == 1 ? "1 excluída" : $"{count.ToString("N0", Culture)} excluídas";

    public const string ShowExcluded = "Ver as excluídas e o motivo";
    public const string BackToOpen = "Voltar às vagas abertas";
    public const string ExcludedReasonUnknown = "Motivo não registrado";

    /// <summary>
    /// What went wrong, in Portuguese: the application throws typed exceptions with English messages for logs, and the
    /// site words them here. An unexpected one shows its type only: its message is English and may be long.
    /// </summary>
    public static string Error(Exception ex) => ex switch
    {
        ListingNotFoundException nf => $"A vaga {nf.Id} nunca foi coletada.",
        DescriptionTooShortException => $"O texto é curto demais para ser um anúncio (menos de {ListingDescription.MinLength} caracteres).",
        _ => $"Erro inesperado ({ex.GetType().Name}).",
    };

    public static string Location(ListingLocation location) =>
        location.Country == Country.Remote && ScoringInput.FormatLocation(location) == "Remote"
            ? "Remoto"
            : ScoringInput.FormatLocation(location) ?? "Local não informado";

    /// <summary>A stored instant (UTC) in the local time of the machine running the site.</summary>
    public static string When(DateTimeOffset at) => at.ToLocalTime().ToString("d MMM yyyy HH:mm", Culture);

    /// <summary>A day as stored, without moving it to local time: "21 out 2026".</summary>
    public static string Date(DateTimeOffset at) => at.ToString("d MMM yyyy", Culture);

    /// <summary>First line of <paramref name="text"/>, cut at <paramref name="max"/> characters; a stack trace stays out of a table cell.</summary>
    public static string Shorten(string? text, int max)
    {
        var line = text?.Split('\n', 2)[0].TrimEnd() ?? "";
        return line.Length <= max ? line : string.Concat(line.AsSpan(0, max - 1).TrimEnd(), "…");
    }

    /// <summary>"hoje", "3d", "5sem": short enough for a table column.</summary>
    public static string Age(DateTimeOffset? posted, DateTimeOffset now)
    {
        if (posted is not { } p)
            return "";
        var days = (int)(now - p).TotalDays;
        return days switch
        {
            < 1 => "hoje",
            < 14 => $"{days}d",
            _ => $"{days / 7}sem",
        };
    }

    /// <summary>Score band used for colour: excluded, low, fair, good, strong.</summary>
    public static string Band(int score) => score switch
    {
        0 => "excluded",
        < 50 => "low",
        < 65 => "fair",
        < 80 => "good",
        _ => "strong",
    };
}
