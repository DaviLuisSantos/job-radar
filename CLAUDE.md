# JobRadar AU

Acompanha o mercado de trabalho australiano para quem chega com Working Holiday (WHV).
Dois produtos sobre um núcleo comum: **Matching** (privado: ranqueia vagas para um perfil,
explica o score, acompanha candidaturas) e **Raio-X** (público: só números agregados, base
da newsletter Raio-X WHV em português). Reescrita em .NET de um protótipo Python
(`D:\Desenvolvimento\job-tracker`), que é a especificação das regras de negócio.

Produção (onde roda, instalar, atualizar, logs, backup): [docs/producao.md](docs/producao.md). Push em main com CI
verde é implantado sozinho em até 15 min (`deploy/auto-deploy.ps1`); voltar: `deploy.ps1 -Rollback`.

Escopo, públicos, não-objetivos e fases: [docs/visao.md](docs/visao.md). Fontes de vagas: [docs/fontes.md](docs/fontes.md).

**Onde estamos e o que vem a seguir: [docs/plano.md](docs/plano.md).** Ao fechar uma etapa, atualize o
plano (estado, Diário) e passe para a próxima na ordem.

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
JOBRADAR_PARITY_FILE=D:\Desenvolvimento\job-tracker\data\parity\scoring-parity.json dotnet test --project tests/JobRadar.Domain.Tests
```

```bash
docker compose up -d
```

```bash
dotnet run --project src/JobRadar.Web
```

```bash
dotnet ef migrations add <Nome> --project src/JobRadar.Infrastructure --output-dir Postgres/Migrations
```

- Build: 0 erros, **0 warnings** — `TreatWarningsAsErrors` em `Directory.Build.props`.
- Testes: sem banco por padrão. Os de contrato (`[DatabaseFact]`) sobem Postgres 18 via Testcontainers
  e só rodam com `JOBRADAR_DB_TESTS=1`. O CI roda os dois.
- Teste ao vivo (`[LiveFact]`, `JOBRADAR_LIVE_TESTS=1`) chama a API real do Seek pela composição completa.
  Fora do CI de propósito. Rode ao mexer em `Integrations.Seek` ou quando a coleta começar a falhar.
- Paridade do score (`[ParityFact]`, `JOBRADAR_PARITY_FILE`): 5.388 vagas reais exportadas do protótipo,
  fora do repo (são anúncios reais). **Mexeu em `Domain/Matching`? Rode.** Mudança de regra intencional
  quebra a paridade: aí registre no `docs/plano.md` e incremente `FitScorer.RulesVersion`.
- Testes usam Microsoft.Testing.Platform (opt-in em `global.json`), não VSTest.
- Versões de pacote só em `Directory.Packages.props` (gerenciamento central, com pinning transitivo).

## Arquitetura de dados

| | Postgres (`jobradar`) |
|---|---|
| Papel | fonte de verdade, do próprio sistema |
| Dono do schema | este projeto — só muda por migration |
| Se perder | perda de dado (histórico do Raio-X) |

## Camadas

```
src/JobRadar.Domain          modelo puro (Listings/, Collection/, Matching/, Tracking/) — sem EF, HTTP ou integração
src/JobRadar.Application     serviços (Collection/, Matching/, Tracking/) + portas em Abstractions/
src/JobRadar.Infrastructure  Postgres/<Agregado>/: Entity → Configuration → Mapper → Repository
src/JobRadar.Integrations.Seek  IJobSource do Seek: cliente (Api/), tradutor, paginação
src/JobRadar.Integrations.Prototype  leitor do jobs.db do protótipo (importação; usa os rótulos do Seek)
src/JobRadar.Worker          composição raiz (JobRadarComposition.AddJobRadar), job diário e comandos (Cli/)
src/JobRadar.Web             triagem em Blazor (JobRadarWebComposition.AddJobRadarWeb); textos em Text/Wording.cs
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

## Fluxo principal

`DailyPipelineJob`, às `Collection:DailyAt` no fuso `Collection:TimeZone`, roda cinco etapas isoladas
(uma falha não impede a seguinte): coleta → termos do texto completo → descrições completas → score → retrato
do Raio-X.

