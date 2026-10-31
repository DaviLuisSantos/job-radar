# Plano — v1 (uso próprio)

> **Documento vivo.** Estado real, em ordem. Ao fechar uma etapa: marque ✅ com a data e o que
> ficou provado, registre no Diário (fora do repositório, ver o fim) e passe a próxima para 🔨.
> Escopo e fases: [visao.md](visao.md). Fontes: [fontes.md](fontes.md).

## Etapas

| # | Etapa | Estado |
|---|---|---|
| 1 | Esqueleto: camadas, `IListingRepository` + Postgres, testes de contrato, composição, CI | ✅ 2026-10-21 |
| 2 | Integração Seek: porta `IJobSource`, cliente HTTP com resiliência, tradutor para `Listing` testado com respostas gravadas | ✅ 2026-10-21 |
| 3 | Coleta: serviço que roda as buscas configuradas, grava vagas + payload bruto, job agendado no Worker, log de execuções | ✅ 2026-10-21 |
| 4a | Score: regras do protótipo portadas como domínio puro (`FitScorer`) com explicação tipada; `Listing.Summary`; paridade com o Python | ✅ 2026-10-21 |
| 4b | Score em uso: perfis na configuração com as buscas de cada um; registro de qual busca achou cada vaga; score gravado por vaga × perfil, recalculado todo dia | ✅ 2026-10-21 |
| 5 | Termos do texto completo: busca por frase no Seek (quem pode se candidatar, requisitos), aplicados ao score | ✅ 2026-10-21 |
| 6 | Candidaturas e rótulos: status, datas, notas; rótulos bom/ruim e calibragem do score | ✅ 2026-10-21 |
| 7 | Triagem: interface para revisar vagas novas, ver a explicação do score, rotular e marcar candidatura | ✅ 2026-10-21 |
| 8 | Raio-X: contagens por subclassificação e cidade, retrato diário com status de qualidade, relatório mensal | ✅ 2026-10-21 |
| 9 | Importação do histórico do protótipo (vagas, rótulos, candidaturas, `market_counts`) | ✅ 2026-10-23 |
| 10 | **Corte**: Worker rodando todo dia em produção, Python desligado | ✅ 2026-10-23 |

Pendências conhecidas, com a etapa onde entram:
- **Etapa 9:** as 9.593 vagas importadas ganham score na passada de 2026-10-24 (19:00 de Brasília). Conferir na
  triagem que aparecem nos 5 perfis.
- **v2 (newsletter):** áreas e cidades do Raio-X ainda são as do protótipo (HVAC-R, Elétrica,
  Desenvolvimento; Perth + Austrália). Mudar = configurar `Market` (cada lista configurada substitui o padrão). Os
  termos não têm padrão no código desde 2026-10-30: só `Market:Terms`, no arquivo local.
