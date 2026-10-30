# Evolução — JobRadar AU → JobRadar (out/2026)

> Plano de implementação de `JobRadar-plano-de-evolucao.md` (vault), conferido contra o código em 2026-10-29.
> Uma fase por vez; cada fase só fecha com o critério de pronto. A passada diária da Austrália não para.
> Ao fechar uma fase: ✅ com a data aqui, Diário em [plano.md](plano.md), e as atualizações do vault (§8 do original).

## Estado

| # | Fase | PRs | Estado |
|---|---|---|---|
| 0 | Rename e decisões de escopo | 1 | ✅ 2026-10-29 (PR 1; passada seguinte não conferida antes de seguir) |
| 1 | SQL Server ao lado do Postgres | 5 | 🔨 |
| 2 | País BR + Gupy | 5 | ⏳ |
| 3 | Observabilidade | 3 | ⏳ |
| 4 | Mensageria | 4 | ⏳ (só com 2+ fontes rodando) |

## O que a auditoria mudou no plano original

**Fase 1 é menor do que o original previa.** A camada de dados quase não tem nada exclusivo do Postgres:

| Item do original | O que existe hoje | Ação |
|---|---|---|
| `jsonb` | 2 colunas: `listing_payloads.payload` e `listing_scores.explanation`. Nenhuma consulta filtra dentro do JSON: são texto serializado | Tipo da coluna por provider (`jsonb` / `nvarchar(max)`). Nada de `ToJson()` |
| `ILike` | 1 uso: busca de texto da triagem (`PostgresListingScoreRepository.ListForTriageAsync`), com o escape à mão | Trocar por `Title.ToLower().Contains(text.ToLower())`: o EF escapa os curingas sozinho nos dois providers e o comentário da armadilha `ESCAPE ''` some |
| Identity | 2 `UseIdentityAlwaysColumn()` (`collection_runs`, `application_events`) | `ValueGeneratedOnAdd()` no modelo comum; o detalhe do Npgsql só na configuração do Postgres |
| `timestamptz` | O mapper já grava tudo em UTC (`ToUniversalTime()`) | `datetimeoffset` no SQL Server, sem mudança no mapper |
| SQL cru, `ON CONFLICT`, `COPY`, `xmin`, arrays, `citext` | **Nenhum.** A gravação diária é "busca e atualiza" (`RecordObservationsAsync`) | Nada a fazer |
| `ids.Contains(...)` | Muitos (lotes de IDs) | O EF traduz para `OPENJSON` no SQL Server: exige nível de compatibilidade ≥ 130 (o 2022 tem). Teste de contrato com lote grande |
| Índice em texto sem tamanho | Toda coluna de chave ou índice já tem `HasMaxLength` | Nada; a migration do SQL Server confirma |
| Maiúsculas | Chaves já normalizadas: fonte em minúsculas (`ListingId`), `search_key` em minúsculas, hash hex | Banco SQL Server com collation **sensível** a maiúsculas (`Latin1_General_100_CS_AS_SC_UTF8`), para a semântica ser a mesma do Postgres. Teste com termos e cidades que só diferem em caixa e acento |

**O que pesa de verdade na fase 1 é estrutura, não dialeto:** o nome `Postgres` está em todo namespace, pasta e
classe da Infrastructure (`Infrastructure.Postgres.*`, `Postgres*Repository`, `AddPostgresInfrastructure`,
`PostgresFixture`), as migrations moram dentro dela e o `deploy.ps1` gera o bundle a partir dela.

**O domínio é australiano por construção** (afeta a fase 2):
- `ListingLocation` tem `AustralianState State` (enum NSW…NT) e `Postcode` de 4 dígitos.
- `FitScorer` é o score de um WHV: cidadania/PR, direito de trabalho, 88 dias, FIFO, carteira de caminhão.
  Os padrões (`ListingPatterns`) são em inglês.
- `Salary` anualiza por 2080 h, sem moeda.
- "Market" já é nome ocupado: é o Raio-X (`Domain/Market`, `MarketSnapshot`, configuração `Market:`).
  O "mercado AU/BR" do plano original vira **`Country`** (código ISO, `AU`/`BR`).

**Conflitos com os documentos do projeto** (resolver na fase 0):
- [visao.md](visao.md) tem "Só Austrália" nos não-objetivos e o propósito é o WHV.
- [plano.md](plano.md): "fontes novas esperam" a calibragem, e a primeira fonte nova seria o Adzuna. A Gupy não
  mexe nos dados da calibragem da Austrália (país separado), então não fere a regra, mas a ordem precisa ser
  decidida e registrada.

