# Evolução — JobRadar AU → JobRadar (out/2026)

> Plano de implementação de `JobRadar-plano-de-evolucao.md` (vault), conferido contra o código em 2026-10-29.
> Uma fase por vez; cada fase só fecha com o critério de pronto. A passada diária da Austrália não para.
> Ao fechar uma fase: ✅ com a data aqui, Diário em [plano.md](plano.md), e as atualizações do vault (§8 do original).

## Estado

| # | Fase | PRs | Estado |
|---|---|---|---|
| 0 | Rename e decisões de escopo | 1 | ✅ 2026-10-29 (PR 1; passada seguinte não conferida antes de seguir) |
| 1 | SQL Server ao lado do Postgres | 5 (PRs 2–6) | 🔨 implantado 2026-10-29; falta medir a triagem no SQL Server com os dados reais (1.5) |
| 2 | País BR + Gupy | 6 (PRs 7–12) | 🔨 implantado 2026-10-29, paridade ok (259 testes); falta a primeira passada com um perfil BR |
| 3 | Observabilidade | 2 (PRs 13–14) | ✅ implantado 2026-10-29 (telemetria desligada até haver `Telemetry:OtlpEndpoint`) |
| 4 | Mensageria | 4 | ⏳ esperar números: as métricas da fase 3 com Seek e Gupy dizem se uma fonte atrasa as outras ([#28](https://github.com/DaviLuisSantos/job-radar/issues/28)) |
| 5 | Mercados remotos e interface | ~10 | 🔨 5.1 a 5.4 implantadas; o resto nas issues abaixo |

**O que falta, como issues:** 5.6 [#22](https://github.com/DaviLuisSantos/job-radar/issues/22), 5.7 [#23](https://github.com/DaviLuisSantos/job-radar/issues/23); prints do README [#24](https://github.com/DaviLuisSantos/job-radar/issues/24); 1.5 [#26](https://github.com/DaviLuisSantos/job-radar/issues/26); pendências do Brasil [#27](https://github.com/DaviLuisSantos/job-radar/issues/27); mensageria [#28](https://github.com/DaviLuisSantos/job-radar/issues/28); voltar sem login [#29](https://github.com/DaviLuisSantos/job-radar/issues/29).

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

**1.5 Desempenho, cópia e README.** A medição falta: [#26](https://github.com/DaviLuisSantos/job-radar/issues/26).
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

**2.3 Regras por país no `FitScorer`.** ✅ `CountryRules`: tudo o que o scorer lê e depende de idioma ou país
(cidadania, WHV, senioridade, contrato, carteira de caminhão, anos pedidos, remoto/híbrido/FIFO). `CountryRules.Australia`
aponta para os mesmos padrões de `ListingPatterns`; o scorer escolhe pelo `Country` da vaga e as fases não mudaram
(o diff do `FitScorer` só troca de onde vem cada padrão). Sem `RulesVersion` nova. Um país sem regras é recusado
(`NotSupportedException`), em vez de lido como australiano. A paridade não rodou nesta máquina: rodar no trabalhopc.

**2.4 Regras BR.** (Era a 2.5: tem de vir **antes** da Gupy, ou as vagas brasileiras chegariam ao score sem regras e
a etapa falharia.) ✅ `RulesVersion` 14. `CountryRules.Brazil` (padrões em `BrazilPatterns`): sênior penaliza e
"pleno/sênior" não, liderança (tech lead, coordenador, gerente…) exclui, remoto e híbrido em português, comissão,
anos de experiência ("5+ anos de experiência", "experiência mínima de 3 anos"), inglês como condição (−10,
`EnglishRequiredPenalty`: marca o que treinar, não exclui). O que é australiano (WHV, cidadania, carteira de caminhão,
FIFO, os seis meses por empregador) nunca casa. O que é **do candidato** foi para o perfil, valendo para qualquer país:
`ExcludedTitles` (júnior, estágio, Java como stack principal) e `OnSiteLocations` (presencial fora de onde ele aceita
exclui). No AU as regras novas nunca disparam: perfis sem as listas, padrões que não casam.

Ficou para depois, com motivo ([#27](https://github.com/DaviLuisSantos/job-radar/issues/27)):
- **Salário** (R$ 10–15 mil +, abaixo de 8 mil −): só vem no texto (1 em 6 vagas) e o alvo é do candidato; entra com
  os rótulos, quando der para ver se pesa.
- **Vaga afirmativa** (exclusiva para PcD, mulheres, pessoas negras): o equivalente da "identified role" do AU, mas a
  explicação de lá fala de aborígenes; precisa de regra e texto próprios. Conferir o que o `badges`/`disabilities`
  da Gupy quer dizer antes.
- **Híbrido no Rio neutro**: hoje híbrido ganha o bônus de híbrido (+5) em qualquer lugar.
- **Banco de talentos** e **CLT/PJ**: não são regra de score; o banco de talentos sai no tradutor da Gupy (não é vaga
  aberta), e o regime vai no resumo da vaga.

**2.5 `JobRadar.Integrations.Gupy`.** ✅ Busca, perfil e fonte agora têm país: a coleta só manda a cada fonte as
buscas do país dela (`ListingSearch.Country`, `IJobSource.Country`, `Country` no perfil, `AU` por padrão). A chave de
uma busca de outro país termina com o país (`"desenvolvedor .net @ rio de janeiro (br)"`); as do AU não mudaram.
A integração segue o molde do Seek: opções na classe, resiliência padrão e um orçamento próprio e mais baixo (1
requisição por segundo, 2 simultâneas). A busca já traz o anúncio inteiro, que vai em `SourcedListing.Description` e a
coleta grava em `listing_descriptions` (origem `Source`), reescrevendo só quando muda. Banco de talentos fica de fora.
Local: uma cidade (`city`), `Remoto` (`workplaceType=remote`) ou `Brasil` (o país todo). Sem filtro de data na API: a
janela incremental não se aplica e cada passada lê as páginas da busca. Resumo da vaga: arranjo e regime ("Remoto · CLT").
Testes com uma resposta fictícia no formato real, e `LiveGupyTests` contra a API (passou em 2026-10-29). Perfil de exemplo `dotnet-br` em
`appsettings.Local.example.json`.

**2.6 Triagem e contagem do Brasil.** ✅ A triagem já separa por país: um perfil só mostra as vagas das suas buscas, e
um perfil BR só busca nas fontes do Brasil. O site marca o país ao lado dos perfis que não são da Austrália (e a
marca "JobRadar AU" que sobrou do rename saiu do topo). Contagem de tecnologias: `tech-count [AU|BR] [YYYY-MM]`
(padrão: BR, este mês), lendo título e anúncio das vagas do país vistas pela primeira vez no mês (mês em UTC). Cada
vaga conta uma vez por tecnologia; só entram as que têm o anúncio inteiro, porque um título sozinho faria as fatias
mentirem. O catálogo (`TechnologyCount.Catalog`) cobre o que o plano de entrevistas acompanha: linguagens, front-end,
dados, nuvem, mensageria, arquitetura, prática e inglês. Página no site: não fiz; o comando basta para alimentar o
`Requisitos das vagas.md` do vault.

**Pronto da fase 2** (§3.5 do original): vagas BR na passada diária, pontuadas pelas regras BR ✅ no código (falta a
primeira passada real com um perfil BR); equivalência do score AU **a conferir com a paridade no trabalhopc**; triagem
por país ✅; relatório mensal separado ✅ (o Raio-X segue só AU); contagem de tecnologias ✅.

**Pronto:** §3.5 do original. O Raio-X público continua só AU (decisão 1).

## Fase 3 — Observabilidade (3 PRs)

**3.1 OpenTelemetry e escopos.** ✅ `AddJobRadarTelemetry` (Infrastructure/Telemetry) no Worker e no site: traces,
métricas e logs por OTLP, com HttpClient, Npgsql (a fonte `Npgsql`, sem pacote beta do EF) e, no site, ASP.NET Core.
**Só com `Telemetry:OtlpEndpoint`**: sem ele nada é coletado e os logs seguem como estavam (o original previa
exportador de console por padrão; num serviço do Windows isso só duplicaria o log). `RunId` (o id do trace da passada)
e `Stage` vão em escopos de log; fonte e país, no texto do erro de busca. Aspire Dashboard 13.5.2 no
`docker-compose.yml`, `--profile telemetry`.

**3.2 Métricas e traces da passada.** ✅ `JobRadarTelemetry` (Application, só com o que o .NET traz): um trace por
passada (`daily pass`), uma `Activity` por etapa e por busca (`collect search`, com fonte, país, chave, achadas e
novas). Métricas: `jobradar.jobs.collected`, `.jobs.new`, `.jobs.known` (o "deduplicated" do original: já conhecidas
quando a busca as devolveu), `jobradar.source.errors` (por fonte, país e tipo de erro), `jobradar.stage.duration` (por
etapa e resultado, cobre o "enrichment") e `jobradar.run.duration`. Tags nunca levam título, empresa ou dado do candidato.

**3.3 `/health` e o vigia.** ✅ `GET /health` no site (`HealthService`): 200 quando o banco responde e uma passada
começou há menos de 26 h; 503 com o motivo, senão. Só a hora da passada, nada de vaga. "Com sucesso" do original virou
"começou": uma etapa que falha é refeita na passada seguinte e já aparece na `/status`; o que o vigia pode consertar é a
passada que não roda. O `watchdog.ps1`, além de religar serviço parado, consulta o `/health`: passada atrasada com o
banco de pé → reinicia o Worker (que recupera a passada perdida ao subir); banco fora → só registra (o Worker já espera
o banco). No máximo uma vez a cada 6 h (`-HealthEveryHours`, marca em `C:\JobRadar\watchdog-health.txt`), para não
reiniciar em laço. Testado no Windows PowerShell 5.1 contra um servidor de mentira que responde 503 (passada atrasada →
tenta reiniciar; banco fora → só registra; saudável → nada; segunda vez dentro das 6 h → nada). O teste achou um furo:
no 5.1 o corpo do 503 não vem em `ErrorDetails`, vem no stream da resposta, e o script agora lê os dois.

**Pronto da fase 3** (§4 do original): trace completo de uma passada no painel ✅ no código (ver no Aspire com
`--profile telemetry`); métricas ✅; `/health` e o vigia usando ✅; erro de fonte com contexto (fonte, país, `RunId`) ✅.

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

## Fase 5 — Mercados remotos e interface (planejada em 2026-10-30)

**Objetivo:** usar a mesma lógica de matching para vagas remotas que aceitem quem mora no Brasil (LATAM e mundo), sem
mexer no que funciona na Austrália, e investir na interface antes que quatro mercados virem uma bagunça.

### Decisões (autor, 2026-10-30)

- **Quatro mercados, separados:** Australia, Brasil, Remoto LATAM e Remoto global. Cada um com os seus perfis e a sua
  tela.
- **Interface em português.** O código segue em inglês; os textos já moram em `Text/Wording.cs`. Os prints do README são
  refeitos.
- **Escopo da UI, completo:** navegação por mercado, linha da triagem mais rica, página da vaga, tela "Hoje" e celular.
- **Deploy:** cada passo vai para a produção (push na `main`, sem PR); cada push deixa o site funcionando, mesmo com a
  interface meio a meio por alguns dias.
- **LATAM x global, pela restrição:** LATAM = vagas restritas a uma região que inclui o Brasil (LATAM, Americas, South
  America, Brazil); global = abertas ao mundo (Worldwide, Anywhere). Uma vaga cai em um só.
- **Fuso:** não pesa no score; só aparece como etiqueta.
- **Senioridade no remoto:** igual ao perfil BR (júnior e estágio excluem, pleno ganha, sênior penaliza, liderança exclui).
- **Excluídas:** fora da lista, com uma contagem que abre a lista delas, cada uma com o motivo.
- **Perfis remotos:** começam como cópia do `dotnet-br` (mesmos sinais, palavras-chave em inglês), um por mercado.
- **"Hoje":** a última visita por mercado fica no banco, para valer entre celular e PC.
- **Regime e salário no remoto:** decididos em 2026-10-30, ver a 5.7.
- **Biblioteca de componentes: MudBlazor** (9.11.0, MIT, .NET 10), para tema claro e escuro, layout responsivo, chips,
  toggles, avisos e gaveta no celular. Alternativa, se o MudBlazor atrapalhar: Microsoft Fluent UI Blazor.

### O que muda no modelo

**Mercado não é país.** Hoje o `Country` faz os dois papéis: onde a vaga fica e qual regra lê o anúncio. Para AU e BR
coincidem; para remoto, não. Uma vaga de uma empresa americana que aceita a América Latina não "é" de país nenhum, e a
mesma vaga pode servir ao mercado LATAM e ao global.

- **`Market`** (`AU`, `BR`, `LatamRemote`, `GlobalRemote`): o que um perfil procura. Fica no perfil e na busca (no lugar
  de `Country`, que segue aceito na configuração por compatibilidade) e decide as fontes e as regras do score. As regras
  passam a ser escolhidas pelo mercado do **perfil**, não pelo país da vaga: a mesma vaga remota é lida pelas regras de
  cada mercado que a buscou.
- **`Country` da vaga** continua: onde ela fica, quando fica em algum lugar.
- **Elegibilidade da vaga** (nova): de onde a empresa aceita gente (`Worldwide`, `LATAM`, `Americas`, `Brazil`, `US only`,
  `Europe`...), como a fonte informa. É o que decide se a vaga serve, como a cidadania decide na Austrália.

### Passos

**5.1 Mercado como conceito.** ✅ 2026-10-30. `JobMarket` (`AU`, `BR`, `LatamRemote`, `GlobalRemote`; não `Market`, que
colide com o namespace do Raio-X) no perfil (`"Market"`, com o `"Country"` de antes ainda aceito) e na busca; cada fonte
declara os mercados que serve (`IJobSource.Markets`: um quadro remoto pode servir LATAM e global). `CountryRules` virou
`MarketRules`, escolhida pelo mercado do **perfil**, não pelo país da vaga; os mercados remotos são recusados até a 5.7.
A chave de busca do BR segue `(br)`. A elegibilidade (campo e migration) ficou para a 5.6, junto da primeira fonte que a
informa: sem fonte, seria uma coluna sempre vazia. Paridade a rodar no trabalhopc (o `FitScorer` mudou de onde tira as
regras).

**5.2 Interface: base.** ✅ 2026-10-30 (estrutura em `a27d47c`, português neste commit; prints do README ficam para depois da 5.3). MudBlazor, tema (claro e escuro) e `Wording` em português. Barra superior com o seletor de
mercado (contagem de novas em cada um) e os perfis do mercado logo abaixo; Captura, Você, Requisitos, Calibragem e Status
num menu de ajustes. Layout responsivo, com gaveta no celular. As teclas da triagem (`j` `k` `g` `b` `u` `o`) continuam.

**5.3 Interface: triagem e vaga** ([#19](https://github.com/DaviLuisSantos/job-radar/issues/19)). ✅ 2026-10-30, com o que as fontes de hoje dão: na linha, etiquetas de
arranjo (remota, híbrida, presencial) e regime (CLT, PJ), lidas do resumo (`ListingConditions`), ao lado dos fatos do
anúncio; excluídas fora da lista, com uma contagem que abre a lista delas, cada uma com o motivo em uma linha. Página da
vaga com ações numa barra fixa, "por que esta nota" em cima e o anúncio inteiro aberto. **Fica para depois:** as etiquetas
de elegibilidade e fuso entram com a 5.6 (a primeira fonte que as informa); contractor/EOR, moeda e faixa, com o
`EngagementDetector` e o `PayParser` da 5.7.

**5.4 Tela "Hoje"** ([#20](https://github.com/DaviLuisSantos/job-radar/issues/20)). ✅ 2026-10-30. `hoje/{au|br|latam|global}` é a página inicial: o que chegou desde a
última visita ao mercado, as cinco melhores novas de cada perfil e as mudanças de candidatura no período. "Nova" é o que a
triagem mostraria (sem tratamento, não excluída, cópias juntas) e foi vista pela primeira vez depois da visita; o filtro
`TriageFilter.SeenSince` vai ao banco. Uma vaga achada por dois perfis conta uma vez no mercado. A visita fica em
`market_visits` (`IMarketVisitRepository`, migration `AddMarketVisits` nos dois bancos) e é gravada só depois da
renderização interativa: o prerender a gravaria antes de a página mostrar o que era novo. Na primeira visita, uma semana
para trás. O seletor de mercado mostra a contagem de novas de cada um, recalculada a cada troca de página.

**5.5 Spike das fontes remotas (sem código no repo)** ([#21](https://github.com/DaviLuisSantos/job-radar/issues/21)). ✅ 2026-10-30, em
[fontes.md](fontes.md#remoto-spike-de-2026-10-30-passo-55-da-evolução): **Himalayas** para os dois mercados (elegibilidade
numa lista de países, vazia = mundo; salário estruturado) e **Jobicy** para o LATAM (`jobGeo`). Remotive, RemoteOK e We
Work Remotely quase não têm .NET para quem está no Brasil; Get on Board espera os termos.

**5.6 Fontes remotas** ([#22](https://github.com/DaviLuisSantos/job-radar/issues/22)). `JobRadar.Integrations.<Site>` para as escolhidas, no molde do Seek e da Gupy, com teste ao vivo.

**5.7 Regras remotas** ([#23](https://github.com/DaviLuisSantos/job-radar/issues/23)). `MarketRules` dos dois mercados remotos, em inglês: elegibilidade que não inclui o Brasil exclui
("US only", "must reside in", "EU only"); fuso fora do alcance penaliza; contractor e pagamento em dólar são dados, não
penalidade; inglês é esperado, não penaliza. LATAM e global diferem no que aceitam como elegível. `RulesVersion` +1.

**5.7, regime e salário (decidido em 2026-10-30, nomes aprovados).** Só nos perfis com `Compensation`; AU e BR não mudam.
- **Formato** `Engagement { Unknown, Contractor, Eor, Direct }` (na tela: NaoInformado, Contractor, EOR, Direto), lido do
  título, resumo e anúncio por `EngagementDetector` com frases de `MarketRules` (`ContractorPhrases`, `EorPhrases`,
  `DirectHirePhrases`); "Deel/Remote.com/Oyster" contam junto de "contractor" ou "employee" numa janela curta. Precedência
  EOR > Contractor > Direto > Unknown. Só etiqueta (item `Engagement`, 0 ponto), filtro na triagem e contagem no `tech-count`.
- **Salário** lido por `PayParser` → `Pay(Min, Max, Currency, PayPeriod)`, normalizado ao mês (anual ÷ 12; hora ×
  `HoursPerMonth`). O `SalaryParser` do Raio-X (AUD, por ano) fica como está.
- **Configuração, fora do Git:** `Matching:Compensation` (`UsdToBrl`, `HoursPerMonth`) e, por perfil,
  `Compensation:Contractor` e `Compensation:Eor` com `Currency`, `MonthlyFloor`, `MonthlyTarget` (exemplo neutro versionado).
  Direto e Unknown usam os valores de Contractor quando o salário vem em USD e os de EOR quando vem em BRL; câmbio só
  quando as moedas diferem. Os pisos estão no hash das regras do perfil.
- **No scorer:** bônus `PayAtTarget` na fase 2 (o mínimo da faixa atinge o alvo); penalidade `PayBelowFloor` na fase 4 (o
  máximo fica abaixo do piso); `PayUnreadable` (0 ponto) quando a moeda ou o valor não se lê. Pesos na `ScoringPolicy`
  (`PayBelowFloorPenalty` 15, o tamanho da de sênior; `PayAtTargetBonus` 8), não no arquivo local: peso é da regra, alvo é
  do candidato. Sem salário: neutro.
- **Log:** uma linha por passada com a contagem e os `fonte:id` de salário ilegível (o scorer é puro; quem loga é o
  `ListingScoringService`).
- **Testes:** exemplos inventados (anúncio real não entra no repo); as vagas reais são conferidas com
  `calibrate --rescore` no banco de produção.

**Pronto:** os quatro mercados aparecem separados, a Austrália segue igual (paridade), um perfil remoto recebe vagas de
uma fonte remota e exclui as que não aceitam o Brasil, e a interface funciona no celular.

## Fora de escopo

Igual ao §6 do original: Kubernetes, Angular, OAuth/JWT, AWS/Terraform, migrar a produção para SQL Server.
