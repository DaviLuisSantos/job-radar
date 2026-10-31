# Fontes de vagas

> Avaliação inicial (2026-10-21). Itens marcados *verificar* não foram confirmados:
> confirme termos de uso e forma de acesso antes de integrar.

## Decidido (2026-10-21)

- **Domínio comum, peculiaridades por integração.** O núcleo não conhece nenhum site; cada um
  entra como `Integrations/<Site>` traduzindo para o modelo neutro.
- **v1: só Seek.** **v2: Adzuna primeiro, Workforce Australia junto com o recorte por região.**
- **Fora:** Indeed e LinkedIn (sem API de busca, anti-bot, risco jurídico). Jora não planejado. O LinkedIn entrou no
  Matching em 2026-10-30, só pelos endpoints de convidado ([abaixo](#linkedin-spike-de-2026-10-30)).
- **Ordem das fontes novas (2026-10-30):** ATS (Greenhouse), depois GitHub vagas (Brasil) e o Adzuna. Ver
  [ATS públicos](#ats-públicos-pesquisa-de-2026-10-30). O Indeed segue fora; a única porta prática é descrita lá.

## Candidatas

| Fonte | Acesso | Valor para o Raio-X | Risco | Quando |
|---|---|---|---|---|
| **Seek** | API de busca não documentada (JSON); páginas de vaga atrás de Cloudflare | maior volume; busca no texto completo (termos de elegibilidade) | termos proíbem acesso automatizado; só agregado em público | v1 |
| **Adzuna** | API pública oficial com chave (*verificar* termos para uso em newsletter) | agregador com categoria e salário; base legal melhor para números publicados | baixo | v2 (primeira nova fonte) |
| **Workforce Australia** (governo, inclui Harvest Trail) | site público (*verificar* se há API ou feed) | trabalho rural e colheita | baixo (dado público) | v2, junto do recorte por região |
| **Quadros de trabalho temporário** | sites pequenos (*verificar* um a um) | vagas casuais que não aparecem no Seek | variável | v2, se o Raio-X pedir |
| **Jora** | pertence ao Seek; agregador | pouco além do Seek | o mesmo do Seek | não planejado |
| **Indeed** | sem API pública para busca; anti-bot forte | volume alto | alto | fora |
| **LinkedIn** | endpoints de convidado (HTML), sem conta; anti-scraping agressivo, histórico de processos | nenhum: a busca não respeita frase e o total é arredondado | alto | Matching (AU e BR) desde 2026-10-30; fora do Raio-X |
| **ATS: Greenhouse** (depois Lever e Ashby) | API pública de job board de cada empresa, JSON, sem chave nem conta ([abaixo](#ats-públicos-pesquisa-de-2026-10-30)) | nenhum por ora: sem categoria, só Matching | baixo: endpoint público da plataforma, nenhum termo aceito | Matching, opcional (`OptIn`); [Fase 6](evolucao.md) |
| **GitHub vagas** (`frontendbr/vagas` e outros) | API de issues do GitHub, token opcional | nenhum: só Matching, só Brasil | baixo: API oficial | Fase 6, depois do ATS |

## Seek — como a integração funciona (etapa 2)

- Endpoint `GET /api/jobsearch/v5/search` (não documentado). Parâmetros em `SeekSearchClient.BuildUri`.
- Até 100 por página e no máximo 500 resultados por busca. Acima disso é preciso dividir a busca,
  por estado por exemplo.
- `totalCount` conta os promovidos repetidos em cada página. Para contar, use sempre `pageSize=100`.
- User-Agent identificado (`JobRadar/0.1 (+repo)`, até 2026-10-29 `JobRadarAU/0.1`), pausa de ~1 s com variação entre páginas,
  backoff em 429.
- Não abre páginas de vaga (Cloudflare). O texto completo entra pela busca por frase (etapa 5) e, desde
  2026-10-23, pelo endpoint GraphQL `jobDetails` que o próprio site usa (respondeu sem desafio, com o
  User-Agent identificado). Decisão do autor, aceitando o risco nos termos do Seek. Só para as vagas que
  importam (score ≥ 40, até 31 dias, até 3.000 por passada), no mesmo orçamento da busca; o primeiro
  403/429/desafio para a etapa.

## Regras do desenho

1. **Modelo neutro + dado bruto.** Cada integração grava o payload original e traduz para o
   modelo comum (`Listing`). Esquisitices do site ficam só no tradutor dela, testado com
   respostas gravadas.
2. **Capacidades explícitas.** Nem toda fonte faz tudo: listar vagas é obrigatório; contar
   por categoria e buscar no texto completo são capacidades opcionais, cada uma com sua
   porta. Fonte sem a capacidade é pulada, não quebra o serviço.
3. **Taxonomia própria.** Categorias de cada site são mapeadas para uma taxonomia do
   sistema (base na classificação oficial de ocupações australiana, *verificar* se
   ANZSCO ou OSCA). O mapeamento é dado, com teste.
4. **Local normalizado.** Cidade, estado e postcode canônicos; pré-requisito para recortes por região.
5. **Identidade por fonte, agrupamento entre fontes.** O ID é `fonte + id da fonte`. A mesma
   vaga em dois sites forma um grupo (título, empresa, local, data); nunca se apaga uma das duas.
6. **Números do Raio-X por fonte.** Cada fonte tem seu retrato e seu status de qualidade.
   Somar fontes só depois de agrupar duplicadas, e a metodologia diz qual número é qual.

## Brasil (spike de 2026-10-29, fase 2 da [evolução](evolucao.md))

Só para o Matching de uso próprio: nada do Brasil vai ao Raio-X público ([visao.md](visao.md)).

**Gupy** (portal de vagas de centenas de empresas brasileiras):
- O endpoint que coletores antigos usam (`portal.api.gupy.io/api/job`) e o `api/v1/jobs` respondem 404. O que a
  página de busca do portal chama é `GET https://portal.gupy.io/api/job-search/jobs?jobName=<termo>&limit=<n>&offset=<n>`:
  JSON, sem cookie nem login, responde também fora do navegador. `pagination.total` dá o total da busca.
- Campos: `id`, `name` (título), `description` (**o anúncio inteiro**, de 1 a 6 mil caracteres numa amostra de 49
  vagas de ".net": não precisa de busca de detalhe), `careerPageName` (empresa), `publishedDate`,
  `applicationDeadline`, `workplaceType` (`remote` | `hybrid` | `on-site`, às vezes ausente), `city`, `state`
  (nome por extenso), `jobUrl`, `type` e `badges`.
- `type` é o regime: `vacancy_type_effective` (CLT, 33 de 49), `vacancy_legal_entity` (PJ), `vacancy_type_associate`
  e `vacancy_type_talent_pool` (banco de talentos, que não é vaga aberta: 10 de 49, a excluir).
- Salário: não há campo; aparece no texto em cerca de 1 em cada 6 (8 de 49 citam "R$").
- `robots.txt` de `portal.gupy.io`: `Disallow:` vazio (libera tudo).
- **Termos de uso** (versão de 01/07/2026, "Diretrizes Gerais do Usuário", vale para usuários em geral): proíbem
  "agregar, copiar ou duplicar partes" da plataforma e interferir no serviço (sobrecarga). Não falam de robôs nem de
  coleta pelo nome. A "API pública" que citam é a de integração das empresas clientes, não esta busca.
  Mesma situação do Seek, cujos termos também proíbem acesso automatizado e que o projeto usa com ritmo baixo e só
  agregado em público.

**Decidido (2026-10-29): Gupy primeiro, nas regras do Seek** (uso pessoal, ritmo baixo, nada público).

**Adzuna** (agregador com API oficial e chave grátis): os termos permitem "personal job search" e pesquisa pessoal;
pedem crédito à Adzuna ao publicar. Existe `adzuna.com.br`; *verificar* com uma chave se a API responde para `br`
(sem chave a autenticação falha antes de olhar o país). Já é a primeira fonte nova planejada para a Austrália: uma
integração serviria aos dois países. A chave tem de ser criada pelo autor.

## Gupy — como a integração funciona (fase 2.5)

- `GET portal.gupy.io/api/job-search/jobs?jobName=&limit=&offset=` com `city=<cidade>` ou `workplaceType=remote`;
  até 100 por página (mais responde 400). Sem filtro de data: cada passada lê as páginas da busca (`MaxPages` 10).
- Orçamento próprio, abaixo do Seek: 1 requisição por segundo, 2 simultâneas, User-Agent identificado.
- `description` vai para `listing_descriptions` (uso pessoal, como as do Seek); banco de talentos é descartado.
- Estado por extenso → sigla (`"São Paulo"` → `SP`); CLT → tempo integral, PJ e associado → contrato.

## Remoto (spike de 2026-10-30, passo 5.5 da [evolução](evolucao.md))

Vagas remotas que aceitem quem mora no Brasil, nos dois mercados da fase 5: **LATAM** (restritas a uma região que inclui o
Brasil) e **global** (abertas ao mundo). Uma chamada por endpoint; amostras fora do repo.

| Fonte | Acesso | Elegibilidade | Salário | .NET a partir do Brasil | Veredito |
|---|---|---|---|---|---|
| **Himalayas** | `himalayas.app/jobs/api/search?q=&country=BR&worldwide=true&page=N` (20 por página), sem chave; com chave grátis, 60/min | `locationRestrictions`: lista de países por extenso; **vazia = mundo** | `minSalary`, `maxSalary`, `currency`, `salaryPeriod` (muitas vezes vazios) | `q=.net&country=BR`: 153; `worldwide`: 17 | **usar, os dois mercados** |
| **Jobicy** | `jobicy.com/api/v2/remote-jobs?count=100&geo=latam&industry=engineering`, sem chave; só os últimos 7 dias | `jobGeo`: texto com vírgulas ("Anywhere", "LATAM", "Brazil", "LATAM, Canada, USA") | estruturado (`salaryMin/Max/Currency/Period`) | pouco; `tag` só olha o título | **segunda fonte LATAM** |
| Get on Board | `getonbrd.com/api/v0/search/jobs?query=` (JSON:API) | `remote_modality`, `countries` = "Remote": não diz se o Brasil entra | USD/mês, às vezes | quase tudo Chile, Peru, México | esperar: termos e `remote_local` não conferidos |
| Remotive | `remotive.com/api/remote-jobs` | `candidate_required_location`, texto livre | texto | o feed público tinha 12 vagas; atraso de 24 h, 4 chamadas/dia | não |
| RemoteOK | `remoteok.com/api` | `location` vazio em mais da metade | anual em USD | nenhuma | não |
| We Work Remotely | só RSS | `region` ("Anywhere in the World") | só no texto | nenhuma | talvez, como complemento global |

- **Termos.** Himalayas: usar o `applicationLink` como veio, mostrar o link da fonte, "mostrar as vagas só em sites e
  apps que você opera", não repassar a outros quadros e tirar as encerradas. Jobicy: creditar a Jobicy como origem e
  manter o link dela. Uso pessoal e privado, como o do Seek e da Gupy, cabe nos dois.
- **Cuidados.** A descrição vem em HTML nas duas (precisa virar texto antes de ir para `listing_descriptions`). O Himalayas
  não tem id numérico: o `guid` é a URL da vaga. Muitas vagas de `country=BR` são de empresas brasileiras só para o
  Brasil: servem ao "remoto a partir do Brasil", mas não são LATAM no sentido da restrição regional. A mesma vaga aparece
  em vários quadros: a triagem já junta cópias por título e empresa.

**Decidido (2026-10-30): Himalayas para os dois mercados e Jobicy para o LATAM**, nas mesmas regras (uso pessoal, ritmo
baixo, nada público). Get on Board fica para quando os termos e a semântica de `remote_local` forem conferidos.

## Himalayas e Jobicy — como as integrações funcionam (passo 5.6)

- **Himalayas:** `GET himalayas.app/jobs/api/search?q=<palavra>&page=N` com `worldwide=true` (global) ou `country=BR`
  (LATAM); 20 por página, até 15 páginas. `locationRestrictions` vira `Listing.Eligibility` (vazia = "Worldwide");
  `timezoneRestrictions` vira "UTC-5 to UTC-2" no resumo (o mundo inteiro não restringe e fica de fora); salário
  estruturado vira "USD 4,000–6,000 per month". Id: "empresa/slug" do `guid`. Resposta lida como UTF-8 sempre.
- **Jobicy:** `GET jobicy.com/api/v2/remote-jobs?count=100&geo=latam&industry=engineering`, paginado por `nextCursor`;
  cada busca lê o feed e fica com as vagas que têm todas as palavras-chave no título ou no anúncio. `jobGeo` "Anywhere" vira
  "Worldwide".
- Os dois: orçamento de 0,5 requisição por segundo, uma por vez, User-Agent identificado; anúncio HTML gravado como texto.

## LinkedIn (spike de 2026-10-30)

**Decidido (2026-10-30), revendo o "fora" de 2026-10-21:** o LinkedIn entra no Matching (uso pessoal, ritmo baixo, nada
público), porque é onde estão as vagas de tecnologia. Só pelos endpoints de convidado, sem conta e sem login: nenhum
termo é aceito, que é o que separa este uso do caso hiQ × LinkedIn (a hiQ perdeu por quebra de contrato, com contas).
O `robots.txt` tem `Disallow: /jobs-guest/` e diz que acesso automatizado depende de permissão: risco aceito pelo autor,
como o do `jobDetails` do Seek. Nada do LinkedIn vai ao Raio-X.

**O que o spike mostrou** (chamadas com o User-Agent identificado, a partir do trabalhopc):
- Busca: `GET www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search?keywords=&location=&start=N`. Responde 200
  com `<li>` de cards em HTML, 10 por página, sem escolha. `start=850` ainda respondia, `start=1000` dá 400: teto de
  ~1.000 por busca. `f_TPR=r<segundos>` filtra pela idade (r86400 = um dia) e é o **único** filtro que este endpoint
  respeita: com ou sem tipo de vaga (`f_JT`), modalidade (`f_WT`) e senioridade (`f_E`), as vagas e a ordem são as mesmas
  (medido em 2026-10-30, valor único ou lista), e `sortBy=DD` não ordena pela data. Por isso nem a busca nem o card dizem
  se a vaga é remota, contrato ou sênior: quem diz é o título e, quando buscado, o anúncio ("Employment type", "Seniority
  level"). Uma rajada de 12 pedidos não foi recusada; o limite por IP não foi medido.
- Card: id (`urn:li:jobPosting:<id>`), título, empresa, local ("Osborne Park, Western Australia, Australia" ou
  "Greater Perth Area"), data (`<time datetime="AAAA-MM-DD">`), link e, raramente, `job-search-card__salary-info`.
- Anúncio: `GET .../jobs-guest/jobs/api/jobPosting/<id>`, 404 quando sumiu. O texto fica em
  `show-more-less-html__markup`; abaixo, "Seniority level", "Employment type", "Job function", "Industries"; a faixa
  salarial, quando há, em `class="salary compensation__salary"` (apareceu em vagas dos EUA). Sem desafio de Cloudflare.
- Local: "Perth WA", "Western Australia" e "Rio de Janeiro" resolvem; **"Brasil" em português devolve vagas dos EUA**
  (tem de ser "Brazil"), e um local desconhecido também cai nos EUA.
- **Não serve para termos nem para o Raio-X:** a busca não respeita frase entre aspas (`"white card" electrician`
  devolveu anúncios sem a frase), e o total vem arredondado ("2,000+").

**Como a integração funciona:** `JobRadar.Integrations.LinkedIn` com `IJobSource` (AU e BR,
configurável em `LinkedIn:Markets`) e `IDescriptionSource`; um cliente só (`LinkedInGuestClient`) para os dois endpoints,
com orçamento próprio (1/s, 2 simultâneas). A primeira recusa (403, 429, 999 ou redirecionamento para `/authwall`,
`/login`, `/checkpoint`) bloqueia todo pedido ao LinkedIn por 6 horas, sem perguntar de novo, porque a coleta roda todas
as buscas mesmo quando uma falha; 999 não é repetido pelo resiliente. HTML lido por regex sobre as classes do site
(sem parser). O payload gravado é o card em JSON, sem os selos que mudam todo dia ("Be an early applicant"). No Brasil,
o local ganha ", Brazil"; "Remoto" vira o país inteiro ("Brazil"), **sem** marcar as vagas como remotas: o filtro é
ignorado e vem vaga presencial junto (até 2026-10-30 elas ganhavam o bônus de remoto e escapavam do `OnSiteLocations`).
"Greater … Area" ganha o estado quando a cidade é uma capital conhecida ("Greater Perth Area" → Perth WA, "Greater Belo
Horizonte" → MG). Card de outro país é descartado; se a primeira página inteira é de outro país, a busca falha dizendo
que o local não foi reconhecido.

Medido em 2026-10-30, para escolher palavras-chave: no Brasil, "desenvolvedor .net" e ".NET developer" trazem quase as
mesmas vagas (21 de 30), então uma das duas basta; no AU, só 4 de 52 títulos de ".NET Developer" em Perth citam .NET ou
C#, porque a busca olha o anúncio inteiro e não respeita aspas. Quem separa é o score.

**Em produção desde 2026-10-30:** fonte opcional (`OptIn`). Só busca nos perfis que pedem, com
`"ExtraSources": ["linkedin"]` no `appsettings.Local.json`, porque a primeira passada sem janela pode passar de mil pedidos
e o perfil de fazenda ou de obra ganha pouco com ele. `LiveLinkedInTests` (busca em "Perth WA" e um anúncio) passou no
mesmo dia. Ligar em mais perfis um de cada vez, olhando na `/status` se alguma busca do LinkedIn falhou.

## ATS públicos (pesquisa de 2026-10-30)

Pesquisa na web e no GitHub por scrapers de vagas e por fontes que ainda faltam. Conclusão: **nenhuma ferramenta pronta
vale integrar** (todas são Python e refazem o que o `IJobSource` já faz); o que vale é o que elas descobriram: as APIs
públicas de job board dos sistemas de recrutamento (ATS) e as listas públicas de empresas que usam cada um.

**O que os ATS dão.** Cada empresa que usa Greenhouse, Lever ou Ashby publica o quadro de vagas em JSON, sem chave,
sem conta e sem anti-bot, no endereço que o próprio site de carreiras da empresa chama. É a primeira candidata sem
dívida jurídica: não há termo de uso aceito nem `robots.txt` a contrariar. O custo é outro: não existe busca por
palavra-chave, só o quadro inteiro de uma empresa por vez, então a coleta é uma **varredura** (baixar tudo, filtrar aqui).

| Plataforma | Endereço | Observações |
|---|---|---|
| Greenhouse | `boards-api.greenhouse.io/v1/boards/{slug}/jobs` (`?content=true` traz o anúncio) | `/jobs/{id}` devolve uma vaga com o texto; `ETag` e 304 |
| Lever | `api.lever.co/v0/postings/{slug}?mode=json` | `descriptionPlain` já em texto; `ETag` |
| Ashby | `api.ashbyhq.com/posting-api/job-board/{slug}?includeCompensation=true` | `secondaryLocations` e compensação; `ETag`; o mais apertado em ritmo |
| Workable | `apply.workable.com/api/v1/widget/accounts/{token}` | sem lista pública de tokens |
| SmartRecruiters | `api.smartrecruiters.com/v1/companies/{id}/postings` | aceita `country=`; sem lista pública |

**Medido em 2026-10-30, a partir do trabalhopc** (User-Agent identificado; amostras fora do repo):

| Fato | Resultado |
|---|---|
| Greenhouse `…/jobs` | 200, quadro inteiro num JSON (um quadro de 746 vagas: 465 KB) |
| Greenhouse `?content=true` | o mesmo quadro com o anúncio em HTML: 5,8 MB (12 vezes mais) |
| `If-None-Match` com o `ETag` | **304 sem corpo**; Lever e Ashby também mandam `ETag` |
| 20 pedidos, 8 simultâneos | 1,1 s, nenhum 429 (não prova que 8 mil aguentam: rajada é o que dispara anti-bot) |
| slug que não existe | 404 limpo; **metade** dos slugs de uma lista pública está morta |
| Quadros vivos (Greenhouse, 1.233 vagas) | 1,1% Austrália, 4,2% Brasil, 1,9% LATAM explícito |
| Lever (265 vagas) e Ashby (484) | 0 Austrália, 3 Brasil cada: começar pelo Greenhouse |
| Listas públicas de slugs (MIT, colhidas do Common Crawl) | 8.333 Greenhouse, 4.368 Lever, 3.161 Ashby |

Campos de uma vaga do Greenhouse: `id`, `title`, `updated_at`, `first_published`, `absolute_url`, `location.name` (texto
livre: "Dublin", "Sydney, Australia", "Remote - Brazil"), `offices`, `departments`, `company_name`; `content` só com o
parâmetro ou na vaga individual.

**Decidido (2026-10-30): Greenhouse primeiro, só Matching, opcional.** Lever e Ashby entram depois como fontes próprias no
mesmo projeto. Nada do ATS vai ao Raio-X (não há categoria; mesma regra do LinkedIn e da Gupy). A fonte nasce `OptIn`:
só roda nos perfis que a citarem em `ExtraSources`, para não mexer nos dados no meio da calibragem.

**Desenho** (revisado contra o código em 2026-10-30; plano em [evolucao.md](evolucao.md), Fase 6):
- `IJobSource.Name = "greenhouse"` (uma fonte por plataforma, regra 5 abaixo), `source_id = "{slug}/{id}"`. Sem hash no
  id: a etapa de descrição precisa montar `/{slug}/jobs/{id}` a partir dele. Slug longo demais para a coluna (64) é
  recusado e registrado.
- **A varredura é uma etapa do pipeline, não está dentro da `SearchAsync`.** O coletor roda as buscas em 6 vagas
  paralelas e uma busca que espera uma varredura de minutos seguraria a vaga; com a fonte primeiro na ordem, as outras
  fontes não rodariam. A etapa baixa os quadros, guarda em memória só as vagas dos mercados que importam e a
  `SearchAsync` apenas filtra esse resultado (como o Jobicy filtra o feed dele). A varredura é uma linha em
  `collection_runs` (`ats: sweep greenhouse`), com `complete` falso só se algum quadro falhou de verdade; **404 é o estado
  normal de um quadro morto e não torna a varredura incompleta**.
- **Tabela `ats_boards`** (plataforma + slug): `ETag`, último status, última varredura, falhas seguidas e a última vez que
  o quadro deu vaga em algum dos nossos mercados. Sem ela o `ETag` se perde a cada reinício.
- **A passada não pode estourar os 30 minutos** que a Fase 4 usa como gatilho: 8.333 quadros a 10 pedidos/s são 14 min só
  de varredura. Por isso a varredura é **fatiada** (a cada passada, os quadros varridos há mais tempo, até um teto) e
  **poda** os quadros que não deram vaga nos nossos mercados em semanas.
- Janela incremental ignorada, como no Jobicy: a localização é texto livre editável, e uma vaga antiga pode entrar num
  dos nossos mercados hoje. Para o ATS, `last_seen_at` é verdadeiro (a varredura vê o quadro todo); para as outras fontes
  continua valendo a armadilha da skill `fontes-de-vagas`.
- Sem `Summary` na varredura (sem `content=true`): a primeira pontuação usa título, departamento e escritório; a etapa de
  descrição busca o texto da vaga individual para as que passam do `Descriptions:MinScore`.
- A lista de slugs é vendorizada no repositório (origem e licença citadas) e entra na tabela por um comando
  (`ats-seed`), não como efeito escondido de uma busca.

### Ferramentas prontas avaliadas (2026-10-30)

| Ferramenta | Veredito |
|---|---|
| [speedyapply/JobSpy](https://github.com/speedyapply/JobSpy) (MIT, 4,6 mil estrelas) | **Não integrar.** LinkedIn, Indeed, Glassdoor, ZipRecruiter e outros, em Python. A única porta prática para o **Indeed**: chama o GraphQL privado do aplicativo iOS (`apis.indeed.com/graphql`) com uma chave extraída do binário. O `robots.txt` do Indeed proíbe `/graphql` e os termos proíbem acesso automatizado: risco acima do que o projeto aceita no Seek. Decisão pendente do autor, não "fora" para sempre. |
| [job-board-aggregator](https://github.com/Riley-Dorrington/job-board-aggregator) (MIT) | **Referência.** Origem das listas de slug (`data/<ats>_companies.json`) e dos limites de ritmo por plataforma: 30 trabalhadores em Greenhouse e Lever, 10 em BambooHR, 5 em Ashby. |
| [open-jobs](https://github.com/elliottdehn/open-jobs) (CC0) | **Dataset de calibragem, não fonte viva.** O crawler foi desligado em 10/10/2026; o último retrato (3,1 milhões de vagas, 5/10/2026) continua baixável. |
| [JobFunnel](https://github.com/PaulMcInnis/JobFunnel) | Arquivado em dezembro de 2025. |
| Apify, fantastic.jobs, SerpApi | Pagos e fecham o dado atrás de um fornecedor. Fora. |
| Arbeitnow | Grátis e sem chave, mas só Europa e Reino Unido. Não serve. |
| Jooble, Careerjet | Existem para AU e BR, mas os termos são opacos (o Careerjet pede o IP do usuário final). Depois do Adzuna. |
| Catho, InfoJobs, Vagas.com | Sem API pública, só scrapers de terceiros (a Catho atrás de Akamai). Fora, pela lógica que excluiu o Indeed. |

### GitHub vagas (Brasil, comunidade)

Vagas da comunidade brasileira de tecnologia publicadas como **issues do GitHub**: `frontendbr/vagas` (15,5 mil estrelas),
`backend-br/vagas` (8 mil) e `soujava/vagas-java` (1 mil), com issues abertas em 2026-10-27. API oficial (60 pedidos/h
sem token, 5.000 com); o corpo da issue é o anúncio inteiro e os rótulos dizem regime e modalidade (exemplo: `PJ`,
`Sênior`, `Remoto`, `verificada`). O título começa com `[Remoto]`, `[Híbrido]` ou `[Presencial]`. Volume pequeno, qualidade
alta, risco zero. Só Brasil, só Matching.

### Adzuna: o que a pesquisa confirmou

`au` **e** `br` existem na mesma API (`api.adzuna.com/v1/api/jobs/{país}/search/{página}`; a página vai no caminho).
Limites informados por terceiros em outubro de 2026: 25/min, 250/dia, 2.500/mês, ampliáveis sob pedido para quem publica
(*verificar* em `developer.adzuna.com` antes de integrar). Segue como a única candidata com base legal para número
publicado no Raio-X; a chave tem de ser criada pelo autor.
