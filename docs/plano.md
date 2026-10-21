# Plano — v1 (uso próprio)

> **Documento vivo.** Estado real, em ordem. Ao fechar uma etapa: marque ✅ com a data e o que
> ficou provado, registre no Diário e passe a próxima para 🔨.
> Escopo e fases: [visao.md](visao.md). Fontes: [fontes.md](fontes.md).

## Etapas

| # | Etapa | Estado |
|---|---|---|
| 1 | Esqueleto: camadas, `IListingRepository` + Postgres, testes de contrato, composição, CI | ✅ 2026-10-21 |
| 2 | Integração Seek: porta `IJobSource`, cliente HTTP com resiliência, tradutor para `Listing` testado com respostas gravadas | ✅ 2026-10-21 |
| 3 | **Coleta**: serviço que roda as buscas configuradas, grava vagas + payload bruto, job agendado no Worker, log de execuções | 🔨 próxima |
| 4 | **Score**: regras do protótipo portadas como domínio puro com explicação; teste de paridade contra o banco do Python | ⏳ |
| 5 | **Termos do texto completo**: busca por frase no Seek (direito de trabalho, cidadania, WHV), aplicados ao score | ⏳ |
| 6 | **Candidaturas e rótulos**: status, datas, notas; rótulos bom/ruim e calibragem do score | ⏳ |
| 7 | **Triagem**: interface para revisar vagas novas, ver a explicação do score, rotular e marcar candidatura | ⏳ |
| 8 | **Raio-X**: contagens por subclassificação e cidade, retrato diário com status de qualidade, relatório mensal | ⏳ |
| 9 | **Importação do histórico** do protótipo (vagas, rótulos, candidaturas, `market_counts`) | ⏳ |
| 10 | **Corte**: Worker rodando todo dia em produção, Python desligado | ⏳ |

Pendências pequenas que entram junto da etapa em andamento:
- Etapa 3: `IListingRepository` passa a receber `SourcedListing` e guardar o payload bruto
  (deduplicado por hash, para não crescer a cada coleta).
- Etapa 3: a busca que bate no teto de 5 páginas hoje só loga um aviso; a coleta precisa saber
  disso (o Raio-X não pode usar listagem incompleta).

## Diário

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
CI: `actions/checkout@v7` e `actions/setup-dotnet@v6` (Node 24).

### 2026-10-21 — Etapa 1 ✅
Esqueleto publicado em `DaviLuisSantos/job-radar-au` (commit `2c18dc3`). CI verde no primeiro push:
build Release + 27 testes, incluindo os 5 de contrato contra Postgres 18 no runner.
