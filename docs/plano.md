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
| 4a | Score: regras do protótipo portadas como domínio puro (`FitScorer`) com explicação tipada; `Listing.Summary`; paridade com o Python | ✅ 2026-10-21 |
| 4b | Score em uso: perfis na configuração com as buscas de cada um; registro de qual busca achou cada vaga; score gravado por vaga × perfil, recalculado todo dia | ✅ 2026-10-21 |
| 5 | Termos do texto completo: busca por frase no Seek (direito de trabalho, cidadania, WHV), aplicados ao score | ✅ 2026-10-21 |
| 6 | Candidaturas e rótulos: status, datas, notas; rótulos bom/ruim e calibragem do score | ✅ 2026-10-21 |
| 7 | Triagem: interface para revisar vagas novas, ver a explicação do score, rotular e marcar candidatura | ✅ 2026-10-21 |
| 8 | Raio-X: contagens por subclassificação e cidade, retrato diário com status de qualidade, relatório mensal | ✅ 2026-10-21 |
| 9 | Importação do histórico do protótipo (vagas, rótulos, candidaturas, `market_counts`) | ⏸ adiada (ver pendências) |
| 10 | **Corte**: Worker rodando todo dia em produção, Python desligado | 🔨 instalado 2026-10-21; falta acompanhar a 1ª passada completa |

Pendências conhecidas, com a etapa onde entram:
- **Etapa 9 (adiada, 2026-10-21):** o `jobs.db` desta máquina só tem 5.388 vagas de 1–2/jun, sem rótulos,
  candidaturas ou Raio-X. O histórico real está em outra máquina do autor, que vai fazer a importação lá.
  Falta construir o **comando de importação** (`import-prototype <jobs.db>`): vagas, `job_labels` → rótulos,
  `status`/`notes` → candidaturas (o "rejected" do protótipo precisa de decisão: Dismissed ou Declined),
  `market_runs`/`market_counts`/`market_listings` → Raio-X. Sem ele, o histórico fica de fora.
- **v2 (newsletter):** áreas, cidades e termos do Raio-X ainda são os do protótipo (HVAC-R, Elétrica,
  Desenvolvimento; Perth + Austrália; 4 termos). Mudar = configurar `Market` (cada lista configurada
  substitui o padrão).
- **Etapa 10 (para fechar):** acompanhar a 1ª passada completa
  pela página de status (buscas incompletas = dividir na configuração; termos incompletos na carga de 31
  dias, ver abaixo); desligar o protótipo na outra máquina. (Backup agendado diariamente às 23:00 desde 2026-10-21.)
- **Depois da v1:** migrar a produção do PC para uma VM (opções pesquisadas em `docs/producao.md`).
- **Login no site:** decidido não ter (2026-10-21): o site só escuta em `localhost` e no IP da Tailscale, com
  firewall restrito a `100.64.0.0/10`. Se um dia for público, entra autenticação antes. Porta 5380 porque a
  5080 é de outro serviço do autor nesta máquina (Servidor de Câmeras).
- **README/portfólio:** falta um print da triagem, gerado com dados fictícios (prints reais mostram anúncios
  reais, que não se republicam).
