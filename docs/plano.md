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
| 9 | Importação do histórico do protótipo (vagas, rótulos, candidaturas, `market_counts`) | ✅ 2026-10-23 |
| 10 | **Corte**: Worker rodando todo dia em produção, Python desligado | ✅ 2026-10-23 |

Pendências conhecidas, com a etapa onde entram:
- **Etapa 9:** as 9.593 vagas importadas ganham score na passada de 2026-10-24 (19:00 de Brasília). Conferir na
  triagem que aparecem nos 5 perfis.
- **v2 (newsletter):** áreas, cidades e termos do Raio-X ainda são os do protótipo (HVAC-R, Elétrica,
  Desenvolvimento; Perth + Austrália; 4 termos). Mudar = configurar `Market` (cada lista configurada
  substitui o padrão).
- **Depois da v1:** migrar a produção do PC para uma VM (opções pesquisadas em `docs/producao.md`).
- **Login no site:** decidido não ter (2026-10-21): o site só escuta em `localhost` e no IP da Tailscale, com
  firewall restrito a `100.64.0.0/10`. Se um dia for público, entra autenticação antes. Porta 5380 porque a
  5080 é de outro serviço do autor nesta máquina (Servidor de Câmeras).
- **Termos de cidadania e direito de trabalho:** corrigido em 2026-10-22 com a palavra de contexto; tech e
  regional ganharam as suas em 2026-10-23 (ver Diário). A busca do termo sozinho segue incompleta todo dia, sem
  efeito: todo perfil agora tem contexto. A passada de 2026-10-24 (Perth) **ainda não tinha** os novos: 412
  execuções, o mesmo de antes (seriam ~452), e as palavras entraram às 19:14 de Brasília, depois do início
  dela. Elas moram no `appsettings.Local.json`, que só chega à produção pelo `deploy.ps1`. Depois de implantar,
  conferir na `/status` ("Last pass") que nenhum termo "+ contexto" aparece como incompleto.
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
- **Etapa 10:** buscas amplas batem no teto de 500. Desde que "All Australia" saiu dos perfis
  (2026-10-21), nenhuma busca de vaga ficou incompleta; se voltar a acontecer, dividir na configuração (por
  subclassificação ou região). O `collection_runs.complete` mostra quais.

## Depois da v1 (registrado 2026-10-21)

Ideias decididas como direção, sem data. Entram como etapas quando a v1 fechar.

### v1.x — Descrição completa das vagas que importam ✅ 2026-10-22

As páginas de vaga do Seek seguem atrás do Cloudflare e **contornar continua fora**. O texto completo
entra pelo navegador do próprio autor, que abre a vaga como qualquer pessoa:

1. **"Send to JobRadar"** (página `/capture` do site, arrastar para a barra de favoritos): lê o texto do
   anúncio aberto (ou a seleção), copia para a área de transferência, abre `/capture?source=seek&id=…` e
   manda o texto por `postMessage` quando a página avisa que está ouvindo. Sem `POST` cross-origin (mixed
   content e Private Network Access bloqueariam); se o navegador separar as janelas, cola-se com Ctrl+V.
2. **Campo "colar"** na página da vaga: a mesma coisa, manual.
3. O texto fica em `listing_descriptions` (separado do `Summary`) e a vaga é repontuada na hora
   (`DescriptionService` → `ListingScoringService.ScoreOneAsync`); a passada diária também o lê.
   Lido **só** pelas regras de requisito e de direito de trabalho (ver Diário 2026-10-22).

Cuidados: o texto fica só no banco do autor (uso pessoal); nunca entra no Raio-X, no repositório nem em
fixture. A busca por frase (etapa 5) continua para as vagas sem descrição.

### v1.x — Perfil a partir do currículo (decidido 2026-10-23)

Hoje o perfil mistura duas coisas: **fatos** sobre o candidato (visto, formação, carteiras, tickets, anos de
experiência), que vêm do currículo e mudam pouco, e **hipóteses** sobre o mercado (palavras-chave, palavras de
contexto, sinais achados rotulando), que só os números confirmam. As duas camadas se separam:

1. **Fatos** em `candidate/candidate.json` (fora do git), no formato de `candidate/candidate.schema.json`, preenchido
   a partir do currículo pela skill `perfil-do-curriculo`, que pergunta o que o currículo não diz. ✅ 2026-10-23
2. **`profile draft <candidate.json>`**: gera, de forma determinística e testada, o que sai dos fatos (carteira que
   falta → `Licence`, formação não reconhecida → `Qualification`, poucos anos → `Experience`, nível →
   `AllowSenior`, locais) e imprime o diff contra o `appsettings.Local.json`, sem gravar. O que foi ajustado
   contra o mercado continua no perfil. **Critério de pronto:** com o `candidate.json` real, o rascunho reproduz
   as regras dos perfis atuais; se não reproduzir, falta um fato no esquema.
3. **`profile preview`**: antes de ligar uma palavra-chave, mostra o volume (`totalCount`, uma página), a
   sobreposição com as buscas atuais, o rendimento (vagas do banco ≥ 50 / ≥ 75 pontuadas em memória) e
   **quantas vagas rotuladas sairiam do perfil** se uma palavra antiga for removida.
4. **Medir depois de ligar**: por palavra-chave (novas/dia, quantas só ela acha, fração visível, fração boa, teto
   de 500), por perfil (o `calibrate` mais o funil achadas → visíveis → boas → candidatadas → resposta) e por
   lacuna (requisitos mais pedidos nas vagas altas que o candidato não tem: qual ticket vale tirar).

