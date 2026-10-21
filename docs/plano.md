# Plano — v1 (uso próprio)

> **Documento vivo.** Estado real, em ordem. Ao fechar uma etapa: marque ✅ com a data e o que
> ficou provado, registre no Diário e passe a próxima para 🔨.
> Escopo e fases: [visao.md](visao.md). Fontes: [fontes.md](fontes.md).

## Etapas

| # | Etapa | Estado |
|---|---|---|
| 1 | Esqueleto: camadas, `IListingRepository` + Postgres, testes de contrato, composição, CI | ✅ 2026-10-21 |
| 2 | Integração Seek: porta `IJobSource`, cliente HTTP com resiliência, tradutor para `Listing` testado com respostas gravadas | ✅ 2026-10-21 |
| 3 | Coleta: serviço que roda as buscas configuradas, grava vagas + payload bruto, job agendado no Worker, log de execuções | ✅ 2026-10-21 |
| 4 | **Score**: perfil como dado; regras do protótipo portadas como domínio puro com explicação; teste de paridade contra o banco do Python | 🔨 próxima |
| 5 | **Termos do texto completo**: busca por frase no Seek (direito de trabalho, cidadania, WHV), aplicados ao score | ⏳ |
| 6 | **Candidaturas e rótulos**: status, datas, notas; rótulos bom/ruim e calibragem do score | ⏳ |
| 7 | **Triagem**: interface para revisar vagas novas, ver a explicação do score, rotular e marcar candidatura | ⏳ |
| 8 | **Raio-X**: contagens por subclassificação e cidade, retrato diário com status de qualidade, relatório mensal | ⏳ |
| 9 | **Importação do histórico** do protótipo (vagas, rótulos, candidaturas, `market_counts`) | ⏳ |
| 10 | **Corte**: Worker rodando todo dia em produção, Python desligado | ⏳ |

Pendências conhecidas, com a etapa onde entram:
- **Etapa 4:** o score do protótipo lê o texto do anúncio (teaser + bullets). Hoje esse texto só
  existe no payload bruto; a etapa decide se o `Listing` ganha um campo `Summary` ou se o score lê
  o payload.
- **Etapa 8:** com a janela incremental, `last_seen_at` só se atualiza para vagas publicadas na
  janela; o sistema não percebe quando uma vaga antiga sai do ar. O Raio-X conta pela busca
  (`totalCount`), não por esta tabela.
- **Etapa 10:** buscas amplas batem no teto de 500 (ex.: "hospitality @ perth" = 490 únicas,
  incompleta). Dividir na configuração (por subclassificação ou região). O `collection_runs.complete`
  mostra quais.

## Diário

### 2026-10-21 — Etapa 3 ✅
`ListingCollector` (Application) roda cada busca em cada fonte; falha numa busca é registrada e a
próxima segue. Janela incremental = dias desde o último sucesso + 1 (falha não avança a janela).
`DailyCollectionJob` (Worker) agenda em horário local (`Collection:DailyAt`, fuso `Australia/Perth`),
com `RunOnStartup` para desenvolvimento. Tabelas novas: `listing_payloads` (jsonb, uma linha por
conteúdo distinto, hash SHA-256) e `collection_runs`. 78 testes (1 ao vivo, opt-in).
Prova real com Worker + Postgres local + API do Seek, 3 buscas de Perth:
- 1ª passada: 541 encontradas, 528 novas, 13 em comum entre ".net" e "c#"; hospitality incompleta (teto).
- 2ª passada: janela de 1 dia, 1 encontrada, 0 nova; **528 vagas = 528 payloads**.
Achados:
- O Seek carimba cada anúncio com `tracking` e `solMetadata` por requisição, e `listingDateDisplay`
  ("2d ago") muda todo dia. Sem tirar esses campos, cada coleta duplicava todo payload (541 para 528
  na 1ª prova). `SeekSearchClient.StableRaw` remove os três antes do hash.
- Rodando a DLL de outra pasta, o host procurava o `appsettings` no diretório corrente. Agora o
  `ContentRootPath` é `AppContext.BaseDirectory` (no Windows, um serviço roda em `System32`).
- Logs de SQL do EF e de cada tentativa do Polly em Information eram ruído: subiram para Warning.

### 2026-10-21 — Etapa 2 ✅
`JobRadar.Integrations.Seek`: `SeekSearchClient` (HttpClient tipado + resiliência padrão: 3 tentativas
com backoff em 429/5xx, respeitando Retry-After), `SeekListingTranslator`, `SeekJobSource` (paginação,
deduplicação dos promovidos, teto de páginas). 53 testes, todos verdes com `JOBRADAR_DB_TESTS=1`.
Teste ao vivo (`JOBRADAR_LIVE_TESTS=1`) passou contra a API real pela composição completa.
Achados na API real (amostra de 100 vagas de hospitality, AU inteira):
- Local tem até três níveis ("Osborne Park, Perth WA") e a região pode conter vírgula
  ("Austinmer, Wollongong, Illawarra & South Coast NSW") → `ListingLocation` virou `Suburb` + `Region`
  (migration `SplitLocationIntoSuburbAndRegion`, que renomeia `city` → `region`).
- 98 IDs únicos em 100 itens: promovidos se repetem entre páginas.
- 9% sem `companyName` → cai para o anunciante.
- A API aceita um User-Agent honesto (`JobRadarAU/0.1 (+repo)`); não é preciso fingir ser navegador.
CI: `actions/checkout@v7` e `actions/setup-dotnet@v6` (Node 24). CI verde (`3b1a471`).

### 2026-10-21 — Etapa 1 ✅
Esqueleto publicado em `DaviLuisSantos/job-radar-au` (commit `2c18dc3`). CI verde no primeiro push:
build Release + 27 testes, incluindo os 5 de contrato contra Postgres 18 no runner.
