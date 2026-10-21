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
| 9 | Importação do histórico do protótipo (vagas, rótulos, candidaturas, `market_counts`) | 🔨 comando pronto 2026-10-21; falta rodar com o `jobs.db` da outra máquina |
| 10 | **Corte**: Worker rodando todo dia em produção, Python desligado | 🔨 instalado 2026-10-21; falta acompanhar a 1ª passada completa |

Pendências conhecidas, com a etapa onde entram:
- **Etapa 9 (para fechar):** trazer o `jobs.db` da outra máquina e rodar `import-prototype` aqui (o banco de
  produção está neste PC). Passo a passo em `docs/producao.md`. O `jobs.db` desta máquina só tem 5.388 vagas
  de 1–2/jun, sem rótulos, candidaturas ou Raio-X: não foi importado no banco de produção.
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
- **Etapa 10:** com páginas completas, as buscas dos 5 perfis (256 desde 2026-10-21; eram 311) fazem até
  ~1.300 requisições numa passada sem janela. Depois, a janela incremental reduz muito.
- **Acerto do score (decidido 2026-10-21, nesta ordem):** (1) rotular: meta de 50 julgadas por perfil, com
  a ordem "across scores" da triagem para não rotular só o topo; (2) ajustar regras e pesos com o
  `calibrate` antes e depois; (3) descrição completa (v1.x, abaixo); (4) ML (v2/v3). **Fontes novas
  esperam:** não melhoram o ranking e mudariam os dados no meio da calibragem. A primeira será o Adzuna
  (API oficial), mais pelo Raio-X do que pela busca pessoal.
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

## Depois da v1 (registrado 2026-10-21)

Ideias decididas como direção, sem data. Entram como etapas quando a v1 fechar.

### v1.x — Descrição completa das vagas que importam

As páginas de vaga do Seek seguem atrás do Cloudflare e **contornar continua fora**. O texto completo
entra pelo navegador do próprio autor, que abre a vaga como qualquer pessoa:

1. **"Enviar para o JobRadar"**: bookmarklet que lê o texto da página aberta no navegador e envia ao site
   local (`POST` para `localhost:5380`/Tailscale). Cobre exatamente as vagas do topo da triagem.
2. **Campo "colar descrição"** no detalhe da vaga: a mesma coisa, manual, como reserva.
3. O texto vira `FullDescription` da vaga (separado do `Summary`), as regras do `FitScorer` passam a lê-lo
   (todas já leem o corpo: cidadania, "12 months", carteira, anos de experiência) e a vaga é repontuada na hora.

Cuidados: o texto fica só no banco do autor (uso pessoal); nunca entra no Raio-X, no repositório nem em
fixture. A busca por frase (etapa 5) continua para as vagas sem descrição.

### v2/v3 — Modelo de ML para o fit

Pré-requisito: **rótulos**. Hoje há zero; a meta é ~200–300 por perfil, com boas e ruins. A métrica e os
atributos já existem: o acerto de pares da calibragem (etapa 6) e os itens da explicação do score.

1. **Regras como trava**: exclusões (cidadania/PR, direito de trabalho incompatível com WHV, vaga reservada)
   continuam decidindo; o modelo só ordena o que sobra.
2. **Modelo simples e explicável primeiro**: regressão logística (ML.NET) sobre os itens da explicação + palavras
   do título/resumo (e da descrição completa, quando houver). Aprende os pesos que hoje são fixos.
   Só substitui as regras se ganhar no acerto de pares em rótulos separados para teste.
3. **Depois, se valer**: embeddings de texto (sinônimos: "Junior .NET" ≈ "Graduate C#") + classificador pequeno.
4. **Aprendizado ativo na triagem**: mostrar primeiro as vagas em que o modelo tem mais dúvida.

Cuidado com o viés de seleção: só se rotula o que aparece no topo. Rotular de vez em quando vagas de score
baixo, ou o modelo só aprende a separar "boa" de "quase boa".

## Diário