O progresso de calibragem não se perde: os rótulos são por vaga (`listing_labels`, sem perfil), as regras
não mudam, os nomes de perfil se mantêm e todo perfil gerado passa por `calibrate` / `calibrate --rescore`
antes de entrar. Upload do currículo pelo site, com extração por API, só na v3 (outros usuários).
O passo 2 mudou de forma com o catálogo de requisitos (abaixo): o que ele derivaria dos fatos (carteira,
formação, tickets) vira o **estado** de cada item do catálogo, e o `candidate.json` é importado para o banco.

### v1.x — Requisitos e pesos configuráveis (decidido 2026-10-23)

Os controles do score estão em quatro lugares: constantes no `FitScorer` (−40 carteira de caminhão, −10 ticket,
−20 requisito, −55 cidadania, ±16/8 sinais…), a `ScoringPolicy` (só padrões do código), frases repetidas em cada
perfil (`Requirements`, `QualifiedTitles`, `DisqualifySignals`) e regex em `ListingPatterns`. E nada sabe o que o
candidato **tem**: que tiraria o HRWL e que o ARCtick está fora de alcance mora na cabeça dele. O Seek não filtra
por isso. Três camadas:

1. **Catálogo de requisitos** (repo, conhecimento de mercado, igual para todos): item com `id` (`white-card`,
   `hrwl`, `forklift`, `truck-hr`, `arctick`, `trade-qualified`, `degree`, `experience-years`, `citizenship`…),
   tipo, frases como os anúncios escrevem, custo de obter (curso curto, anos, certificado), e se é buscado no
   Seek (custa uma busca por palavra de contexto em cada passada) ou só lido nas descrições. Itens com leitura
   especial (carteira com "desirable", cidadania com negação) apontam para o detector que já existe.
2. **Estado do candidato em cada item** (banco, editado no site): `tenho` (bônus quando o anúncio pede),
   `tiraria` (leve, o −10 de hoje), `não tenho` (o −20), `fora de alcance` (forte ou teto, o −40). Mudou o
   estado, repontua na hora. No banco porque o site já grava rótulos lá e porque perfis não viajam entre o
   trabalhopc e a produção; o `candidate.json` vira formato de importação (`candidate import`).
3. **Pesos na política**, com os valores de hoje como padrão, no `rules_hash`.

Os perfis ficam com a hipótese de mercado (palavras-chave, contextos, sinais, `priority` da linha) e dizem só
**quais itens do catálogo importam** para eles.

| # | Passo | Muda score? | Estado |
|---|---|---|---|
| 1a | Constantes do `FitScorer` para a `ScoringPolicy` | não: paridade e `calibrate` iguais | ✅ 2026-10-23 |
| 1b | Frases de requisito dos perfis viram itens do catálogo | não | ✅ 2026-10-23 (perfis reais migrados à parte, aguardando o autor) |
| 2 | Estado no banco + `import-candidate`; estado inicial reproduz o tratamento de hoje | não | ✅ 2026-10-24 |
| 3 | Bônus do "tenho" (`RulesVersion` 10, medido com `calibrate --rescore`) | sim | ✅ 2026-10-24 |
| 4 | Página "Requisitos": estado editável, repontuação na hora, impacto por item (vagas que pedem; quantas passariam de 50/75 se o candidato tivesse, simulado em memória) | não | ✅ 2026-10-24 |
| 5 | Anos de experiência: ler "minimum 3 years" e comparar com os anos do candidato na área da linha | sim | ✅ 2026-10-24 (faltam os anos reais do autor) |
| 6 | Se valer: palavras-chave e sinais no banco, com o `profile preview` na mesma página | — | |

A métrica de lacuna do passo 4 responde "qual ticket tirar primeiro"; agregada por categoria ("x% das vagas de
construction pedem white card") serve ao Raio-X. Limites: orçamento de buscas (a página mostra quantas o catálogo
custa) e a busca por frase sem contexto ("white card an advantage" casa como "essential"): o peso fica moderado
fora das vagas com descrição.

### v1.x — Informações da descrição (registrado 2026-10-23) ✅ 2026-10-24

Pedido do autor: as descrições completas trazem o que a busca não dá. Salário em vaga sem o campo de salário do
Seek (`SalaryText`), roster (2:1, 8:6, 14:7), alojamento e voos, início ("immediate start"), turno, duração do
contrato. Não são requisitos, e sim **atributos** da vaga. Mesma ideia do catálogo: cada atributo tem os padrões
de como os anúncios escrevem, é extraído na gravação da descrição (`DescriptionService`) e na passada, e aparece
na triagem como coluna e filtro. Se valer, entra no score por sinal do perfil ("accommodation provided" já é
sinal do fifo no exemplo). Cuidado: a descrição só é buscada para vagas rotuladas e de score alto, uma amostra
enviesada. Atributo tirado dela **não** entra no Raio-X; o salário do Raio-X continua vindo do campo do Seek.
Entra depois do passo 4 dos requisitos.

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

### 2026-10-27 — Queda de energia: vigia dos serviços e ReadyToRun
O PC desligou sem aviso em 2026-10-25 11:50 e voltou em 26/10 09:54. No boot, `JobRadarWorker` e
`JobRadarWeb` passaram dos 30 s de partida (eventos 7009/7000) e ficaram parados: **sem passada em 25 e 26**.
A recuperação configurada (`sc failure`) só reinicia serviço que cai depois de rodar. O backup seguiu (tarefa
do Agendador). Iniciados à mão em 27/10 17:14 (BRT); o worker rodou a passada perdida na partida.
Correção: `deploy/watchdog.ps1` + tarefa `JobRadar Watchdog` (login + 2 min, a cada 15 min; respeita a trava
`deploying.lock` que o `deploy.ps1` mantém enquanto para os serviços) e publicação ReadyToRun (win-x64):
partida de 2,6 s para 1,3 s num comando curto. Testado: serviço parado → iniciado e registrado; com trava →
não mexe. Fica para depois: Docker como serviço, para tudo voltar sem login.