**Observabilidade parte de mais perto do que parecia:** os logs já são estruturados (templates com
`{Stage}`, `{Seconds}`), a `/status` (`StatusService`) já calcula a última passada, e o vigia
(`deploy/watchdog.ps1`) só olha se o serviço está parado. Faltam métricas, traces, escopos e `/health`.

**User-Agent do Seek** (`SeekOptions.UserAgent`) cita `job-radar-au` no link: o rename muda o que o Seek vê.
Trocar junto, de uma vez, sem mudar mais nada da integração.

## Decisões (aceitas em 2026-10-29)

1. **Escopo (fase 0):** o Brasil entra só no Matching (uso próprio) ou também num "Raio-X BR"? Proposta:
   Matching e contagem de tecnologias para uso próprio; nada público do BR até haver motivo. Atualiza `visao.md`.
2. **Score BR (fase 2):** proposta é **um** `FitScorer` (a regra "única implementação" continua), com as partes
   do país extraídas para `CountryRules` (padrões de exclusão, local, salário, idioma) e a política por perfil
   como hoje. Alternativa: um segundo scorer atrás de uma porta, mais simples de escrever e mais difícil de
   manter consistente (duas explicações, dois `rules_hash`).
3. **Local (fase 2):** `ListingLocation` com `Country` e `Subdivision` em texto (UF ou estado australiano)
   no lugar do enum `AustralianState`, que vira só um valor do AU. Mexe em toda a cadeia (mapper, migration,
   tradutor do Seek, importação), por isso fica num PR só, sem mudança de comportamento, antes da Gupy.
4. **Ordem entre as fases 2 e 3:** a 3 é pequena (2–3 PRs) e deixa as fases 2 e 4 medíveis. Proposta: manter a
   ordem do original (a fase 2 depende da 1, a 3 não depende de nada) e puxar a 3 se a 2 travar na Gupy.

## Fase 0 — Rename e decisões de escopo (1 PR)

- Repo `job-radar-au` → `job-radar` no GitHub (o nome antigo redireciona) e os remotos dos checkouts. As pastas
  podem ficar: o `docker-compose.yml` fixa `name: job-radar-au`, e o volume do banco segue com ele.
- Referências: README, `CLAUDE.md`, `docs/*`, `App.razor` e páginas (título), `app.css`, `SeekOptions.UserAgent`,
  `deploy/auto-deploy.ps1 -Repository`. Os serviços (`JobRadarWorker`,
  `JobRadarWeb`) e `C:\JobRadar` já não têm "AU": ficam.
- `visao.md` e `plano.md` com as decisões 1 e 4.
- **Pronto:** CI verde; o auto-deploy da produção puxa do repo novo; passada seguinte igual à anterior
  (`/status`: mesmo número de execuções e nenhuma busca nova incompleta).

## Fase 1 — SQL Server ao lado do Postgres (5 PRs)

**1.1 Infrastructure neutra, sem mudança de comportamento.** `Infrastructure.Postgres.*` → `Infrastructure.Persistence.*`;
`Postgres*Repository` → `Ef*Repository`; `AddPostgresInfrastructure` → `AddPersistence`. Só rename, os testes de
contrato passam iguais. Mantém `PostgresStorageHealth` (o "esperar o banco" é por provider). As migrations
ficam em `Postgres/Migrations` (são do Postgres) até a 1.2 as levar para o assembly próprio.

**1.2 Provider por configuração e migrations em assemblies próprios.**
- ✅ `Database:Provider` (sem valor = `Postgres`; nome desconhecido falha dizendo os válidos), lido em
  `DatabaseProviders.Parse`; `UseJobRadarDatabase` é o único ponto que liga o contexto a um provider (DI, fábrica de
  design, fixture). A connection string continua em `ConnectionStrings:JobRadar`.
- ✅ `JobRadar.Infrastructure.Migrations.Postgres`: as 11 migrations movidas sem regerar, na raiz do projeto
  (`--output-dir .`). O histórico (`__EFMigrationsHistory`) guarda o id, não o namespace: a produção não reaplica
  nada. Teste sem banco (`DatabaseProviderTests`) prova os ids e que o modelo bate com o snapshot.
- Fica para a 1.3, com o SQL Server: `IEntityTypeConfiguration` comuns; o que muda por provider (tipo JSON, identity) num ajuste por provider,
  sem `if` espalhado.