- **LinkedIn (2026-10-30):** fonte opcional, desligada até um perfil pedir. Ligar com `"ExtraSources": ["linkedin"]` nos
  perfis de tecnologia do arquivo local da produção, um de cada vez, e conferir na `/status` a primeira passada
  ([fontes.md](fontes.md#linkedin-spike-de-2026-10-30)).
- **Depois da v1:** migrar a produção do PC para uma VM (opções pesquisadas em `docs/producao.md`).
- **Voltar sem login ([#29](https://github.com/DaviLuisSantos/job-radar/issues/29)), decidido em 2026-10-30:** o autor quis
  uma solução que não dependa de SO. Tudo virou containers (`docker compose --profile prod`, scripts em PowerShell 7), igual
  em qualquer host com Docker. No PC do autor o Docker Desktop continua subindo só no login (opção C, aceita); num host
  Linux a pilha sobe no boot, sem mudar nada no JobRadar. Opções descartadas: Postgres nativo como serviço do Windows
  (amarra ao Windows), Docker Engine no WSL ligado por tarefa (frágil e ainda do Windows).
- **Login no site:** decidido não ter (2026-10-21): o site só escuta em `localhost` e no IP da Tailscale, com
  firewall restrito a `100.64.0.0/10`. Se um dia for público, entra autenticação antes. Porta 5380 porque a
  5080 é de outro serviço do autor nesta máquina (Servidor de Câmeras).
- **Termos de elegibilidade:** corrigido em 2026-10-22 com a palavra de contexto; tech e o perfil de fazenda
  ganharam as suas em 2026-10-23 (ver Diário). A busca do termo sozinho segue incompleta todo dia, sem
  efeito: todo perfil agora tem contexto. A passada de 2026-10-24 (Perth) **ainda não tinha** os novos: 412
  execuções, o mesmo de antes (seriam ~452), e as palavras entraram às 19:14 de Brasília, depois do início
  dela. Elas moram no `appsettings.Local.json`, que só chega à produção pelo `deploy.ps1`. Depois de implantar,
  conferir na `/status` ("Last pass") que nenhum termo "+ contexto" aparece como incompleto.
- **Etapa 10:** com páginas completas, as buscas dos 5 perfis (256 desde 2026-10-21; eram 311) fazem até
  ~1.300 requisições numa passada sem janela. Depois, a janela incremental reduz muito.
- **Acerto do score (decidido 2026-10-21, nesta ordem):** (1) rotular: meta de 50 julgadas por perfil, com
  a ordem "across scores" da triagem para não rotular só o topo; (2) ajustar regras e pesos com o
  `calibrate` antes e depois; (3) descrição completa (v1.x, abaixo); (4) ML (v2/v3). **Fontes novas
  esperam:** não melhoram o ranking e mudariam os dados no meio da calibragem. Revisto em 2026-10-30 (pesquisa em
  [fontes.md](fontes.md#ats-públicos-pesquisa-de-2026-10-30)): a primeira será o ATS (Greenhouse), depois GitHub vagas
  (Brasil) e o Adzuna; nascem `OptIn`, ligadas num perfil por vez, o que protege a calibragem. Plano na Fase 6 de
  [evolucao.md](evolucao.md). O Adzuna segue sendo a que pode alimentar o Raio-X.
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

### Evolução: JobRadar com mais de um país (decidido 2026-10-29)

SQL Server ao lado do Postgres, Brasil no Matching (Gupy), observabilidade e mensageria, em fases com
critério de pronto: [evolucao.md](evolucao.md), onde fica o estado. A Gupy não mexe nos dados da calibragem
da Austrália (país separado), por isso não espera por ela; o Adzuna continua a primeira fonte nova da Austrália.

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
   Lido **só** pelas regras de requisito e de elegibilidade (ver Diário 2026-10-22).

Cuidados: o texto fica só no banco do autor (uso pessoal); nunca entra no Raio-X, no repositório nem em
fixture. A busca por frase (etapa 5) continua para as vagas sem descrição.

### v1.x — Perfil a partir do currículo (decidido 2026-10-23)

Hoje o perfil mistura duas coisas: **fatos** sobre o candidato (formação, carteiras, tickets, anos de
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
−20 requisito, −55 elegibilidade, ±16/8 sinais…), a `ScoringPolicy` (só padrões do código), frases repetidas em cada
perfil (`Requirements`, `QualifiedTitles`, `DisqualifySignals`) e regex em `ListingPatterns`. E nada sabe o que o
candidato **tem**: que tiraria o HRWL e que o ARCtick está fora de alcance mora na cabeça dele. O Seek não filtra
por isso. Três camadas:

1. **Catálogo de requisitos** (repo, conhecimento de mercado, igual para todos): item com `id` (`white-card`,
   `hrwl`, `forklift`, `truck-hr`, `arctick`, `trade-qualified`, `degree`, `experience-years`…),
   tipo, frases como os anúncios escrevem, custo de obter (curso curto, anos, certificado), e se é buscado no
   Seek (custa uma busca por palavra de contexto em cada passada) ou só lido nas descrições. Itens com leitura
   especial (carteira com "desirable", elegibilidade com negação) apontam para o detector que já existe.
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

1. **Regras como trava**: exclusões (status reservado, condição que o perfil não atende, vaga reservada)
   continuam decidindo; o modelo só ordena o que sobra.
2. **Modelo simples e explicável primeiro**: regressão logística (ML.NET) sobre os itens da explicação + palavras
   do título/resumo (e da descrição completa, quando houver). Aprende os pesos que hoje são fixos.
   Só substitui as regras se ganhar no acerto de pares em rótulos separados para teste.
3. **Depois, se valer**: embeddings de texto (sinônimos: "Junior .NET" ≈ "Graduate C#") + classificador pequeno.
4. **Aprendizado ativo na triagem**: mostrar primeiro as vagas em que o modelo tem mais dúvida.

Cuidado com o viés de seleção: só se rotula o que aparece no topo. Rotular de vez em quando vagas de score
baixo, ou o modelo só aprende a separar "boa" de "quase boa".

## Diário

Fora do repositório: `docs/private/diario.md` no checkout principal (ignorado pelo git), com o registro datado
de cada etapa. Entrada nova no topo.