### 2026-10-22 — Calibragem de construction e trades (`RulesVersion` 5)
Rótulos: construction 15 boas / 21 ruins (62%), trades 7 / 10 (60%). As ruins com score alto eram trades
qualificados que o título entrega (glazier, floor layer, tiler, polywelder, tradesperson, splicer).
Duas correções de domínio: (1) uma frase de requisito achada com **qualquer** palavra de contexto conta
para todo perfil que a tem ("HC licence + FIFO" é carteira HC no anúncio, também para construction: era o
caso das "Operator Assistant" da Halliburton); (2) "leading hand(s)" deixou de ser título de entrada: o
"hands?" (kitchen/farm hand) tirava "FIFO Shift Electricians / Leading Hands" do teto. Paridade mantida.
Configuração local: títulos acima nos dois perfis, "assembler" e "leading hand" no trades, "concreter"
saiu do construction (a "FIFO Concreters" rotulada boa ia a 35). Simulação: construction 62% → **79%**,
trades 60% → **97%**, fifo 82% → 81% (efeito da correção 1).
Testado e descartado: "experienced"/"experience preferred" como sinal negativo (76%: derruba "Entry Level
and Experienced Offsiders", boa). O que o resumo não explica (Driller's Offsider de Swick, Ozijobs) está no
corpo do anúncio: é o caso da captura da descrição completa (v1.x).

### 2026-10-21 — "Remote" é lugar, e diploma também é requisito (`RulesVersion` 4)
Passada com requisitos: fifo 42% → **80%** de acerto de pares (5/8 boas ≥ 75, 8/33 ruins ≥ 50; 107 vagas
com 70+, eram 554); construction 57% → 67%; 95 buscas de requisito completas, 4.804 marcas.
Caso apontado pelo autor: "FIFO-Social Worker" com 84, pedindo diploma. Três falhas: (1) +10 de "remote":
o resumo inclui o formato de trabalho do Seek, e em vaga de campo o anunciante marca "Remote" pelo local
(remote mine, remote communities); só 5 de 116 bônus no fifo pareciam trabalho de casa. Agora
`RewardRemote` por perfil, desligado em fifo, construction, trades e regional. (2) Diploma não era
procurado: o grupo "TradeCertificate" virou **"Qualification"** e ganhou "degree in" (225 com FIFO),
"tertiary qualification", "relevant degree". "degree" sozinho e "bachelor" deram contagens estranhas
com palavra solta e ficaram de fora. (3) "demonstrated experience" (510 com FIFO) entrou em experiência;
"social worker", "counsellor", "psychologist", "paramedic", "teacher", "nurse" nos títulos do fifo.
O rename muda o nome da regra gravada na explicação: entre o deploy e a passada seguinte, explicações
antigas com `FullTextTradeCertificate` não se leem (a passada regrava todas).
Pendente: "insurance" nos sinais negativos do fifo pega benefício ("income protection insurance").

### 2026-10-21 — Calibragem do fifo e requisitos no texto completo (`RulesVersion` 3)
Primeira calibragem com rótulos reais: **fifo 8 boas, 33 ruins, acerto de pares 42%** (pior que sorteio),
30 das 33 ruins com 50+. Causa 1: o perfil premiava sinais que toda vaga FIFO tem (FIFO, roster, 2:1, 8:6,
mining, Pilbara...), então tudo ia para 86–100. Causa 2, dita pelo autor: as ruins pedem anos de
experiência, certificado de trade, carteira HR ou experiência com máquina específica, e isso está no corpo
do anúncio. Simulação nos rótulos (fora do repo, `FitScorer` puro): tirar os sinais de contexto 42% → 70%;
mais títulos qualificados (operator, driver, planner, coordinator, advisor, trainer...) → 80%. Aplicado no
perfil local do fifo.
Código novo: `ProfileRequirements` por perfil (experiência, certificado de trade, licença), buscado no texto
completo como "frase + palavra de contexto" ("years experience + FIFO"): sozinhas essas frases passam de 500
por estado (ex.: "years experience" 14.951), com o contexto ficam em dezenas a centenas. −20 por tipo
encontrado, uma vez; licença de caminhão já contada e certificado em título de formação não pesam de novo.
Contextos locais: fifo "FIFO"; construction "labourer", "offsider"; trades "trade assistant", "trades
assistant", "offsider" ("assistant" sozinho pega dental/admin). 19 frases por perfil.
Fica para os rótulos dizerem: o −20 é moderado porque a busca não vê "cert III an advantage".

### 2026-10-21 — Scores órfãos
A 1ª passada com a regra WHV (106 s, 256 buscas, 0 cortadas) mostrou o efeito colateral de tirar "All
Australia": **6.305 scores congelados** (vagas que só aquelas buscas achavam), com a regra antiga e sem
envelhecer, ainda na triagem (no tech, 1.830 de 2.182). Agora a passada de score apaga, no fim, todo score
que ela não gravou (`IListingScoreRepository.DeleteScoredBeforeAsync`): cobre busca que saiu do perfil e
perfil que saiu da configuração. A vaga, os rótulos e as candidaturas ficam; ela só sai da triagem daquele
perfil. Comparação truncada a microssegundos (o Postgres guarda µs; o .NET, 100 ns). 2 vagas rotuladas
ficam sem score em perfil nenhum e saem da calibragem até alguma busca voltar a achá-las.
Regra WHV na mesma passada: 29 vagas do banco com WHV + cidadania/residência; 19 pontuações (vaga × perfil)
levaram −55, nenhuma ficou com 70+. As 3 Driller's Offsider rotuladas como ruins foram de 95/92 para 35/32.

### 2026-10-21 — WHV citado para recusar (`RulesVersion` 2)
Na triagem, o autor viu se repetir: anúncio pede cidadania/residência e cita WHV **para recusar**. A busca
por frase não dá contexto, e "working holiday" no texto completo dava +15 e ainda **cancelava** a penalidade de
cidadania (regra herdada do protótipo). Todas as 4 Driller's Offsider rotuladas como ruins tinham esse +15.
Medição no Seek (31 dias, Austrália): "working holiday" 499 anúncios; com citizen(s) 40, resident(s) 46,
residency 19, citizenship 16 (o Seek trata citizen/citizens e resident/residents igual, mas não citizenship
nem residency). Regra nova: grupo `whv_residency` com 4 termos compostos ("working holiday + citizen" etc.;
21 termos no total). Com ele, a menção a WHV no texto completo não dá bônus nem cancela penalidades, e o
anúncio leva −55, só uma vez se a penalidade de cidadania também se aplicar. O texto da própria vaga
(título/resumo) ainda vale como boas-vindas: esse foi lido, não buscado. Custo aceito: "citizens, PR or
WHV welcome" no corpo do anúncio também cai; os rótulos dirão se isso pesa. Paridade com o protótipo mantida
(o grupo não existe lá).

### 2026-10-21 — Rotular rápido e sem viés
Ponto de partida medido: **0 rótulos e 0 candidaturas** em 15.575 vagas; o fifo tinha 835 vagas com 70+,
sinal de que o score não separa o topo. A triagem já tinha Good/Bad por linha; entrou:
- **Ordem "across scores"** (`LabelSample.Spread`): distribui as faixas de score (80+, 65, 50, 35, 1, excluídas)
  em rodízio, com embaralhamento estável dentro da faixa (FNV-1a do id), para a página trazer de tudo.
- **Teclado:** `j`/`k` movem, `g`/`b` rotulam, `u` desfaz, `o` abre. A seleção segue cliques e teclas, não
  eventos de foco: o Blazor devolve o foco sozinho a uma linha reordenada, e isso movia a seleção.
  O foco vai por posição (`wwwroot/triage.js`), não por `ElementReference`, que fica velha quando a linha volta.
- **Desfazer** o último rótulo (barra acima da lista ou `u`): devolve o veredito anterior a todas as cópias.
  Status (Applied/Dismiss) não tem desfazer; corrige-se com "Show handled".
- **Progresso** no topo da triagem: julgadas boas/ruins do perfil (rótulo + Applied/Dismissed), meta
  `Calibration.LabelGoal` = 50.
Verificado no navegador contra o banco de produção (rotular → desfazer, 0 rótulos ao final). 309 testes.

### 2026-10-21 — Perfis sem "All Australia"
Decisão do autor: tirar "All Australia" dos perfis que já buscam cidades (tech, trades, construction, fifo);
o regional mantém "Western Australia". Fica na configuração local (`appsettings.Local.json`, fora do repo):
**311 → 256 buscas** por passada, e as 33 cortadas de "All Australia" deixam de rebuscar o histórico todo dia.
Seguem cortadas as de cidade grande (Perth 17, Sydney 5, Brisbane 5, Melbourne 2): se pesar, dividir por
subclassificação.
Efeito colateral aceito: 5.209 vagas só foram achadas por buscas "… @ all australia" (de 8.752 com esse
achado). Um perfil só repontua o que as suas buscas acharam, então essas ficam com o último score calculado
e não entram mais em repontuação; continuam no banco e na triagem.

### 2026-10-21 — Carga completa forçada (`collect --full`)
Achado antes: a prova da 4b (1 página) deixou 137 buscas cortadas, mas a janela avançou e as vagas além da
1ª página nunca vieram. Correção (`6faa5da`): só execução **completa** avança a janela (buscas e termos);
comando `collect [--full]` roda a passada na hora (mesmo `DailyPipeline` do agendamento).
Rodada no banco de produção, código do repositório: **204 s no total** (coleta 145 s, termos 43 s, score
5,6 s, Raio-X 8,6 s), 0 falhas. 53.187 achados, **9.412 vagas novas** (total 15.575), 1.119 marcas de termo.
Melhores do tech: 162 com 70+.
Ainda cortadas no teto de 500: **62 buscas** (All Australia 33, Perth 17, Sydney 5, Brisbane 5, Melbourne 2).
Pela regra nova, elas buscam o histórico inteiro todo dia (+~310 requisições, ~60 s) e mesmo assim perdem o
que passa de 500. Decisão: ver "Perfis sem All Australia" acima.

### 2026-10-21 — Desempenho: paralelismo com orçamento
O gargalo é rede e educação com o Seek, não CPU. `SeekPacingHandler` (dentro da resiliência, então retentativas
também passam) dá um orçamento único ao processo: **4 requisições simultâneas e 5/s**, sem rajada
(`Seek:MaxConcurrentRequests`, `Seek:RequestsPerSecond`); as pausas fixas entre páginas saíram. Em cima dele:
buscas e termos em paralelo (`Collection:MaxParallelSearches` = 6), estados de um termo ao mesmo tempo,
contagens do Raio-X todas de uma vez. O banco segue uma operação por vez (`SemaphoreSlim` no coletor e nos
termos): o `DbContext` do escopo não aceita duas. Score com PLINQ e regex compilada. Tempo de cada etapa no log.
Medido no banco local, mesma passada incremental, contra o Seek: coleta 126 s → **64 s** (agora preso no
orçamento: 311 req a 5/s ≈ 62 s), termos 6 s (janela de 2 dias), score 2,7 s, Raio-X ~61 s → **9,4 s**. Zero
falhas, zero 429. Primeira carga completa: ~30 min → ~5 min estimados. 301 testes (limitador: pico de
concorrência e taxa; coletor em paralelo).

### 2026-10-21 — Etapa 9: comando de importação
`JobRadar.Integrations.Prototype` lê o `jobs.db` (SQLite, só leitura) e tolera versões antigas (tabela ou
coluna que falta = vazio). Usa o leitor de rótulos do Seek (exceção documentada: o protótipo guardava os rótulos
do Seek). `PrototypeImportService`: vagas com a 1ª data do protótipo (já existente: detalhes atuais ficam, só a
1ª data recua), payload = a linha do protótipo, perfis por chave direta `profile: <nome>` (o perfil pontua),
termos, rótulos, status (`rejected` + candidatura → Declined; sem → Dismissed, o sentido que a calibragem do
protótipo dava), notas, retratos e histórico de vagas do Raio-X. O que já está aqui vence; repetir é seguro.
Comando `import-prototype <jobs.db> [--dry-run]`. 297 testes (SQLite fictício com esquema atual e antigo).
O teste de composição do site pegou o serviço registrado sem o leitor (que só existe no Worker): registro movido
para a composição do Worker.
Prova real num banco descartável: 5.388 vagas em 8,7 s; 2ª execução 0 novas, payloads continuam 5.388; ligação
aos perfis fifo 1.800, trades 1.677, tech 1.626, construction 1.102. Banco descartável apagado.

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