**Coleta** — `ListingCollector.CollectAsync(ProfileCatalog.Searches)`:
1. para cada fonte e busca, calcula a janela incremental pelo último sucesso;
2. `IJobSource.SearchAsync`, que devolve as vagas únicas e diz se a busca veio completa;
3. `IListingRepository.RecordObservationsAsync` grava vagas novas, atualiza as conhecidas, guarda o
   payload só quando o conteúdo mudou e registra que aquela busca achou a vaga (`listing_search_hits`);
4. registra a execução em `collection_runs`, com sucesso ou erro. Falha numa busca não para as outras.

**Termos** — `FullTextTermService.TagAsync`: cada frase de `ScoringPolicy.FullTextGroups` (21 hoje) e dos requisitos de cada perfil
(`ProfileRequirements`: "frase + palavra de contexto", para caber no teto de 500; os grupos de
`ScoringPolicy.ContextGroups` também vão com cada palavra de contexto) é
buscada na Austrália toda por cada fonte com a capacidade `IFullTextSearch` (o Seek tem: a busca dele cobre
o anúncio inteiro). As vagas que já estão no banco ganham a marca em `listing_terms`. Cada termo é uma
execução em `collection_runs` com chave `term: <frase>`. Janela: 31 dias na primeira vez; depois, dias desde o
último sucesso + 2, alargada até a vaga mais antiga coletada desde então.

**Score** — `ListingScoringService.ScoreAllAsync`: para cada perfil, pontua com `FitScorer` todas as
vagas que **as buscas daquele perfil** acharam e grava em `listing_scores` (uma linha por vaga × perfil,
com a explicação em jsonb e o `rules_hash`). Recalcula tudo todo dia, porque a idade muda o score, e no fim
apaga os scores que a passada não gravou (vaga que as buscas do perfil não acham mais, perfil removido).

**Descrição completa** — `DescriptionFetchService`, na passada, busca o texto pelo GraphQL `jobDetails` do Seek
(`SeekJobDetailsClient`, mesmo orçamento da busca): primeiro as vagas rotuladas ou com status, qualquer score ou
idade; depois as de melhor score ≥ `Descriptions:MinScore`, publicadas há até `MaxAgeDays`, até `MaxPerPass`.
Recusa do Seek (`SourceBlockedException`) para a etapa, não a passada. Além disso, o autor pode capturar o texto
do anúncio que ele mesmo abriu (favorito da página `/capture` ou colar na página da vaga); `DescriptionService`
grava em `listing_descriptions` e repontua na hora (`ScoreOneAsync`). O `FitScorer` lê a descrição **só** nas regras de requisito e de direito de trabalho;
sinais do perfil ficam em título e resumo. O site lê os perfis do worker (`profiles.json`, copiado pelo build).
Texto de uso pessoal: nunca no Raio-X, no repo ou em fixture.

**Raio-X** — `MarketSnapshotService.TakeAsync(dia)`: por categoria (`Market:Categories`, código de cada fonte) e
cidade, conta as vagas abertas e as que citam cada termo; nas `FullCities` lista todas. Contagem que falha tem
uma segunda chance. `SnapshotQuality` dá `ok | partial | suspect`; **o relatório mensal só usa `ok`**
(`MarketReportService` + `RaioXMarkdown`). Só números agregados: nada de título, empresa ou link no texto.

Perfis: `Matching:Profiles` em `src/JobRadar.Worker/appsettings.Local.json` (**fora do git**; modelo em
`appsettings.Local.example.json`). Cada palavra-chave é buscada em cada local; as buscas da coleta são a
união dos perfis. Os **fatos** do candidato (visto, formação, carteiras, tickets, experiência) ficam à parte, em
`candidate/candidate.json` (fora do git, como o currículo; esquema e exemplo fictício versionados), preenchido
pela skill `perfil-do-curriculo`. Fato vem do currículo; palavra-chave, contexto e sinal são ajustados contra o
mercado e ficam no perfil. Requisitos de anúncio (anos de experiência, trade, carteira, ticket) são itens do
`RequirementCatalog`, que o perfil cita por id em `Requirements:Items`: frase nova de requisito entra no item, não
no perfil, e vale para todo perfil que usa o item.

