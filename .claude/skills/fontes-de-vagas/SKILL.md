---
name: fontes-de-vagas
description: Coleta de vagas do JobRadar e as integrações (Seek, Gupy, LinkedIn, Himalayas, Jobicy): ListingCollector, termos do texto completo, descrições completas, Raio-X, pacing e anti-bot. Use antes de mexer numa src/JobRadar.Integrations.*, na coleta, nos termos, nas descrições, no Raio-X, ou quando a coleta começar a falhar.
---

# Fontes de vagas e coleta

Rode o teste ao vivo ao mexer numa `Integrations.*`, ou quando a coleta começar a falhar:

```bash
JOBRADAR_LIVE_TESTS=1 dotnet test --project tests/JobRadar.Worker.Tests
```

Teste ao vivo (`[LiveFact]`, `JOBRADAR_LIVE_TESTS=1`) chama a API real do Seek, da Gupy, do LinkedIn, do Himalayas e do
Jobicy pela composição completa. Fora do CI de propósito.

## Etapas do pipeline

**Varredura de quadros** — `IBoardSweep.SweepAsync`, etapa "Board sweeps" do `DailyPipeline`, **antes** da coleta. Só
para fonte lida por quadro inteiro, não por palavra-chave (hoje o Greenhouse, `AtsBoardSweepService`), e só se algum
perfil a citar em `ExtraSources`. Lê da tabela `ats_boards` a fatia de quadros varridos há mais tempo
(`Ats:MaxBoardsPerSweep`), manda o `ETag` de cada um (304 = sem corpo; `collect --full` ignora os tags), guarda em memória
(`AtsSnapshot`) só as vagas que caem em AU, BR ou remoto LATAM/global, grava o resultado em `ats_boards` e uma linha em
`collection_runs` com chave `ats: sweep greenhouse`. A `SearchAsync` da fonte só filtra o snapshot (mercado + todas as palavras
no título ou departamento); sem varredura nesta passada devolve `Complete = false`, não "nada achado".

**Coleta** — `ListingCollector.CollectAsync(ProfileCatalog.Searches)`:
1. para cada fonte e cada busca **de um mercado que a fonte serve** (`IJobSource.Markets`, `Market` do perfil), calcula a janela
   incremental pelo último sucesso. Fonte opcional (`IJobSource.OptIn`, o LinkedIn) só roda as buscas dos perfis que a citam
   em `ExtraSources`; nome desconhecido ali para a coleta;
2. `IJobSource.SearchAsync`, que devolve as vagas únicas e diz se a busca veio completa;
3. `IListingRepository.RecordObservationsAsync` grava vagas novas, atualiza as conhecidas, guarda o
   payload só quando o conteúdo mudou e registra que aquela busca achou a vaga (`listing_search_hits`);
4. grava em `listing_descriptions` o anúncio inteiro que a própria busca devolve (`SourcedListing.Description`: a Gupy
   devolve, o Seek não), só quando o texto muda;
5. registra a execução em `collection_runs`, com sucesso ou erro. Falha numa busca não para as outras.

**Termos** — `FullTextTermService.TagAsync`: cada frase de `MarketRules.FullTextGroups` de cada mercado (as do código,
comissão e carteira, mais as `Phrases` do bloco de elegibilidade do mercado) e dos requisitos de cada perfil
(`ProfileRequirements`: "frase + palavra de contexto", para caber no teto de 500; os grupos de
`ScoringPolicy.ContextGroups` também vão com cada palavra de contexto) é
buscada na Austrália toda por cada fonte com a capacidade `IFullTextSearch` (o Seek tem: a busca dele cobre
o anúncio inteiro). As vagas que já estão no banco ganham a marca em `listing_terms`. Cada termo é uma
execução em `collection_runs` com chave `term: <frase>`. Janela: 31 dias na primeira vez; depois, dias desde o
último sucesso + 2, alargada até a vaga mais antiga coletada desde então.

**Descrição completa** — `DescriptionFetchService`, na passada, busca o texto pelo GraphQL `jobDetails` do Seek
(`SeekJobDetailsClient`, mesmo orçamento da busca): primeiro as vagas rotuladas ou com status, qualquer score ou
idade; depois as de melhor score ≥ `Descriptions:MinScore`, publicadas há até `MaxAgeDays`, até `MaxPerPass`.
Recusa do Seek (`SourceBlockedException`) para a etapa, não a passada. Além disso, o autor pode capturar o texto
do anúncio que ele mesmo abriu (favorito da página `/capture` ou colar na página da vaga); `DescriptionService`
grava em `listing_descriptions` e repontua na hora (`ScoreOneAsync`). O site lê os perfis do worker (`profiles.json`,
copiado pelo build). Texto de uso pessoal: nunca no Raio-X, no repo ou em fixture.

**Raio-X** — `MarketSnapshotService.TakeAsync(dia)`: por categoria (`Market:Categories`, código de cada fonte) e
cidade, conta as vagas abertas e as que citam cada termo; nas `FullCities` lista todas. Contagem que falha tem
uma segunda chance. `SnapshotQuality` dá `ok | partial | suspect`; **o relatório mensal só usa `ok`**
(`MarketReportService` + `RaioXMarkdown`). Só números agregados: nada de título, empresa ou link no texto.

