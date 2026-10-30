# Mapa do repositório

Leitura curta para se orientar antes de abrir o código. Regras completas de cada área: as skills em `.claude/skills/`.

## Pontos de entrada

| Entrada | O que faz |
|---|---|
| `src/JobRadar.Worker/Program.cs` | sem argumentos, sobe o host com o `DailyPipelineJob`; com um comando, roda `Cli/CliRunner.cs` e sai |
| `src/JobRadar.Worker/JobRadarComposition.cs` | composição raiz (`AddJobRadar`): registra portas, fontes e banco |
| `src/JobRadar.Worker/DailyPipeline.cs` | as cinco etapas da passada diária, isoladas entre si |
| `src/JobRadar.Web/Program.cs` | site de triagem (Blazor + MudBlazor), composição em `JobRadarWebComposition.cs` |
| `deploy/*.ps1` | instalação, auto-deploy (push em main com CI verde), rollback, backup, watchdog |
| `.github/workflows/ci.yml` | build + testes, incluindo os de contrato nos dois bancos |

## Pipeline diário

coleta (`ListingCollector`) → termos do texto completo (`FullTextTermService`) → descrições completas
(`DescriptionFetchService`) → score (`ListingScoringService` + `FitScorer`) → retrato do Raio-X (`MarketSnapshotService`).
Roda às `Collection:DailyAt` no fuso `Collection:TimeZone`; uma etapa que falha não impede a seguinte.

## Módulos

| Projeto | Papel |
|---|---|
| `JobRadar.Domain` | modelo puro: `Listings/`, `Collection/`, `Matching/` (score, regras de mercado), `Tracking/`, `Triage/`, `Import/`, `Market/` (Raio-X) |
| `JobRadar.Application` | serviços de caso de uso e as portas (`Abstractions/`: repositórios, `IJobSource`, `IDescriptionSource`...) |
| `JobRadar.Infrastructure` | EF Core: `Persistence/<Agregado>/` (Entity, Configuration, Mapper, repositório), `ProviderModel` |
| `JobRadar.Infrastructure.Migrations.*` | migrations do Postgres e do SQL Server, uma de cada por mudança |
| `JobRadar.Integrations.Seek` | busca, texto completo, descrição (GraphQL) e contagem do Raio-X no Seek |
| `JobRadar.Integrations.Gupy` | vagas do Brasil, anúncio inteiro já na busca |
| `JobRadar.Integrations.LinkedIn` | fonte opcional (AU e BR) por endpoints de convidado |
| `JobRadar.Integrations.Himalayas` / `.Jobicy` | vagas remotas (LATAM e global) |
| `JobRadar.Integrations.Prototype` | importação do `jobs.db` do protótipo Python |
| `JobRadar.Worker` | host, job diário e comandos de linha de comando |
| `JobRadar.Web` | triagem, página da vaga, captura de descrição, requisitos, calibragem (`Components/Pages/`) |

## Testes

| Projeto | Cobre |
|---|---|
| `JobRadar.Domain.Tests` | score, regras de mercado; paridade com o protótipo (`JOBRADAR_PARITY_FILE`) e casos locais (`JOBRADAR_LOCAL_CASES`) |
| `JobRadar.Application.Tests` | serviços com portas falsas |
| `JobRadar.Infrastructure.Tests` | mappers sem banco; contratos dos repositórios em `Contracts/` (`JOBRADAR_DB_TESTS=1`, Docker) |
| `JobRadar.Integrations.*.Tests` | tradutores e clientes de cada fonte (`Remote` = Himalayas e Jobicy) |
| `JobRadar.Worker.Tests` | composição, comandos, vocabulário público; testes ao vivo (`JOBRADAR_LIVE_TESTS=1`) |
| `JobRadar.Web.Tests` | componentes e serviços do site |

Padrão: `dotnet test` (sem banco, sem rede). Comandos completos no `CLAUDE.md`.

## Áreas sensíveis

- **Esta máquina é a produção**: o Postgres do `docker compose` é o banco real e `deploy/` implanta deste checkout.
  Trabalhe num worktree.
- `src/JobRadar.Domain/Matching/` (`FitScorer`, `ScoringPolicy`): mudança de regra quebra a paridade e pede
  `RulesVersion` nova. Skill `regras-do-score`.
- Migrations e `Infrastructure/Persistence/ProviderModel.cs`: dois bancos, collation, renomeações erradas do EF.
  Skill `banco-e-migrations`.
- `Integrations.Seek/Api/SeekPacingHandler.cs` e `Integrations.LinkedIn/Api/LinkedInPacing.cs`: orçamento contra
  anti-bot; não afrouxar. Skill `fontes-de-vagas`.
- Dados privados, fora do git: `src/JobRadar.Worker/appsettings.Local.json` (perfis, elegibilidade),
  `candidate/` (currículo e fatos), `docs/private/`. Nada disso vai para arquivo versionado.
- `PublicVocabularyTests` (Worker.Tests) barra no CI o vocabulário de status legal em arquivo versionado.
- Raio-X é público: só números agregados.

## Ler primeiro

1. `docs/plano.md` — onde estamos e o que vem a seguir.
2. `src/JobRadar.Worker/JobRadarComposition.cs` — como as peças se ligam.
3. `src/JobRadar.Worker/DailyPipeline.cs` — o fluxo do dia.
4. `src/JobRadar.Application/Abstractions/` — as portas.
5. `src/JobRadar.Domain/Matching/FitScorer.cs` — o score (o comentário da classe explica a ordem das fases).
6. `src/JobRadar.Infrastructure/Persistence/JobRadarDbContext.cs` — o modelo do banco.
