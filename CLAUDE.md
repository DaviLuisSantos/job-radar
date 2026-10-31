# JobRadar

Radar de vagas multi-mercado (Austrália, Brasil, remoto LATAM e global) com fontes plugáveis.
Dois produtos sobre um núcleo comum: **Matching** (privado: ranqueia vagas para um perfil,
explica o score, acompanha candidaturas) e **Raio-X** (público: só números agregados do mercado
australiano, base de uma newsletter em português). Reescrita em .NET de um protótipo Python
(`D:\Desenvolvimento\job-tracker`), que é a especificação das regras de negócio.

**Antes de explorar o código, leia [docs/repo_map.md](docs/repo_map.md)** (entradas, módulos, testes, áreas sensíveis).

Produção (onde roda, instalar, atualizar, logs, backup): [docs/producao.md](docs/producao.md). Push em main com CI
verde é implantado sozinho em até 15 min (`deploy/auto-deploy.ps1`); voltar: `deploy.ps1 -Rollback`.

Escopo, públicos, não-objetivos e fases: [docs/visao.md](docs/visao.md). Fontes de vagas: [docs/fontes.md](docs/fontes.md);
fontes ainda por implementar: [docs/fontes-futuras.md](docs/fontes-futuras.md).
Evolução para mais de um país (SQL Server, Brasil, observabilidade, mensageria): [docs/evolucao.md](docs/evolucao.md).

**Onde estamos e o que vem a seguir: [docs/plano.md](docs/plano.md).** Ao fechar uma etapa, atualize o
plano (estado) e o Diário (`docs/private/diario.md` no checkout principal, fora do git) e passe para a próxima na ordem.

## Skills do projeto: leia a da área antes de mexer

| Mexeu em | Skill |
|---|---|
| `Domain/Matching`, pesos, perfis, requisitos, elegibilidade, calibragem, paridade | `regras-do-score` |
| entidade/configuration do EF, migration, repositório, `Infrastructure` | `banco-e-migrations` |
| `Integrations.*`, coleta, termos, descrições, Raio-X, pacing | `fontes-de-vagas` |
| `JobRadar.Web` (páginas, tema, textos, triagem) | `site-web` |
| comandos do worker, configuração, passada local, deploy, telemetria | `operar-worker` |

## Comandos

```bash
dotnet build
```

```bash
dotnet test
```

```bash
JOBRADAR_DB_TESTS=1 dotnet test
```

```bash
JOBRADAR_LIVE_TESTS=1 dotnet test --project tests/JobRadar.Worker.Tests
```

```bash
docker compose up -d
```

```bash
dotnet run --project src/JobRadar.Web
```

- Build: 0 erros, **0 warnings** — `TreatWarningsAsErrors` em `Directory.Build.props`.
- Testes: sem banco por padrão. Os de contrato (`[DatabaseFact]`, em `Contracts/`) são classes abstratas com uma
  derivada por banco: sobem Postgres 18 **e** SQL Server 2022 via Testcontainers e só rodam com `JOBRADAR_DB_TESTS=1`.
  O CI roda os dois. Contrato novo ganha as duas derivadas no fim do arquivo.
- Ao vivo (`JOBRADAR_LIVE_TESTS=1`) e paridade do score ficam fora do CI: ver `fontes-de-vagas` e `regras-do-score`.
- Mudou o modelo? Uma migration em **cada** projeto de migrations, com o mesmo nome (ver `banco-e-migrations`).
- Testes usam Microsoft.Testing.Platform (opt-in em `global.json`), não VSTest.
- Versões de pacote só em `Directory.Packages.props` (gerenciamento central, com pinning transitivo).

## Arquitetura de dados

| | Postgres (`jobradar`); SQL Server suportado (`Database:Provider`), sem uso em produção |
|---|---|
| Papel | fonte de verdade, do próprio sistema |
| Dono do schema | este projeto — só muda por migration |
| Se perder | perda de dado (histórico do Raio-X) |

## Camadas