### 2026-10-24 — Carteira com letra de condição (`RulesVersion` 13)
Achado do piloto: "HR-B Drivers License" (classe HR com condição) passava pelo padrão de carteira, e o anúncio contava
como sem carteira. O padrão aceita agora a letra colada por hífen, entre parênteses ou seguida de "class" ("hr-b",
"hc (b)", "mr b class"); o texto chega em minúsculas, e "HR a driver will call" não pode virar carteira. 29 vagas do
banco citam assim. Paridade 237/237. Medido nos rótulos de produção: construction 100% → 97% (boas ≥ 75: 28 → 25) e
fifo 95% → 92% (13 → 10), por três driller offsider rotuladas boas que pedem HR-B. O autor confirmou que carteira HR
o barra de qualquer forma; as três foram remarcadas ruins (nota: "pede carteira HR-B, que barra o candidato").

### 2026-10-24 — Piloto: Claude lendo os requisitos (não adotado)
Pergunta: um modelo lendo a descrição com contexto ("cert III an advantage" x "essential") acerta mais que a busca por
frase? Branch `pilot/llm-requirements` (fora da main): `IRequirementReader` (`JobRadar.Integrations.Claude`, SDK
oficial `Anthropic` 13.3, saída estruturada, instruções em cache, effort baixo) devolve por item do catálogo
"exigido / desejável / não pedido" e os anos pedidos; com a leitura, o `FitScorer` pesa requisitos, anos e carteira
por ela. `read-requirements <modelo> <arquivo>` e `calibrate --rescore --readings <arquivo>`. Credencial: login do
`ant` (OAuth) no trabalhopc, sem chave em arquivo. Rodado nas 52 vagas julgadas com descrição (de 92), custo
estimado ~US$ 2:

| leitura | construction | fifo | trades |
|---|---|---|---|
| frase (hoje) | 100%, 28/28 boas ≥ 75, 3 ruins ≥ 50 | 95%, 13/16, 4 | 91%, 6/8, 1 |
| Haiku 5.5 | 97%, 22/28, 4 | 93%, 8/16, 3 | 92%, 6/8, 1 |
| Sonnet 5.5 | 96%, 20/28, 4 | 93%, 9/16, 3 | 92%, 4/8, 1 |
| Opus 5.5 | 98%, 22/28, 4 | 94%, 10/16, 2 | 92%, 5/8, 1 |

Nenhum melhora: seguram um pouco mais as ruins, mas derrubam boas. Lido à mão, **o modelo lê certo**: a "Drillers
Offsider" boa pede "HR-B Drivers License" (o padrão de carteira não reconhece "HR-B": bug das regras atuais, e o
rótulo contradiz a decisão de 2026-10-23 de que vaga que pede HR é ruim); as "Entry Level" boas listam "Experience in
manufacturing, mining, or site-based work" em "What you'll need" (requisito de verdade, mas genérico, que o autor não
vê como barreira). Ou seja: a busca por frase acerta mais **porque erra a favor** das boas, e o −20 fixo de
experiência pune demais quando a leitura é precisa. Adotar exigiria recalibrar o peso de experiência genérica, com
risco de ajustar 52 rótulos à mão. Fica fora da main; as leituras ficam no trabalhopc (`%TEMP%\jr-pilot-readings`)
para reanalisar sem novo custo. Responde o "IA no score" da `visao.md`: hoje não melhora a calibragem.

### 2026-10-24 — Informações da descrição como etiquetas
O autor achou o resumo pobre. Medido: das 1.781 vagas visíveis (≥ 50), 1.755 já tinham a descrição completa no banco,
e a triagem mostrava só título, empresa e o resumo do Seek (~195 caracteres). `ListingFacts` (domínio, puro) lê da
descrição, ou do resumo quando não há, o que o candidato procura primeiro: pagamento com o período (só quando o campo
do Seek não traz número: "Competitive Hourly Rate" não conta), FIFO/DIDO/BIBO, roster, alojamento, refeições, voos,
início imediato, turno e duração; o que o anúncio diz que **não** oferece não vira etiqueta. A triagem lê só as vagas
da página; a página da vaga também. Mostrar, não pontuar; nada disso entra no Raio-X (amostra enviesada).
Rodado nas 1.755 descrições reais (no trabalhopc, só leitura): 815 ganham etiqueta; pagamento 305, FIFO 173, início
imediato 265, roster 91 (2:1 ×25, 8:6 ×10…). Amostras conferidas à mão. Corrigido no caminho: "Monday to Friday" dava
"Day shift" (×276) e diz os dias, não o turno, agora "Mon–Fri"; voos e alojamento perdiam "FIFO flights", "flights
and accommodation provided", "shared accommodation". Visto na triagem real (site novo apontado para a produção, só
olhando).

