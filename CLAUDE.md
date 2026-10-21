# JobRadar AU

Acompanha o mercado de trabalho australiano para quem chega com Working Holiday (WHV).
Dois produtos sobre um núcleo comum: **Matching** (privado: ranqueia vagas para um perfil,
explica o score, acompanha candidaturas) e **Raio-X** (público: só números agregados, base
da newsletter Raio-X WHV em português). Reescrita em .NET de um protótipo Python
(`D:\Desenvolvimento\job-tracker`), que é a especificação das regras de negócio.

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
src/JobRadar.Domain          modelo puro (Listings/, Collection/, Matching/) — sem EF, HTTP ou integração
src/JobRadar.Application     serviços (Collection/) + portas em Abstractions/
src/JobRadar.Infrastructure  Postgres/<Agregado>/: Entity → Configuration → Mapper → Repository
src/JobRadar.Integrations.Seek  IJobSource do Seek: cliente (Api/), tradutor, paginação
src/JobRadar.Worker          composição raiz (JobRadarComposition.AddJobRadar) e jobs
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

`DailyPipelineJob`, às `Collection:DailyAt` no fuso `Collection:TimeZone`, roda duas etapas isoladas
(uma falha não impede a outra):

**Coleta** — `ListingCollector.CollectAsync(ProfileCatalog.Searches)`:
1. para cada fonte e busca, calcula a janela incremental pelo último sucesso;
2. `IJobSource.SearchAsync`, que devolve as vagas únicas e diz se a busca veio completa;
3. `IListingRepository.RecordObservationsAsync` grava vagas novas, atualiza as conhecidas, guarda o
   payload só quando o conteúdo mudou e registra que aquela busca achou a vaga (`listing_search_hits`);
4. registra a execução em `collection_runs`, com sucesso ou erro. Falha numa busca não para as outras.

**Score** — `ListingScoringService.ScoreAllAsync`: para cada perfil, pontua com `FitScorer` todas as
vagas que **as buscas daquele perfil** acharam e grava em `listing_scores` (uma linha por vaga × perfil,
com a explicação em jsonb e o `rules_hash`). Recalcula tudo todo dia, porque a idade muda o score.

Perfis: `Matching:Profiles` em `src/JobRadar.Worker/appsettings.Local.json` (**fora do git**; modelo em
`appsettings.Local.example.json`). Cada palavra-chave é buscada em cada local; as buscas da coleta são a
união dos perfis.

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
- Um perfil só pontua as vagas que as buscas **dele** acharam. Mudou a lista de palavras-chave? As vagas
  antigas continuam ligadas às buscas antigas pelo `search_key` (`"palavra @ local"` em minúsculas).
- `ContentRootPath = AppContext.BaseDirectory` no `Program.cs`: sem isso, o `appsettings` é procurado no
  diretório corrente.
- Janela incremental: `last_seen_at` só se atualiza para vagas da janela. Não use esse campo para dizer
  que uma vaga saiu do ar.
- Score: `FitScorer` é a **única** implementação; a ordem das fases faz parte da regra (ver o comentário
  da classe). Os padrões de texto ficam em `ListingPatterns`. `".NET"` não casa dentro de `"ASP.NET"`
  (fronteira de token), e o "no" de "no experience" nega a frase de entrada seguinte — as duas coisas
  vêm do protótipo, e a paridade depende delas.

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