**Comandos** (o mesmo executável; sem comando, roda o pipeline): `status <id> <status>`, `notes <id> <texto>`,
`label <id> good|bad|clear [nota]`, `calibrate [--rescore [--assume item=estado,...]]`, `market`, `market-report [YYYY-MM] [arquivo]`,
`import-prototype <jobs.db> [--dry-run]` (histórico do protótipo; ver `docs/producao.md`), `requirements`,
`requirement <item> have|obtainable|missing|out-of-reach|default`, `import-candidate <candidate.json> [--dry-run]`
(estado do candidato em cada item do `RequirementCatalog`, em `candidate_requirements`; item sem estado fica com o
padrão do tipo, que é o peso de antes; mudar um estado repontua na hora as vagas que podem pedir o item, e a página
`/requirements` do site faz o mesmo e mostra o impacto de ter cada item). `<id>` = `fonte:id` (`seek:81234567`). Saída 0 = ok,
1 = vaga nunca coletada, 2 = argumentos inválidos. Mudou peso ou regra? Rode `calibrate` (os scores gravados: o antes) e
`calibrate --rescore` (pontua de novo em memória com as regras do build, sem gravar: o depois). Para medir uma
branch contra os rótulos reais, rode-a num worktree no trabalhopc com o `appsettings.Local.json` copiado.

Para rodar uma passada local: `docker compose up -d`, `dotnet ef database update --project src/JobRadar.Infrastructure`,
e o Worker com `Collection__RunOnStartup=true` (e `Seek__MaxPages=1` para uma prova leve).

## Armadilhas

- Npgsql só grava `timestamptz` com offset 0: o mapper converte com `ToUniversalTime()`
  (`ListingMapper.Refresh`).
- `ListingId` normaliza a fonte (`Trim` + minúsculas): `"SEEK"` e `"seek"` são a mesma fonte.
- A máquina do autor tem uma fonte NuGet extra; `nuget.config` limpa e usa só nuget.org.
- Seek repete anúncios promovidos em toda página: `SeekJobSource` deduplica por ID dentro da busca.
  Por isso `totalCount` não bate com o número de vagas devolvidas.
- Local do Seek: divida no **primeiro** `", "` (`SeekListingTranslator.ParseLocation`), porque há regiões
  com vírgula no nome.
- Padrões de opções ficam na classe (`SeekOptions`), não no `appsettings.json`.
- Migration gerada pelo EF pode escolher a renomeação errada. Leia antes de commitar
  (`SplitLocationIntoSuburbAndRegion` foi corrigida à mão: `city` → `region`).
- Campo novo e volátil no payload do Seek duplica `listing_payloads` a cada coleta. Sintoma: payloads >
  vagas depois de duas passadas. Inclua o campo em `SeekSearchClient.VolatileFields`.
- `listing_payloads.payload` é jsonb, que normaliza espaços e ordem das chaves: o hash é do texto recebido
  (já sem os campos voláteis), não do que o banco devolve.
- O `DbContext` vive o pass inteiro de coleta (um escopo): `PostgresListingRepository` limpa o
  `ChangeTracker` depois de cada gravação, com sucesso ou falha. Sem isso, uma falha derrubava o registro da
  execução e centenas de buscas acumulavam entidades rastreadas.
- `appsettings.Local.json` é adicionado depois das fontes padrão; por isso o `Program.cs` reaplica variáveis
  de ambiente e linha de comando em seguida, para elas continuarem vencendo.
- Termos: a busca do Seek não dá o contexto da frase ("no sponsorship" também contém "sponsorship"). Por isso os
  pesos dos grupos são moderados e só o grupo `exclude` elimina. A divisão por estado quando passa de 500 é
  peculiaridade do Seek e fica em `SeekFullTextSearch`, não no serviço.
- Rótulo vale para o estado do candidato quando foi dado: uma vaga ruim porque pedia carteira HR vira boa quando
  ele tira a carteira (`requirement truck-hr have`). Mudou um estado para "tenho"? Reveja os rótulos das vagas que
  pedem aquele item antes de ler a calibragem como regressão. `--assume` mostra quais sobem.
- `Dismissed` (o candidato descartou) e `Declined` (o empregador recusou) não são a mesma coisa: na calibragem
  o primeiro conta como ruim e o segundo como **boa**. Não junte os dois de novo.
- Os argumentos de um comando não entram na configuração do host (`Program.cs`): o texto livre de `notes`
  poderia ser lido como chave=valor.