### 2026-10-24 — Página "About you" (`RulesVersion` 12)
Pedido do autor: um jeito simples e rápido de dizer a própria situação. A `/requirements` é para medir (ids,
selects, números); a `/me` pergunta o mesmo em palavras: anos de experiência por linha com botões rápidos (0, 1, 2,
3, 5, 8, 10+ ou outro número) e cada licença, ticket e formação com o que é, como se tira e quatro botões ("I have
it", "I'd get it", "I don't have it", "Out of reach"). Grava e repontua na hora; textos em `Wording.Requirement`.
Regra v12: com os anos do candidato num perfil, o número de anos que o próprio anúncio pede conta mesmo que o perfil
não busque frase de experiência; assim o tech compara os anos de software sem mexer na configuração. Mudar os anos
repontua o perfil inteiro. Provado no trabalhopc numa cópia do banco, pelo navegador (também em tela de celular):
paridade 191/191; sem anos o v12 sai idêntico ao instalado; fifo = 0 repontuou 6.474 vagas. A produção já tinha tech
= 4 (gravado pelo autor na `/requirements`); o tech tem um só rótulo, então a calibragem ainda não diz nada dele.

### 2026-10-24 — Anos de experiência pedidos x os do candidato (`RulesVersion` 11, passo 5)
O requisito de experiência custava 20 pedisse o anúncio 1 ano ou 10. Agora, quando o texto do anúncio diz quantos anos
("minimum 3 years experience", "3+ years of civil experience", "2-3 yrs": `ListingPatterns.YearsAsked`, o menor
número entre 1 e 20) **e** os anos do candidato naquela linha são conhecidos, o número decide: alcança, "tenho"
(bônus); um ano a menos, "tiraria" (−10); mais, "não tenho" (−20). Sem um dos dois, nada muda. Anos por perfil em
`candidate_experience`: comando `experience <perfil> <anos>|default`, bloco na página `/requirements` ou
`import-candidate` (soma os anos das áreas de `experienceAreas` de cada alvo). `--assume years:<perfil>=<n>` mede
antes de gravar. De quebra: estado de carteira de caminhão entra no `rules_hash` de todo perfil (a regra lê o texto
do anúncio em qualquer perfil).
Medido no trabalhopc numa cópia do banco (apagada depois): paridade 190/190; sem anos gravados o v11 sai idêntico
ao instalado. Simulando os mesmos anos em construction, fifo e trades:

| anos | construction | fifo | trades |
|---|---|---|---|
| nenhum (hoje) | 100%, 3 ruins ≥ 50 | 95%, 4; boas ≥ 75: 13/16 | 91%, 1; 6/8 |
| 0 | igual | igual | igual |
| 1 | igual | 95%, 5 | igual |
| 2 | igual | 95%, 5; boas ≥ 75: 14/16 | 91%, 1; 7/8 |
| 5 | igual | igual a 2 | 90%, 1; 7/8 |

Efeito pequeno porque **o número quase nunca está no que se lê**: das vagas com requisito de experiência, só 13%
no construction (103 de 803), 6% no fifo (48 de 782) e 10% no trades (41 de 394) trazem os anos no resumo ou na
descrição; o resto só tem a marca da busca por frase, sem número. Cresce com as descrições baixadas. O perfil tech
não busca nenhum item de experiência hoje: para a regra valer lá, `experience-years` entra no `Requirements:Items`
dele (configuração do autor) e os anos de software em `experience tech <n>`.

### 2026-10-24 — Página "Requirements" (passo 4)
`/requirements` lista cada item do catálogo com o estado do candidato (editável ali) e o que ter o item mudaria em
cada perfil: quantas vagas mexem e quantas delas passam de 50 e de 75 sem e com ele, e quantas foram rotuladas boas
ou ruins. Pontuado em memória (`ListingScoringService.ImpactAsync`): um filtro barato por frase poupa o scorer das
vagas que não podem pedir o item, e a página toda mede em ~10 s com os dados de produção. Mudar um estado, ali ou
com `requirement`, repontua na hora as vagas que podem pedir o item (`RescoreRequirementAsync`); o resto do perfil
fica para a passada, onde nada mais muda. Provado no trabalhopc com o site novo apontado para uma cópia do banco de
produção (túnel ssh para o navegador): HRWL marcado "tenho" repontuou 1.406 scores, 1.002 com o bônus, e a vaga
mostra "Asks for something you have". Cópia apagada; a produção não foi tocada.
O que a página mostrou (vagas que passam de 50, sem → com o item): **HRWL**, o único ao alcance com curso curto,
leva construction de 17 a 269, trades de 4 a 115 e fifo de 33 a 83 — é o ticket que mais abre vaga. Carteira HR
moveria 539 vagas no construction (29 → 314), mas está anos à frente. Trade e experiência movem centenas em cada
perfil, e não se compram.

### 2026-10-24 — Bônus do "tenho" (`RulesVersion` 10, passo 3)
Anúncio que pede o que o candidato tem ganha `HaveRequirementBonus` (10, uma vez por tipo, na fase de pontos,
antes da compressão). As classes de carteira de caminhão incluem as menores (MR < HR < HC < MC): a classe que o
candidato tem tira o −40 da regra que lê o texto do anúncio e das frases buscadas. `calibrate --rescore --assume
item=estado,...` mede um estado sem gravá-lo. Medido no trabalhopc com os rótulos de produção (só leitura):
paridade 175/175; com os estados gravados (nenhum "tenho") o v10 sai idêntico ao instalado. Simulado:

| `--assume` | construction (acerto, ruins ≥ 50) | fifo | trades |
|---|---|---|---|
| nenhum | 100%, 3 | 95%, 4 | 91%, 1 |
| `hrwl`, `voc`, `truck-mr` ou `experience-operating` = have | igual | igual | igual |
| `truck-hr=have` | 92%, 7 | 90%, 9 | 89%, 2 |
| `truck-hc=have` | 88%, 9 | 88%, 11 | 89%, 2 |
| `trade-qualification=have` | igual | 93%, 4 | 86%, 1 |

Leitura: as vagas rotuladas ruins **por causa** da carteira HR/HC ou do trade sobem quando o candidato passa a ter;
é o bônus e a hierarquia funcionando, e mostra que **um rótulo vale para o estado do candidato quando foi dado**.
Quando ele tirar uma carteira, essas ruins deixam de ser ruins, e a calibragem vai acusar "regressão" que não é:
reavaliar os rótulos daquele requisito. HRWL e VOC não mexem nos números: quase nenhuma vaga rotulada pede. O
valor 10 do bônus é um chute (o mesmo do ticket que falta); só se calibra quando houver itens "tenho" e rótulos de
vagas que os pedem. Antes disso, o white card e o forklift precisam entrar no catálogo (hoje o `import-candidate`
os aponta como ausentes).

### 2026-10-24 — Estado do candidato em cada requisito (passo 2)
Tabela `candidate_requirements` (uma linha por item que o candidato definiu) e o estado decidindo os pontos do
requisito achado: `have` 0, `obtainable` 10, `missing` 20, `out_of_reach` 40 (`ScoringPolicy`; saíram
`RequirementPenalty` e `TicketPenalty`). Item sem linha, e frase própria do perfil fora do catálogo, ficam com o
padrão do tipo, que é o tratamento de antes: carteira de caminhão fora de alcance, outra licença ou ticket
"tiraria", experiência e formação "não tenho". Com a tabela vazia nada muda, nem o `rules_hash`: ele só ganha os
estados que diferem do padrão nos itens que o perfil busca. Vários do mesmo tipo no anúncio: vale o pior; um que o
candidato tem não esconde outro. Comandos: `requirements`, `requirement <item> <estado>|default` e
`import-candidate <candidate.json> [--dry-run]` (tickets, carteiras pesadas e formação reconhecida; o que o
catálogo não tem, como white card e forklift, aparece no relatório). O efeito vale na passada seguinte; o
`calibrate --rescore` lê os estados do banco e mede na hora.
Provado no trabalhopc numa **cópia** do banco de produção (pg_dump para `jobradar_verify`, migration só nela,
apagada depois; a produção não foi tocada): sem estados, o `calibrate --rescore` do build novo sai idêntico ao do
instalado; gravar e voltar ao padrão também. Medido de passagem: ARCtick e licença elétrica como fora de alcance
(o que o autor já disse que são) levam fifo de 5 a 4 ruins ≥ 50 e trades de 90% a 91% (ruins ≥ 50: 2 → 1), sem
derrubar boa. Aplicado na produção depois do deploy (2026-10-24, `requirement … out-of-reach`), com o
mesmo efeito no `calibrate --rescore`; os scores gravados mudam na passada seguinte.

### 2026-10-23 — Catálogo de requisitos (passo 1b)
`RequirementCatalog` (13 itens, na `ScoringPolicy` e portanto no `rules_hash`): três de experiência (anos,
"proven/demonstrated", "operating"), quatro de formação (trade, diploma, refrigerante/ARCtick, elétrica), seis de
carteira e ticket (HR, MR, HC, MC, HRWL, VOC), com as frases exatas dos perfis reais. O perfil lista os ids em
`Requirements:Items`; as listas de frase continuam valendo e somam aos itens; id desconhecido para o início (um erro
de digitação tiraria uma penalidade calado). Prova no trabalhopc com os perfis e rótulos de produção:
`calibrate --rescore` do build novo com a configuração de hoje sai idêntico ao do instalado; com a configuração
migrada (um script troca cada conjunto de frases pelos itens e confere que a expansão dá o mesmo conjunto), os
scores são os mesmos e muda só a frase mostrada numa vaga que pede ARCtick e electrical licence (as duas valem os
mesmos −20). trades e fifo usam os 13 itens; construction, 11 (sem as licenças de profissão). A configuração
migrada ainda não substituiu a real: espera o autor.

### 2026-10-23 — Pesos do score na política (requisitos configuráveis, passo 1a)
Todo número que o `FitScorer` somava ou subtraía virou propriedade da `ScoringPolicy`, com o valor de antes como
padrão: sinais (16/8, 30/15), sênior, remoto/híbrido, FIFO, entrada, WHV, tipo de trabalho, local preferido, a
compressão (acima de 80, dividido por 3), cidadania, WHV com residência, direitos ambíguos e de 12 meses,
comissão, requisito (20), ticket (10), carteira de caminhão (40, ou 10 se desejável) e sponsor recusado (15). Sobrou
só o −50 de exibição das exclusões, que não muda score. Sem `RulesVersion` nova: os testes por regra continuam
com os mesmos números, e um teste novo troca os pesos e confere que o score segue a política. O `rules_hash`
muda (a política serializada ganhou campos), então a próxima passada regrava todos os scores, com os mesmos
valores. Provado no trabalhopc (clone à parte, por ssh): paridade passa (161 de 161, nenhum pulado), e o
`calibrate --rescore` do build instalado e o desta branch saem idênticos byte a byte, com os perfis e rótulos de
produção.
A política ainda não é lida da configuração: só faz sentido junto do estado do candidato (passo 2).

### 2026-10-23 — Fatos do candidato separados do perfil (v1.x, passo 1)
Primeiro passo do perfil a partir do currículo: `candidate/` com o esquema (`candidate.schema.json`, JSON Schema
2020-12, cada campo dizendo o que faz no score), um candidato fictício e um README; o git ignora todo o resto da
pasta (o `candidate.json` real e o próprio currículo). A skill `perfil-do-curriculo` lê o currículo, preenche
os fatos e pergunta o que falta (carteira, tickets que tiraria, reconhecimento da formação, linhas de trabalho);
o que ficar sem resposta vai para `openQuestions`. Ainda não gera perfil: é o passo 2 (`profile draft`). O exemplo
foi validado contra o esquema com ajv em modo estrito; um campo inventado e um enum errado foram recusados.

### 2026-10-23 — Regras v9, lidas nas descrições das ruins (`RulesVersion` 9)
O autor pediu para ler os anúncios das vagas ruins que ainda pontuavam alto. Achados: "HC **Unrestricted** licence"
escapava do padrão; "HR driver's license is essential - HC licence desirable" virava HR desejável (−10) por causa do
"desirable" do HC; "**Long-term** Australian working rights" (Cleaning Utility) não excluía; "**Assistant** Village
Manager" contava como título júnior e escapava da exclusão de liderança. Agora cada carteira lê só até a próxima e
"essential/mandatory/required/must" vence "desirable"; "long-term work rights" exclui como "unlimited"; "assistant …
manager" é gerência. Regional: o autor quer só onde os 88 dias contam (cultivo em área regional; nenhum código postal
da Grande Perth nas listas da 462), então perfis ganharam `ExcludedLocations` e o regional exclui "Perth WA" (era
−15). Medido com `--rescore`: construction ruins ≥ 50 4 → 3; fifo 94% → 95% de acerto, ruins ≥ 50 9 → 5. As três
boas do regional eram jardinagem e paisagismo em Perth: boas vagas, mas não para esse perfil (o rótulo é da vaga,
não do perfil). Paridade: mais 8 casos, todos "Assistant … Manager", contados à parte.