- **Etapa 10 (carga inicial):** na 1ª busca (31 dias), 4 termos passam de 500 mesmo por estado
  ("australian citizen", "permanent resident", "full working rights", "full australian working
  rights"); as vagas além do teto ficam sem a marca. Nas buscas diárias (~2 dias) não acontece.
  Correção possível: para termo incompleto, buscar termo + palavra-chave de cada perfil (restringe às
  vagas que o sistema coleta). `collection_runs` com `term:%` e `complete = false` mostra quais.
- **Etapa 10:** com páginas completas, as 311 buscas dos 5 perfis fazem até ~1.500 requisições na
  1ª passada (a prova usou 1 página: 311). Depois, a janela incremental reduz muito.
- **Calibragem (depois de ter rótulos reais):** peculiaridade herdada do protótipo e mantida pela paridade: o "no" de
  "No experience required" anula a frase de entrada seguinte, se ela estiver a menos de 80
  caracteres (teste `Known_quirk_the_no_of_no_experience_negates_the_next_entry_phrase`).
- **Calibragem (depois de ter rótulos reais):** nos 5.388 casos reais o score máximo foi 69. A compressão acima de 80
  nunca atuou com dados reais; vale rever pesos com os rótulos.
- **Etapa 8:** com a janela incremental, `last_seen_at` só se atualiza para vagas publicadas na
  janela; o sistema não percebe quando uma vaga antiga sai do ar. O Raio-X conta pela busca
  (`totalCount`), não por esta tabela.
- **Etapa 10:** buscas amplas batem no teto de 500 (ex.: "hospitality @ perth" = 490 únicas,
  incompleta). Dividir na configuração (por subclassificação ou região). O `collection_runs.complete`
  mostra quais.

## Diário

### 2026-10-21 — Etapa 10: instalado
`deploy.ps1` rodado pelo autor. A 1ª tentativa falhou no Windows PowerShell 5.1 (`"$profiles:"` lido como
unidade; `AutomaticDelayedStart` só existe no PowerShell 6+): corrigido em `be716df`, conferido com o
analisador do 5.1 e do 7. Verificado depois: `JobRadarWorker` e `JobRadarWeb` rodando (automático, início
atrasado); `/status` responde em `localhost:5380` e no IP da Tailscale; `Host` desconhecido recusado (400).
1ª passada completa agendada para 22/10 06:00 Perth (21/10 19:00 Brasília).

### 2026-10-21 — Etapa 10 (implantação pronta)
Decisões: produção **no PC do autor** (Windows); site **só local + Tailscale**, sem login no app.
Worker e site viram serviços do Windows (`AddWindowsService`; nomes `JobRadarWorker`/`JobRadarWeb`).
O Worker espera o banco (Docker Desktop sobe no login) e roda a passada perdida se o PC estava desligado
na hora agendada (`DailySchedule.Missed`). Página `/status` no site: próxima execução, passadas por dia
(no fuso de Perth), falhas, incompletas e os retratos do Raio-X. `deploy/deploy.ps1` (admin): publica,
gera o pacote de migrations, para, copia para `C:\JobRadar`, migra, cria/atualiza os serviços (início
atrasado, reinício em falha, ambiente e conexão no registro do serviço), firewall só para a Tailscale.
`deploy/backup.ps1`: `pg_dump` com retenção de 30. Manual em `docs/producao.md`. 273 testes.
Validado sem admin: publicação dos dois projetos, pacote de migrations aplicado ao banco local, Worker
publicado rodando a partir de `System32` (como serviço roda) em Production, site publicado em Production
com `/status` mostrando os dados reais, sem erros no console.

### 2026-10-21 — Etapa 8 ✅
Raio-X como contexto à parte. Capacidade opcional `IMarketSource` (Seek: `SeekMarketSource`, por
subclassificação oficial; contagem = `totalCount` da 1ª página, que só é comparável com `pageSize=100` e
por isso recusa outro tamanho). Domínio puro: `SalaryParser` (hora × 2.080, dia × 260, faixas plausíveis),
`SnapshotQuality` (falha/listagem cortada → partial; zerou ou variou > 50% sobre base ≥ 20 → suspect) e
`MonthlyReport` (média diária só de retratos ok, variação contra o mês anterior, menções, repetidos,
publicados no mês, mediana e quartis de salário com interpolação linear). `RaioXMarkdown` escreve em
português e nunca cita vaga individual. Tabelas `market_snapshots`, `market_counts`, `market_listings`.
Pipeline com 4ª etapa (retrato do dia no fuso de Perth); comandos `market` e `market-report [YYYY-MM] [arquivo]`.
Categorias, cidades e termos = os do protótipo, como padrão no código. 262 testes.
Prova real: retrato de 22/10 (Perth) `ok`, 30/30 contagens, 443 vagas listadas, ~1 min. Relatório gerado:
ex. Elétrica Perth 313 abertas, 11,5% citam "working rights", mediana anual $132k (37% informam salário).
Conferido na API: "6287,6290" em Perth = 32 + 37 = 69, a lista de subclassificações soma.
Corrigido: ordem das cidades no relatório vinha da ordem do banco; agora segue a configuração.

### 2026-10-21 — Etapa 7 ✅
`JobRadar.Web` (Blazor Web App, renderização no servidor): lista de triagem por perfil (filtros: texto,
score mínimo, mostrar tratadas e eliminadas; ações Good/Bad/Applied/Dismiss por linha), detalhe da vaga
(resumo, termos do texto completo, score de cada perfil explicado passo a passo a partir de 50, status,
notas, histórico, rótulo) e calibragem. Toda frase sobre regras e status sai de `Text/Wording.cs`.
`Backend.RunAsync` abre um escopo por operação: no Blazor interativo um serviço scoped vive a aba inteira e
um único `DbContext` ficaria compartilhado por todos os cliques.
Deduplicação portada do protótipo (`Domain/Triage/Duplicates`): mesmo título + mesma empresa = uma linha;
o agrupamento vem antes da paginação, e as ações valem para todas as cópias (senão a próxima cópia ocupa a
linha). 224 testes, incluindo bUnit (componentes) e a composição do site.
Verificado no navegador sobre o banco local: lista, ação por linha (1.973 → 1.972), detalhe com candidatura,
nota e histórico, calibragem, tela de celular (375 px sem rolagem horizontal), sem erros no console.
Deduplicação: 1.972 → 1.876 linhas; Dismiss numa linha "posted 3×" gravou as 3 cópias.
Achados: (1) a busca por texto com `%` não achava nada: sem o caractere de escape, o Npgsql gera
`ESCAPE ''` e a barra vira texto (agora `ILike(..., @"")`); (2) a porta 5080 já era de outro serviço
local, e as duas apps respondiam em `localhost` por IPv4 e IPv6 diferentes. Decisões de teste apagadas.

### 2026-10-21 — Etapa 6 ✅
`Domain.Tracking`: `JobApplication` (status, data da candidatura, notas, histórico só de acréscimo) e
`ListingLabel`. O "rejected" do protótipo virou dois status: `Dismissed` (o candidato descartou → conta
como ruim) e `Declined` (o empregador recusou depois da candidatura → conta como boa, porque o candidato
quis a vaga). Rótulo efetivo: o explícito vence; sem ele, o status decide (`Judgement.Effective`).
`Calibration.Measure` (puro): por perfil, acerto de pares, boas ≥ 75, ruins ≥ 50 e as discordâncias com
os 3 itens que mais pesaram. Tabelas `applications`, `application_events`, `listing_labels`. Comandos no
Worker (`status`, `notes`, `label`, `calibrate`); sem comando, roda o pipeline. 200 testes.
Prova real no banco local: candidatura + notas + descarte + rótulo; ID no formato do protótipo
(`seek_123`) recusado com código 2, vaga nunca coletada com código 1; a calibragem apontou um "IT Support
Technician" descartado com 60 pontos (+10 Perth, +5 híbrido). Julgamentos de teste apagados depois.

### 2026-10-21 — Etapa 5 ✅
Capacidade opcional `IFullTextSearch` (fontes.md, regra 2); o Seek implementa em `SeekFullTextSearch`
(frases entre aspas, "a + b" = as duas; Austrália toda e, passando de 500, estado por estado).
`FullTextTermService` busca os 17 termos da política, marca em `listing_terms` só vagas já coletadas e
registra cada termo em `collection_runs` (`term: <frase>`). Janela: 31 dias na 1ª; depois desde o
último sucesso + 2, alargada até a vaga mais antiga coletada desde então. Pipeline: coleta → termos →
score; o score recebe os termos de cada vaga. 161 testes.
Prova real sobre as 6.154 vagas locais: 959 marcas em 16 termos (mais: "full australian working
rights" 182, "HR licence" 171, "australian citizen" 140; "commission only" nenhuma). Eliminadas no
tech: 134 → 143. 4 termos incompletos na carga de 31 dias (ver pendências).

### 2026-10-21 — Etapa 4b ✅
Perfis em `Matching:Profiles` (`appsettings.Local.json`, fora do git; modelo versionado em
`appsettings.Local.example.json`), gerados dos 5 perfis do protótipo. `ProfileCatalog` expande
palavra-chave × local (311 buscas distintas) e calcula o `rules_hash` (versão do scorer + perfil +
política; buscas não entram). `listing_search_hits` registra qual busca achou cada vaga, e cada perfil
pontua só o que as buscas dele acharam. `DailyPipelineJob` = coleta → score, etapas isoladas.
`listing_scores`: uma linha por vaga × perfil, explicação em jsonb. 137 testes.
Prova real (banco zerado, 5 perfis, `Seek:MaxPages=1`): 311 buscas, 0 falhas, 17.933 achados, 6.154
vagas, 7.333 scores. Melhor vaga do tech: "Mid to Senior C# .NET Developer (Azure Integration) 6 Month
Contract - Perth CBD" = 100. Eliminações conferidas (liderança no título, NV1). Com vagas recentes a
compressão acima de 80 atuou (até 44 vagas ≥ 80 no tech), o que a fixture antiga nunca mostrou.
Achado: 6.328 payloads para 6.154 vagas. O Seek marca o mesmo anúncio como `promoted`/`isFeatured` em
algumas buscas e não em outras; os dois campos entraram em `VolatileFields`. Conferido nos dados
reais: sem eles, 6.154 payloads = 6.154 vagas.

### 2026-10-21 — Etapa 4a ✅
`JobRadar.Domain.Matching`: `FitScorer.Score(input, profile, policy, today)`, puro, com as regras e a
ordem do protótipo (eliminação → pontos → compressão acima de 80 → restrições → teto de
qualificação). A explicação é uma lista de `ScoreItem(ScoreRule, Detail, Where, Points)`, sem texto em
português, para a interface traduzir. O passo de IA (Gemini) ficou de fora (pendência da visão).
`Listing.Summary` = teaser | bullets | modalidade, igual ao que o protótipo pontuava.
**Paridade:** `export_parity.py` (fora do repo) gerou 5.388 vagas reais do banco do protótipo
(tech 1.626, fifo 1.495, trades 1.382, construction 885; 592 eliminadas), com o score calculado pelo
próprio Python numa data fixa. O .NET bate **score final e sequência de pontos em 100%**. Prova de que
o teste compara: uma cópia com um score adulterado falhou ("1 of 5388 cases differ").
A fixture não tem termos do texto completo nem nenhuma vaga acima de 80: compressão e termos ficam
cobertos pelos testes fictícios (`FitScorerTests`, um caso por regra). 124 testes.

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