## Armadilhas

- ATS (Greenhouse): **a varredura não pode ficar dentro da `SearchAsync`**. O coletor roda 6 buscas em paralelo e uma que espera
  minutos prende uma vaga (e, com a fonte primeiro, as outras fontes não rodariam). Por isso é etapa própria, com escopo e
  `DbContext` próprios: é a única escrita no banco fora do `SemaphoreSlim` do coletor, de propósito. Singleton com repositório
  scoped quebra o container (`ValidateOnBuild`): quem varre é `AddScoped`, e o snapshot é um singleton sem banco.
- ATS: o id da vaga é `slug/id` **sem hash**, porque a etapa de descrição monta `/v1/boards/{slug}/jobs/{id}` a partir dele;
  slug que não cabe em `source_id` (64) é recusado e contado. `IDescriptionSource.Source` tem de ser igual a `ListingId.Source`
  (minúsculas), ou a etapa devolve zero em silêncio.
- ATS: **404 é o estado normal** de um quadro morto (metade da lista): conta falha em `ats_boards` e não torna a varredura
  incompleta. Só timeout, erro 5xx ou conexão caída deixam `Complete = false`, e esses não aposentam o quadro.
- ATS: o Greenhouse manda o anúncio com o HTML **escapado** (`&lt;p&gt;`). `HtmlText.ToPlain` tira as tags e só depois decodifica, então
  o texto precisa de um `HtmlDecode` antes, ou as tags sobrevivem no texto guardado (`GreenhouseDescriptionSource`).
- ATS: a janela incremental é ignorada (como no Jobicy) e `collection_runs.posted_within_days` não vale para ele. Para o
  Greenhouse, `last_seen_at` é verdadeiro (a varredura vê o quadro todo); para as outras fontes a armadilha abaixo continua.
- ATS: "Remote" sozinho é descartado (os quadros querem dizer um país, quase sempre os EUA); só entra remoto com Brasil/LATAM
  ou "Anywhere/Worldwide". Perfil com palavra nova só vê os quadros que dizem "sem mudança" depois de um `collect --full`.
- `ListingId` normaliza a fonte (`Trim` + minúsculas): `"SEEK"` e `"seek"` são a mesma fonte.
- Seek repete anúncios promovidos em toda página: `SeekJobSource` deduplica por ID dentro da busca.
  Por isso `totalCount` não bate com o número de vagas devolvidas.
- Local do Seek: divida no **primeiro** `", "` (`SeekListingTranslator.ParseLocation`), porque há regiões
  com vírgula no nome.
- Campo novo e volátil no payload do Seek duplica `listing_payloads` a cada coleta. Sintoma: payloads >
  vagas depois de duas passadas. Inclua o campo em `SeekSearchClient.VolatileFields`.
- `listing_payloads.payload` é jsonb, que normaliza espaços e ordem das chaves: o hash é do texto recebido
  (já sem os campos voláteis), não do que o banco devolve.
- Termos: a busca do Seek não dá o contexto da frase (a frase negada também casa). Por isso os
  pesos dos grupos são moderados e só o grupo `exclude` elimina. A divisão por estado quando passa de 500 é
  peculiaridade do Seek e fica em `SeekFullTextSearch`, não no serviço.
- Paralelismo: chamadas ao Seek podem rodar em paralelo à vontade, o `SeekPacingHandler` segura o total
  (4 simultâneas, 5/s). **Não** reintroduza pausas fixas nem aumente o orçamento sem motivo: rajada é o que
  dispara anti-bot. Acesso ao banco dentro de um serviço paralelo passa pelo `SemaphoreSlim` (um `DbContext`
  por escopo não aceita duas operações). O tempo de uma tentativa inclui a espera na fila do orçamento
  (`AttemptTimeout` 60 s).
- LinkedIn: fonte opcional, só nos perfis com `"ExtraSources": ["linkedin"]` (a primeira passada sem janela pode passar de
  mil pedidos). Ligar num perfil não muda a chave das buscas: o histórico continua. Orçamento próprio (`LinkedInPacing`, 1/s, 2 simultâneas), separado do Seek. A primeira recusa (403, 429, 999 ou
  redirecionamento para `/authwall`) bloqueia **todo** pedido ao LinkedIn por 6 horas: a coleta roda todas as buscas mesmo
  quando uma falha, e insistir é o que vira bloqueio do IP. Local do Brasil em inglês (", Brazil"): "Brasil" e local
  desconhecido caem nos EUA, e a busca falha dizendo que não reconheceu o local. A busca para numa página curta, numa página cheia que só repete IDs já conhecidos (conta como completa) ou
  no `MaxPages`. Página sem cartão que cita vagas, ou cartões sem título, falham a busca (layout mudou), não viram "sem
  resultados". Nada do LinkedIn vai ao Raio-X.
- Contagem do Raio-X só com `Seek:PageSize = 100`: o `totalCount` inclui os promovidos repetidos em cada página
  (`SeekMarketSource.CountAsync` recusa outro valor).
- Janela incremental: `last_seen_at` só se atualiza para vagas da janela. Não use esse campo para dizer
  que uma vaga saiu do ar.