### 2026-10-23 — Perfis: sinais de função no fifo, licenças de profissão
Configuração (fora do git), medida com `calibrate --rescore` numa cópia antes de implantar, com os rótulos do dia
(construction 28 boas/22 ruins, fifo 16/47, trades 8/12):
- fifo não tinha sinal positivo de função; com "offsider", "labourer", "drainer" e "concreter", boas ≥ 75 foram de
  9 para 13 de 16, acerto de pares 93% → 94%, ruins visíveis iguais.
- "relevant experience" (posta mais cedo no mesmo dia) saiu: derrubava duas boas ("Delivery Driver/Offsider",
  "Shutdown Roles"); sem ela o construction tem 28 de 28 boas ≥ 75.
- "refrigerant handling licence", "electrical licence" e "ARCtick" viraram qualificação (−20) no trades e no fifo: o
  autor não tem certificado de profissão (é técnico em mecatrônica e em sistemas), e essas licenças pedem o
  certificado mais tempo supervisionado. Sem efeito nos rótulos de hoje; cobre anúncio que só cita a licença.
Em aberto, perguntado ao autor: o regional dá −15 a "Perth WA" (88 dias), mas duas jardinagens em Perth são boas.

### 2026-10-23 — Carteira de caminhão e tickets (`RulesVersion` 8)
O autor: anúncio que exige carteira HR é ruim, porque ela pede anos de carteira de carro (a dele vira definitiva no
fim de dezembro); licença que se tira com curso (HRWL e afins) ele tiraria. As regras tratavam as duas com −20, e o
padrão não lia "HR **truck** driver's licence" (a Driller Offsider de 89 pontos não tinha penalidade nenhuma).
Agora: carteira de caminhão (HR/HC/MR/MC, "heavy rigid/combination") −40, ou −10 se o anúncio diz "desirable",
"highly regarded" etc. logo depois; negação só na mesma oração (um "no experience needed" na linha de cima não
dispensa a carteira); ticket −10. Com o anúncio completo, requisitos e carteira são lidos **nele**, não na busca
por frase: a busca marcava "HRWL" em anúncios que não pedem (a boa "Gas Field Installer" levava −20 assim).
Medido com o `calibrate --rescore` novo (as regras da branch contra os rótulos de produção, nada gravado):
construction 95% → 99% de acerto de pares (ruins ≥ 50: 8 → 4; boas ≥ 75: 13 → 14 de 14), fifo 89% → 91% (10 → 9;
6 → 7 de 10), trades 97% → 99% (5 de 6 boas ≥ 75). Duas ruins subiram ao perder o falso "HRWL" e mostraram o motivo
real, experiência escrita de outro jeito: com "min 2 years", "minimum of 2 years", "relevant experience" e
"experience in a similar role" nas frases de experiência dos perfis (configuração), construction vai a 100% e fifo a
92% (ruins ≥ 50: 8). "previous experience" ficou de fora: derrubou uma boa ("an advantage"). Paridade: 55 de 5.388
casos do protótipo mudam, todos de carteira de caminhão, contados à parte pelo teste.