- ✅ Uma `DesignTimeDbContextFactory` em cada assembly de migrations: dispensa o `-- --provider` do original.
- ✅ `deploy.ps1`: bundle a partir do assembly de migrations do Postgres (o bundle foi gerado localmente com o
  mesmo comando).

**1.3 Migration inicial do SQL Server + itens da auditoria.** ✅ `JobRadar.Infrastructure.Migrations.SqlServer` com a
`InitialCreate` gerada do mesmo modelo (lida: 15 tabelas, `datetimeoffset`, `nvarchar(max)` no JSON, `IDENTITY`, cascatas
de um nível só). O que difere por banco ficou no `ProviderModel`: jsonb e identity do Postgres (o modelo dele não mudou,
sem migration nova), collation `Latin1_General_100_CS_AS_SC_UTF8` no SQL Server. Descoberta: a collation só entra quando
a migration **cria** o banco; a fixture da 1.4 precisa de um banco novo, não o `master` do container. Busca da triagem
sem `ILike` (`ToLower().Contains()`). O SqlClient puxava `System.Security.Cryptography.Pkcs` 9.0.11, vulnerável: fixado
em 10.0.13.

**1.4 Contratos nos dois bancos.** ✅ no PR 5. O primeiro CI contra o SQL Server de verdade achou duas diferenças, as duas
resolvidas no `ProviderModel`: o change tracker do provider SQL Server compara chaves de texto sem caixa (trocado por
ordinal, com teste sem banco), e `nvarchar(max)` aceitava JSON inválido que o jsonb recusa (`CHECK ISJSON`; a
`InitialCreate` do SQL Server foi regerada, porque nunca tinha sido aplicada). Ficou de fora: a variável para rodar
só um dos bancos; o CI roda os dois no mesmo job.
- `Testcontainers.MsSql` em `Directory.Packages.props`; `SqlServerFixture` ao lado da `PostgresFixture`.
- As 10 classes de contrato viram abstratas com uma derivada por banco (sem `[Theory]` por provider: a fixture
  de coleção do xUnit fica limpa assim). `[DatabaseFact]` continua o portão.
- Teste por banco: aplica todas as migrations do zero e `HasPendingModelChanges()` é falso.
- Testes novos: chaves que só diferem em caixa/acento; `Contains` com lote de milhares de IDs.
- CI: matriz `provider: [postgres, sqlserver]` com `JOBRADAR_DB_PROVIDER` para o job filtrar.
- `Every_application_port_is_registered` com um caso por provider.

**1.5 Desempenho, cópia e README.**
- ✅ Comando `copy-database Postgres|SqlServer <conexão>`. **Não** pelos repositórios, como o original propunha: eles
  respondem perguntas de negócio e não leem tabelas inteiras. `DatabaseCopy` (Infrastructure) copia entidade por
  entidade, pais primeiro, em páginas de 1.000; recusa um destino com linhas antes de gravar qualquer coisa; ids
  gerados (logs de execução e de status, que nenhuma tabela referencia) são gerados de novo, na ordem da origem.
  Contrato copia de um Postgres para um SQL Server de verdade e lê de volta pelos repositórios.
- Medir a triagem no SQL Server com plano real. Atenção: ela hoje traz **todas** as linhas do perfil e agrupa
  duplicatas em memória antes de paginar (armadilha registrada). A história honesta de desempenho é medir
  isso, empurrar ao banco o que dá (filtros, projeção só das colunas usadas) e ajustar o índice
  `ix_listing_scores_profile_score` (com `INCLUDE`). Antes/depois em `docs/perf-sqlserver.md`.
  **Pendente, no trabalhopc:** precisa dos dados reais (ficam lá) e de um SQL Server rodando; nesta máquina o Docker
  Desktop não sobe. Passos: `docker compose --profile sqlserver up -d sqlserver`, migrations do SQL Server,
  `copy-database`, e medir.
- ✅ README: como escolher o banco, criar o SQL Server pelas migrations e copiar os dados.

**Pronto:** os seis itens do §2.7 do original. A produção segue em Postgres.

## Fase 2 — País BR + Gupy (5 PRs)