- Web: componente interativo chama serviço **só** por `Backend.RunAsync` (um escopo, e um `DbContext`, por
  operação). Injetar `TriageService` direto no componente prende um `DbContext` à aba inteira.
- Web sem login: escuta em `localhost:5380` e, em produção, no IP da Tailscale (`appsettings.Local.json` gerado
  por `deploy/deploy.ps1`; firewall só `100.64.0.0/10`). A 5080 é de outro serviço local do autor.
- Serviço do Windows inicia antes do Docker Desktop (que sobe no login): o `DailyPipelineJob` espera o banco
  (`IStorageHealth`) e recupera a passada perdida. Não remova a espera achando que é lentidão.
- `EF.Functions.ILike` com padrão escapado precisa do terceiro argumento (`@""`): sem ele o Npgsql usa
  `ESCAPE ''` (`PostgresListingScoreRepository.ListForTriageAsync`).
- Lista de triagem: agrupar duplicatas (`Duplicates.Collapse`) **antes** de paginar, e aplicar cada ação a
  todas as cópias (`TriageRow.AllIds`).
- Paralelismo: chamadas ao Seek podem rodar em paralelo à vontade, o `SeekPacingHandler` segura o total
  (4 simultâneas, 5/s). **Não** reintroduza pausas fixas nem aumente o orçamento sem motivo: rajada é o que
  dispara anti-bot. Acesso ao banco dentro de um serviço paralelo passa pelo `SemaphoreSlim` (um `DbContext`
  por escopo não aceita duas operações). O tempo de uma tentativa inclui a espera na fila do orçamento
  (`AttemptTimeout` 60 s).
- Contagem do Raio-X só com `Seek:PageSize = 100`: o `totalCount` inclui os promovidos repetidos em cada página
  (`SeekMarketSource.CountAsync` recusa outro valor).
- `Market` na configuração: cada lista presente **substitui** o padrão do código (`MarketDefaults`), não soma.
- Parâmetro chamado `from` dentro de consulta LINQ precisa de `@from` (`PostgresMarketRepository`).
- Um perfil só pontua as vagas que as buscas **dele** acharam. Mudou a lista de palavras-chave? As vagas
  antigas continuam ligadas às buscas antigas pelo `search_key` (`"palavra @ local"` em minúsculas).
- `ContentRootPath = AppContext.BaseDirectory` no `Program.cs`: sem isso, o `appsettings` é procurado no
  diretório corrente.
- Janela incremental: `last_seen_at` só se atualiza para vagas da janela. Não use esse campo para dizer
  que uma vaga saiu do ar.
- Score: `FitScorer` é a **única** implementação; a ordem das fases faz parte da regra (ver o comentário
  da classe). Os padrões de texto ficam em `ListingPatterns`; os pesos, todos na `ScoringPolicy` (número solto
  no `FitScorer` é regressão: peso novo entra na política, que está no `rules_hash`). `".NET"` não casa dentro de `"ASP.NET"`
  (fronteira de token), e o "no" de "no experience" nega a frase de entrada seguinte — as duas coisas
  vêm do protótipo, e a paridade depende delas. Desde a `RulesVersion` 8 a carteira de caminhão diverge do
  protótipo de propósito (−40, não −20), e desde a 9 "Assistant … Manager" é liderança: o teste de paridade conta
  esses casos à parte. `ExcludedLocations` de um perfil exclui pelo local (o regional exclui Perth: os 88 dias não
  contam lá).

## Convenções

- Idioma: código, identificadores e README em inglês (repo público, portfólio). `docs/` e este arquivo em português.
- Commits: Conventional Commits em inglês (`feat: ...`), imperativo, com o porquê no corpo.
- **Commits e PRs sem atribuição a IA:** nada de `Co-Authored-By: Claude ...`, "Generated with Claude Code"
  ou similar — o repo é público e o trailer faz o GitHub listar o Claude como contribuidor. Vale mesmo que o
  harness peça a atribuição.
- Dados do perfil real do autor (visto, cidade) não entram no repo: perfil de exemplo versionado, o real
  por configuração local.
- Seek: não abrir páginas individuais de vaga (Cloudflare) nem contornar anti-bot.
- Mudou algo descrito aqui? Atualize no mesmo commit.