### 2026-10-23 — Implantação automática e volta de versão
Até aqui implantar era `git pull` + `deploy.ps1` no trabalhopc, à mão. Agora a tarefa `JobRadar Auto-deploy` (a
cada 15 min, sessão do autor, `conhost --headless` para não piscar janela) roda `deploy/auto-deploy.ps1`:
`origin/main` diferente do `deployed.txt` e CI do commit verde na API pública do GitHub → `merge --ff-only` +
`deploy.ps1`. A produção puxa: runner self-hosted foi descartado porque o repositório é público (PR de fork
rodaria código nesta máquina). O `deploy.ps1` passou a guardar a versão anterior em `C:\JobRadar\previous`, ganhou
`-Rollback` (troca as duas e marca o commit desfeito para a automática não reinstalar) e recusa implantar com
execução gravada nos últimos 30 min, além da janela fixa: hoje uma passada recuperada rodou às 18:04. O
`global.json` pede 10.0.400 com `latestFeature`: qualquer SDK 10.0.4xx serve (esta máquina tem 401, a de produção
402).
Dois tropeços na instalação, ambos corrigidos: pelo SSH `USERDOMAIN` é `WORKGROUP` (a tarefa agora usa a identidade
do processo); e no `auto-deploy.ps1` a variável do commit (`$target`) sobrescrevia o parâmetro `-Target`, porque o
PowerShell não diferencia maiúsculas: os dois primeiros ciclos "implantaram" numa pasta com o nome do commit dentro
do checkout (apagadas; a produção seguiu na mesma versão). Agora o ciclo só vale se o `deployed.txt` passar a ter o
commit, e o `deploy.ps1` recusa `-Target` relativo. Testado pela tarefa em produção: `ccc74a2` implantado em 38 s,
ciclo seguinte sem nada a fazer; `-Rollback` para `05eec04` (a automática esperou), `-Rollback` de novo de volta a
`ccc74a2`.