```
src/JobRadar.Domain          modelo puro (Listings/, Collection/, Matching/, Tracking/) — sem EF, HTTP ou integração
src/JobRadar.Application     serviços (Collection/, Matching/, Tracking/) + portas em Abstractions/
src/JobRadar.Infrastructure  Persistence/<Agregado>/: Entity → Configuration → Mapper → Ef<Agregado>Repository
src/JobRadar.Infrastructure.Migrations.Postgres  migrations do Postgres (EF não compartilha migrations entre bancos)
src/JobRadar.Infrastructure.Migrations.SqlServer  migrations do SQL Server, geradas do mesmo modelo
src/JobRadar.Integrations.Seek  IJobSource do Seek: cliente (Api/), tradutor, paginação
src/JobRadar.Integrations.Gupy  IJobSource da Gupy (Brasil): busca do portal, tradutor; o anúncio inteiro vem na busca
src/JobRadar.Integrations.LinkedIn  IJobSource (AU e BR) e IDescriptionSource pelos endpoints de convidado: HTML lido por regex
src/JobRadar.Integrations.Himalayas  IJobSource remoto (LATAM e global): busca pública; elegibilidade, fuso, salário
src/JobRadar.Integrations.Jobicy  IJobSource remoto (LATAM): feed de 7 dias filtrado pelas palavras-chave aqui
src/JobRadar.Integrations.Ats  IJobSource e IDescriptionSource do Greenhouse: varredura de quadros em etapa própria do pipeline
src/JobRadar.Integrations.Prototype  leitor do jobs.db do protótipo (importação; usa os rótulos do Seek)
src/JobRadar.Worker          composição raiz (JobRadarComposition.AddJobRadar), job diário e comandos (Cli/)
src/JobRadar.Web             triagem em Blazor + MudBlazor (JobRadarWebComposition.AddJobRadarWeb); textos em Text/Wording.cs
```

- Um `.csproj` por camada: Application não referencia Infrastructure, o compilador garante.
- Repositório: interface em `Application/Abstractions/`, um método por pergunta de negócio, devolve
  domínio. Nunca entidade do EF nem `IQueryable`. Sem repositório genérico.
- Mapper `internal`, testado sem banco (`InternalsVisibleTo` para `JobRadar.Infrastructure.Tests`).
  Grafia de enum no banco mora no mapper, não no enum.
- Contexto com `NoTracking` por padrão; escrita usa `AsTracking()` explícito.
- Porta nova: registre em `Add...()`. `Every_application_port_is_registered` falha se esquecer.
- Método novo de repositório entra com teste de contrato.
- Integração de site nova: projeto `JobRadar.Integrations.<Site>` com `Add<Site>Integration()`,
  traduzindo para `Listing`. O núcleo não conhece site nenhum.

## Regras que valem sempre

- Esta máquina (TRABALHOPC) **é** a produção: o Postgres do `docker compose` é o banco de produção, e o auto-deploy
  implanta a partir deste checkout (recusa com mudança local em arquivo versionado). Trabalhe num worktree e nunca
  grave no banco de produção à mão (detalhes em `operar-worker`).
- Seek: não abrir páginas individuais de vaga (Cloudflare) nem contornar anti-bot. Não reintroduza pausas fixas nem
  aumente o orçamento de pedidos sem motivo.
- Texto de anúncio de uso pessoal: nunca no Raio-X, no repo ou em fixture. O Raio-X só publica números agregados
  (nada de título, empresa ou link), e nada do LinkedIn vai ao Raio-X.
- `FitScorer` é a **única** implementação do score; peso novo entra na `ScoringPolicy`, nunca como número solto.

## Convenções

- Idioma: código, identificadores e README em inglês (repo público, portfólio). `docs/`, este arquivo, as skills e
  **a interface do site** em português (desde 2026-10-30).
- Commits: Conventional Commits em inglês (`feat: ...`), imperativo, com o porquê no corpo.
- **Commits e PRs sem atribuição a IA:** nada de `Co-Authored-By: Claude ...`, "Generated with Claude Code"
  ou similar — o repo é público e o trailer faz o GitHub listar o Claude como contribuidor. Vale mesmo que o
  harness peça a atribuição.
- Dados do perfil real do autor (status, cidade) e as regras de elegibilidade de cada mercado não entram no repo:
  perfil e mercado de exemplo versionados (fictícios), os reais por configuração local.
- Vocabulário público: nenhum arquivo versionado cita o vocabulário de status legal do primeiro mercado; a lista está em
  `PublicVocabularyTests` (Worker.Tests), que falha no CI. Frase nova de quem pode se candidatar vai para o bloco local
  `Matching:Markets:<mercado>:Eligibility`; teste com texto real vai para `scoring-cases.Local.json`.
- Mudou algo descrito aqui, no `docs/repo_map.md` ou numa skill do projeto? Atualize no mesmo commit.