**2.1 Spike da Gupy (sem código no repo).** ✅ 2026-10-29, detalhes em [fontes.md](fontes.md#brasil-spike-de-2026-10-29-fase-2-da-evolução).
A busca do portal (`portal.gupy.io/api/job-search/jobs`) é JSON aberto e já traz o anúncio inteiro, o regime e o
modelo de trabalho; o endpoint antigo do plano original não existe mais. Os termos proíbem "agregar, copiar ou
duplicar partes" da plataforma, como os do Seek proíbem acesso automatizado. **Decidido pelo autor (2026-10-29): Gupy**, nas
mesmas regras do Seek: uso pessoal, ritmo baixo, nada do Brasil em público, anúncio inteiro só no banco local. O
Adzuna segue como próxima fonte da Austrália (e, se a API cobrir o Brasil, uma segunda fonte daqui).

**2.2 `Country` e local neutro, sem mudança de comportamento.** ✅ `ListingLocation(Country, Suburb, Region, State,
Postcode)`: `Country` (`AU` | `BR`, código ISO) é o mercado da vaga, sempre conhecido; o estado virou texto ("WA",
"SP"). O enum `AustralianState` saiu do domínio: só o tradutor do Seek sabe quais são os estados australianos. Migration
`AddListingCountry` nos dois bancos (coluna `country`, editada para `AU` no histórico; CEP de até 9 caracteres). Um país
desconhecido no banco falha em vez de ser adivinhado. A paridade não rodou nesta máquina (o arquivo fica no
trabalhopc); `FormatLocation` produz o mesmo texto de antes.

**2.3 Regras por país no `FitScorer`.** Extrair `CountryRules` (decisão 2): o AU é exatamente o que existe hoje,
provado pela paridade das 5.388 vagas sem mudança de resultado e sem mudar `RulesVersion`. Nenhuma regra BR ainda.

**2.4 `JobRadar.Integrations.Gupy`.** `AddGupyIntegration()`, `IJobSource`, tradutor para `Listing` com
`Country = BR`, respostas gravadas nos testes, resiliência e ritmo como o Seek (orçamento próprio, baixo).
Perfil BR em `appsettings.Local.json`. Sem `IFullTextSearch` se a Gupy não tiver.

**2.5 Regras BR e a contagem de tecnologias.** Padrões em português (júnior/pleno/sênior, remoto/híbrido/presencial,
CLT/PJ, salário em R$, inglês obrigatório, stack principal), pesos na `ScoringPolicy`, `RulesVersion` +1.
Calibrar com rótulos depois, como no AU. Contagem mensal de tecnologias pedidas nas vagas BR (comando e página
de uso próprio). Filtro por país na triagem.

**Pronto:** §3.5 do original. O Raio-X público continua só AU (decisão 1).

## Fase 3 — Observabilidade (3 PRs)

**3.1 OpenTelemetry e escopos.** `OpenTelemetry.Extensions.Hosting` com ASP.NET Core, HttpClient e EF Core no
Worker e na Web. `BeginScope` com `RunId`, `Country`, `Source` no `DailyPipeline`. Exportador console por padrão;
OTLP opcional para o Aspire Dashboard (um container a mais no `docker-compose.yml`, só local).

**3.2 Métricas e traces da passada.** `Meter` `JobRadar`: as 6 métricas do original. Uma `Activity` por etapa
do `DailyPipeline` e por busca. As tags são fonte, país e etapa, nunca título ou empresa.

**3.3 `/health` e o vigia.** Na Web, reusando o `StatusService`: banco acessível e última passada com sucesso
há menos de 26 h. O vigia passa a consultar `/health` **além** de olhar o serviço parado (o Worker não tem HTTP;
o `/health` vê a passada que ele gravou).

**Pronto:** §4 do original.

## Fase 4 — Mensageria (4 PRs, só com Seek + Gupy em produção)

Começa por **reavaliar se o problema existe**: hoje cada busca já falha isolada e as etapas também. Se a
fase 3 mostrar que uma fonte lenta não atrasa as outras de forma que importe, a fase fica registrada como
"não precisou" em vez de virar vitrine.

1. Porta `IJobEventBus` e eventos (`ListingCollected` → `ListingDescribed` → `ListingScored`), com uma
   implementação em memória que mantém a passada atual funcionando.
2. Outbox na transação da gravação, nos dois providers.
3. RabbitMQ (biblioteca a decidir: `RabbitMQ.Client`, MassTransit 8 ou Wolverine; o MassTransit 9 é comercial,
   conferir a licença na hora), consumidores idempotentes, retry com espera crescente, fila de mortos.
   `Testcontainers.RabbitMq`.
4. Métricas de fila no `Meter` da fase 3; teste que reprocessa a fila de mortos sem duplicar.

**Pronto:** §5.3 do original.

## Fora de escopo

Igual ao §6 do original: Kubernetes, Angular, OAuth/JWT, AWS/Terraform, migrar a produção para SQL Server.