### 2026-10-23 — Etapa 9 fechada: histórico do protótipo importado (v1 completa)
Pelo `ssh segmix` (o trabalhopc, a partir da outra máquina): cópia `jobs-2026-10-23.db` com o hash conferido,
backup `jobradar_2026-10-23_201115.dump` (10,7 MB), dry-run e importação com `bd9c21b`. Resultado: **9.593 vagas
novas** e 5.014 já conhecidas (só juntaram datas; o banco foi de 16.167 para 25.760), 653 termos novos dos 1.417,
**5 rótulos** (nenhum conflito; 73 → 78), nenhuma candidatura (o protótipo só tinha `new`), Raio-X com **13/out e
21/out** novos (os outros 3 dias já estavam aqui) e 642 vagas do Raio-X juntadas. As vagas ficaram ligadas aos 5
perfis atuais (`profile: <nome>`); o score delas sai na próxima passada. Com as etapas 9 e 10, a v1 está fechada.

### 2026-10-23 — Importador: dias do Raio-X pela data de Perth
No cruzamento antes de importar, os 5 dias do Raio-X do `jobs.db` final estavam um dia atrás dos daqui: o
protótipo nomeava o dia pela data do Brasil e rodava à noite, já a manhã seguinte em Perth. O "21/out" dele foi
tirado meia hora antes do "22/out" daqui; importado assim, o mesmo momento viraria dois dias no relatório mensal.
`PrototypeMapping.InScheduleDays` renomeia cada retrato pela data, no fuso da agenda, de `market_runs.ran_at`, e as
datas de primeira/última vista das vagas do Raio-X (que são dias do protótipo) seguem o seu retrato. Sem
`ran_at`, o dia fica como está. O dry-run passou a listar os dias.

### 2026-10-23 — `/status` mostra a última passada e o que está implantado
Da outra máquina só a página de status é alcançável (Tailscale, firewall só na 5380), e ela não dizia quais
buscas falharam nem qual versão estava rodando: a conferência dos termos "+ contexto" virou dedução pela
contagem. Agora a página mostra a linha do `deployed.txt` (commit e hora; o commit do build quando não há
registro) e a última passada com cada busca ou termo que falhou ou parou no teto, com o erro. Sem id de passada
em `collection_runs`: a passada começa na primeira execução que sai mais de 15 min depois do **fim** de todas as anteriores
(`CollectionPass`). A primeira versão media 1 h entre inícios e juntou as duas passadas de hoje (18:04, recuperada
depois da implantação, e 19:01): as descrições seguem a última execução por minutos. Medido em `collection_runs`
desde 2026-10-22: nenhuma pausa interna passa de 2 min; entre passadas, de 54 min a 23 h. Porta nova
`ICollectionRunRepository.ListStartedSinceAsync`. O `deployed.txt` é lido a cada visita: o script o grava
depois de subir os serviços. Implantado pelo `ssh segmix` (alias desta máquina para o trabalhopc, chave no
`ssh-agent`).

### 2026-10-23 — Etapa 10 fechada: protótipo desligado
Na outra máquina (CASAPC), a tarefa `JobTracker Daily` (`run.ps1 --daily`, 08:00 de Brasília) foi
**desativada**, não apagada; a última execução foi às 18:35, com resultado 0. O Worker já roda sozinho em
produção desde 2026-10-21, com backup diário às 23:00. Para a etapa 9 ficou pronta a cópia final do histórico,
`D:\Projetos\job-tracker\data\export\jobs-2026-10-23.db` (backup pela API do SQLite, `integrity_check` ok,
fora do git): 14.607 vagas, 5 rótulos, 1.417 termos, 150 `market_counts`, 5 execuções do Raio-X, 2.595
execuções de coleta. SHA-256 `818A04C53CC082660417AE36E857C61FB383CBD7E0A0F424A546BB0717281F53`.

### 2026-10-23 — Prints do README com dados fictícios
`docs/img/triage.png` e `listing.png`: banco descartável `jobradar_demo` com 12 vagas inventadas (empresas
fictícias, links para example.com) ligadas às buscas do perfil de exemplo versionado; a passada do Worker com o
Seek apontado para um endereço inválido (coleta, termos e Raio-X falham, o score roda: etapas isoladas);
site da publicação numa porta à parte; Playwright a 2×. Banco descartável apagado depois. Para refazer: o
mesmo caminho; nada de anúncio real nem do perfil real em print.

### 2026-10-23 — Palavras de contexto de tech e regional
Medido no Seek (31 dias, Austrália toda, os 4 termos de cidadania e "full working rights") e na cobertura das
descrições já gravadas de cada perfil. **tech**: software, engineer, microsoft, network, technology: 98% das
272 vagas com texto têm ao menos uma; o maior total é 595 (Austrália), abaixo de 500 por estado. "developer"
cobre o mesmo e chega a 448 em NSW, perto do teto: ficou de fora. "support" passa de 2.000 ("support worker").
**regional**: farm, harvest, horticulture, picking, packing: 51% das 274, mas o que fica de fora é jardinagem e
estoque, não o foco do perfil; todos ≤ 258. "fruit", "agriculture", "seasonal" somavam só 7 pontos.
Na configuração local (`Requirements` só com `Contexts`, sem frases de requisito: nenhuma penalidade nova).
+40 buscas de termo por passada. Uma marca "termo + contexto" vale como o termo para qualquer perfil, então
uma palavra ampla não cria significado errado; só custa buscas.

### 2026-10-23 — Implantar sem admin
O `deploy.ps1` pedia admin inteiro, mas só criar e configurar serviço, registro e firewall precisam. Separado em
`install.ps1` (admin, uma vez: isso tudo + escrita em `C:\JobRadar` + entrada na ACL de cada serviço para o
usuário iniciar e parar) e `deploy.ps1` (sem admin, a cada versão: publica, para, copia, migra com a conexão lida
do registro do serviço, inicia, espera o `/status`, grava `deployed.txt`). O `deploy.ps1` recusa a janela da
passada diária (05:55–06:45 Perth) sem `-Force`. Assim o Claude implanta sozinho.

### 2026-10-23 — Descrições em produção; rotuladas primeiro
Instalado às 18:03 (Brasília). A passada seguinte gravou **2.989 descrições** em ~10 min (21:05–21:15 UTC), sem
recusa do Seek. Calibragem (73 julgadas; a de 2026-10-22, com menos rótulos, entre parênteses): construction **95%** (79%),
fifo **88%** (81%), trades 97% (97%). Mas 38 das 73 vagas julgadas ficaram sem texto: 37 tinham melhor score < 40 (só
1 passava dos 31 dias). Justamente as boas que o score subestima, e as ruins que ele acerta, ficavam medidas
sem a descrição. Agora `ListToFetchAsync` traz primeiro as vagas com rótulo ou status (`listing_labels`,
`applications`), qualquer score ou idade; depois o critério de antes. Anúncio que saiu do ar volta a ser pedido
a cada passada (`Gone`); são dezenas, aceito.

### 2026-10-23 — Descrição completa automática (GraphQL do Seek)
O autor pediu para não depender de captura manual. Pesquisa: Adzuna devolve só trecho; Workforce
Australia sem API pública; Google Jobs (SerpApi/DataForSEO) pago e sem garantia de texto completo; a API
oficial do Seek é só para publicar. O site do Seek carrega o texto pelo GraphQL `jobDetails` (`/graphql`);
uma requisição de teste, com o User-Agent identificado, voltou 200 sem desafio e com o anúncio inteiro.
Decisão do autor: usar, aceitando o risco nos termos do Seek (as páginas de vaga continuam fora).
Entrou a etapa **Full descriptions** na passada (depois dos termos, antes do score): `DescriptionFetchService`
escolhe vagas sem texto, com melhor score ≥ 40, publicadas há até 31 dias, até 3.000 por passada (opções
`Descriptions`); `SeekJobDetailsClient` no mesmo orçamento da busca (`SeekPacing`: 4 simultâneas, 5/s) e na
mesma resiliência; HTML vira texto (`SeekHtml`); grava com origem `source`. 403, 429 depois das tentativas,
`cf-mitigated` ou resposta HTML = `SourceBlockedException`: a etapa para e o resto da passada segue.

### 2026-10-22 — Termos de cidadania com palavra de contexto (`RulesVersion` 7)
"australian citizen", "permanent resident", "full working rights" e "full australian working rights" passavam
de 500 mesmo por estado (2.000–3.250 anúncios em 31 dias), todo dia. Com as palavras de contexto dos perfis
ficam em 3–333 (ex.: "australian citizen" + FIFO 47, + labourer 64; "full australian working rights" + FIFO
333). `ScoringPolicy.ContextGroups` (cidadania e "full working rights") faz esses termos serem buscados também
como "termo + contexto" para cada palavra de contexto dos perfis (+20 buscas por passada); a busca sozinha
continua, para os perfis sem contexto. No score, "termo + contexto" conta como o termo; uma marca que é ela
própria termo da política ("working holiday + citizen") nunca é lida como "working holiday" + contexto.
Efeito colateral aceito: "HR licence + FIFO" (requisito) agora também conta como o "HR licence" da política;
o peso é o mesmo (−20, uma vez).

### 2026-10-22 — Etapa 10: 1ª passada agendada acompanhada
A passada das 06:00 de Perth (2026-10-21 22:00 UTC, 19:00 em Brasília) rodou sozinha, pelo serviço, em
cerca de 2 minutos: 256 buscas de vaga, 0 falhas, 0 cortadas no teto, 13 vagas novas; 136 termos, 0 falhas,
4 incompletos (os de cidadania e direito de trabalho, pendência acima); score dos 5 perfis regravado às
22:01; retrato do Raio-X `ok`. Nenhum aviso ou erro dos serviços no Event Log. Backup das 23:00 ok
(`jobradar_2026-10-21_230000.dump`, 7,9 MB; tarefa com resultado 0). Para fechar a etapa falta só desligar
o protótipo na outra máquina; depois, a etapa 9 (importar o `jobs.db` de lá).

### 2026-10-22 — Captura da descrição completa (v1.x, `RulesVersion` 6)
O que os rótulos ainda não explicavam (Driller's Offsider de Swick e Ozijobs) está no corpo do anúncio.
Entrou a captura: favorito "Send to JobRadar" + página `/capture` + campo de colar na página da vaga;
tabela `listing_descriptions` (migração `AddListingDescriptions`), repontuação na hora.
Regras sobre a descrição, escolhidas para um texto longo e com contexto:
- **Exclui** só por direito de trabalho incompatível (padrões específicos: unlimited/permanent rights,
  "12 months work rights", "no working holiday visas").
- **Cidadania/PR é −55, não exclusão**: "Australian or New Zealand citizens, PR or valid working visa holders"
  inclui WHV. Anulado por boas-vindas a WHV no texto, desde que não negadas logo antes ("unable to accept
  working holiday visas"). A descrição substitui as frases buscadas de cidadania, WHV e "full working rights".
- **Requisitos do perfil** contam quando a frase não vem negada logo antes ("no HRWL needed").
- **Sinais do perfil não leem a descrição**: num texto longo "supervisor", "manager" aparecem por acaso
  ("report to the site manager"); uma vaga com descrição não pode subir ou cair só por ter mais texto.
O site passou a ler os perfis do worker: o build copia `src/JobRadar.Worker/appsettings.Local.json` como
`profiles.json` (sem ele a captura salvava e não repontuava nada; achado no teste no navegador).
Não testável daqui: a recepção do texto vindo do Seek (não abro página de vaga). O seletor do corpo é
`[data-automation=jobAdDetails]`; se o Seek mudar, a seleção manual do texto continua funcionando.

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
