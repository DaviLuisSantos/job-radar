# Fontes futuras (backlog)

> Tudo o que a pesquisa de **2026-10-30** achou e que ainda **não** está implementado, com o que já se sabe para não
> começar do zero. O que está em produção e as decisões ficam em [fontes.md](fontes.md); a ordem de execução, na
> [Fase 6 de evolucao.md](evolucao.md#fase-6--fontes-sem-dívida-jurídica-planejada-em-2026-10-30). Medidas feitas do
> trabalhopc com User-Agent identificado; amostras fora do repo. Itens *verificar* não foram conferidos.

## Ordem sugerida

| # | Fonte | Esforço | Risco | Por que nessa posição |
|---|---|---|---|---|
| 1 | GitHub vagas (Brasil) | pequeno | nenhum | API oficial, anúncio inteiro, sem tabela nova |
| 2 | Lever e Ashby | pequeno cada | baixo | mesmo desenho do Greenhouse; só novos tradutores |
| 3 | Adzuna (AU e BR) | médio | baixo | única que pode alimentar número publicado; precisa da chave do autor |
| 4 | Workable e SmartRecruiters | médio | baixo | falta descoberta de empresas; endpoint já respondeu |
| 5 | Get on Board, We Work Remotely | pequeno | baixo | remoto; esperam conferência de termos e semântica |
| 6 | BambooHR, Workday, iCIMS | grande | médio | listas de empresas existem, mas cada API é diferente e Workday cobra um pedido por vaga |
| 7 | Jooble, Careerjet | médio | médio | termos opacos; só se o Adzuna não bastar |
| 8 | Indeed (via a técnica do JobSpy) | médio | **alto** | decisão do autor; ver abaixo |

## ATS: mesma família do Greenhouse

O desenho está pronto e provado no Greenhouse ([fontes.md](fontes.md#ats-públicos-pesquisa-de-2026-10-30)): varredura
em etapa própria (`IBoardSweep`), tabela `ats_boards` com `ETag`, id `slug/id` sem hash, uma fonte por plataforma
(`IJobSource.Name = "lever"`...). Para cada plataforma nova: um cliente, um tradutor, uma `IJobSource`, uma
`IDescriptionSource` se o quadro não trouxer o texto, e a lista de slugs. O `ats_boards.platform` já separa as
plataformas, então não precisa de migration.

| Plataforma | Endereço | Amostra de 2026-10-30 | Lista de slugs | Detalhes |
|---|---|---|---|---|
| **Lever** | `api.lever.co/v0/postings/{slug}?mode=json` | 13 de 40 quadros vivos; 265 vagas: 0 AU, 3 BR, 2 LATAM | `src/JobRadar.Integrations.Ats/Data/lever-boards.json` (4.365) | `ETag`; resposta é um array; `categories.location` e `categories.allLocations`; `descriptionPlain` já em texto, então dispensa `IDescriptionSource`; `createdAt` em milissegundos. Um quadro grande (Palantir) tem 5,8 MB. |
| **Ashby** | `api.ashbyhq.com/posting-api/job-board/{slug}?includeCompensation=true` | 24 de 40 vivos; 484 vagas: 0 AU, 3 BR | `.../Data/ashby-boards.json` (3.161) | `ETag`; **slug diferencia maiúsculas**; `location` e `secondaryLocations[].location`, `employmentType`, compensação estruturada (aproveitar em `SalaryText`); um quadro de teste (Ramp) tinha 3 MB. A fonte é a mais apertada em ritmo: o job-board-aggregator usa 5 trabalhadores, contra 30 do Greenhouse e do Lever. |
| **Workable** | `apply.workable.com/api/v1/widget/accounts/{token}` | respondeu 200 (`{"name","description","jobs":[]}`), mas a empresa testada tinha 0 vagas | não há lista pública pronta | validar com uma empresa que tenha vagas e achar uma lista de tokens |
| **SmartRecruiters** | `api.smartrecruiters.com/v1/companies/{id}/postings?limit=&country=` | respondeu 200 com `totalFound: 0` para as duas empresas testadas | não há lista pública pronta | aceita `country=au`/`br`, o que dispensa baixar tudo; validar com uma empresa que tenha vagas |
| **BambooHR** | por empresa, endereço a conferir | não medido | 11.316 (job-board-aggregator, `data/bamboohr_companies.json`) | *verificar* o endpoint e se há `ETag` |
| **Workday** | `{tenant}.wd{n}.myworkdayjobs.com`, API própria por tenant | não medido | 12.884 entradas `tenant|wdN|site` (job-board-aggregator) | um pedido a mais **por vaga** para o texto; o mais caro dos ATS |
| **iCIMS** | por empresa, a conferir | não medido | 10.108 (job-board-aggregator) | *verificar* o endpoint |
| Paylocity | — | a lista pública não existe mais (404) | — | ignorar |

**Onde achar as listas inteiras de novo:** `Riley-Dorrington/job-board-aggregator`, pasta `data/` (`<plataforma>_companies.json`),
MIT, colhidas do Common Crawl; metade dos quadros está morta, e a varredura descobre isso sozinha. O corte feito aqui
(`[a-z0-9][a-z0-9_-]+`, só dígitos fora) está em `Data/*-boards.json`. **Ao refazer a lista, tire também os slugs que contêm o vocabulário
de `PublicVocabularyTests`** (há nomes de empresa que o contêm): o teste só olha arquivo rastreado pelo git, então
uma lista nova passa na máquina e falha no CI (aconteceu em 2026-10-31).

**O que vale em qualquer plataforma nova.** (1) Medir antes de codar: 40 quadros aleatórios dão quantos estão vivos e quantas
vagas AU, BR e LATAM, como foi feito aqui. (2) O anúncio individual só entra pela etapa de descrição. (3) Slug que não cabe em
`source_id` (64) é recusado. (4) Lembrar da armadilha do Greenhouse: o texto pode vir com o HTML escapado.

## GitHub vagas (comunidade brasileira)

Vagas publicadas como **issues do GitHub**. Estado em 2026-10-30:

| Repositório | Estrelas | Issues abertas | Último push |
|---|---|---|---|
| `frontendbr/vagas` | 15,5 mil | 15 | 2026-10-09 |
| `backend-br/vagas` | 8 mil | 50 | 2026-10-26 |
| `soujava/vagas-java` | 1 mil | 29 | 2026-10-25 |
| `react-brasil/vagas` | 3,1 mil | 9 | 2024-01-09 (parado) |

- API: `GET /repos/{dono}/{repo}/issues?state=open&per_page=100` (60 pedidos/h sem token, 5.000 com). Autenticada por token
  opcional em `appsettings.Local.json`; **nunca** no repositório.
- Uma issue de exemplo: título `[Remoto] Desenvolvedor(a) Full Stack Sênior — … | 4 dias / 32h`, rótulos
  `["PJ","Sênior","Python","Remoto","AWS","TypeScript","verificada"]`, corpo com seções "Sobre a empresa", "Site da empresa"...
  (4 mil caracteres). O título abre com `[Remoto]`, `[Híbrido]` ou `[Presencial]`.
- Tradução: modalidade e regime vêm do título e dos rótulos (`PJ` → contrato, `CLT` → tempo integral); corpo da issue →
  `Description` (já vem na busca, sem etapa de descrição); `number` → `source_id` com o repositório (`dono/repo#n`);
  `created_at` → `PostedAt`; localização no corpo, em texto livre. `Markets = {BR}`, sem `OptIn`. Ignorar issue fechada.
- Perguntas abertas: onde fica a cidade (corpo, não rótulo); como o rótulo `verificada` pesa; paginação dos 50+.
- Fora do Raio-X, como toda fonte BR.

## Adzuna (AU e BR)

- `au` e `br` na mesma API: `api.adzuna.com/v1/api/jobs/{país}/search/{página}` (a **página vai no caminho**), com `app_id` e
  `app_key`. Limites informados por terceiros em outubro de 2026: 25/min, 250/dia, 1.000/semana, 2.500/mês; ampliáveis
  sob pedido a quem publica. *Verificar* tudo em `developer.adzuna.com` (os termos pedem crédito à Adzuna ao publicar).
- Tem endpoints de contagem e de estatística salarial: úteis ao Raio-X.
- **A chave só o autor cria.** Sem ela a autenticação falha antes de olhar o país.
- Única candidata com base legal para número publicado; ver [fontes.md](fontes.md#adzuna-o-que-a-pesquisa-confirmou).

## Remoto: o que já foi avaliado e ficou de fora

Tabela completa em [fontes.md](fontes.md#remoto-spike-de-2026-10-30-passo-55-da-evolução). Resumo do que falta:

| Fonte | Endereço | Por que esperou |
|---|---|---|
| Get on Board | `getonbrd.com/api/v0/search/jobs?query=` (JSON:API) | não diz se o Brasil entra (`remote_modality`); quase tudo Chile, Peru e México; termos e `remote_local` não conferidos |
| We Work Remotely | só RSS; `region` ("Anywhere in the World") | sem salário estruturado; serve de complemento global |
| Remotive | `remotive.com/api/remote-jobs` | feed público com 12 vagas, atraso de 24 h, 4 chamadas/dia |
| RemoteOK | `remoteok.com/api` | `location` vazio em mais da metade; nenhuma vaga .NET |
| Arbeitnow | `arbeitnow.com/api/job-board-api` (*verificar* o endereço) | grátis e sem chave, mas só Europa e Reino Unido |

## Agregadores com chave

| Fonte | O que se sabe | Risco |
|---|---|---|
| **Jooble** | 70+ países, chave por formulário, limites e preço não publicados | termos opacos |
| **Careerjet** | 80–90 países; chave amarrada a **um site que exibe os resultados**; cada chamada pede o IP e o User-Agent do usuário final | desenhado para publisher, não para pipeline |

Só depois do Adzuna, e só se a cobertura ainda faltar. Conferir os termos primeiro, como foi feito para o LinkedIn.

## Indeed (decisão do autor, hoje "fora")

A única porta prática é a do `speedyapply/JobSpy` (MIT, 4,6 mil estrelas): chama o **GraphQL privado do aplicativo iOS**
(`apis.indeed.com/graphql`) com uma `Indeed-API-Key` extraída do binário. Aceita Austrália e Brasil (`country_indeed`);
sem limite de ritmo relatado; 1.000 resultados por busca; descrição exige um pedido por vaga.

- O `robots.txt` do Indeed proíbe `/graphql` e os termos proíbem acesso automatizado.
- É uma chave de terceiros, sem consentimento, e quebra quando o Indeed muda o aplicativo: risco **acima** do aceito no Seek
  (`jobDetails`) e no LinkedIn (endpoints de convidado).
- Decidir antes de qualquer código; se for, vale ler o código do JobSpy para o desenho e **não** vendorizar a chave.

## Governo da Austrália e quadros pequenos

| Fonte | Situação |
|---|---|
| **Workforce Australia** (inclui Harvest Trail) | a pesquisa não achou API nem feed; é um spike de site público a fazer à mão. Dado governamental, risco baixo. A única que cobre trabalho rural e colheita fora do Seek. |
| **Jora** | do grupo Seek; sem API; pouco além do Seek |
| **CareerOne** | quadro antigo; sem feed conhecido |
| Quadros de trabalho temporário | sites pequenos, um a um (*verificar*) |

## Brasil: o que ficou de fora

| Fonte | Por quê |
|---|---|
| **Catho** | sem API pública; só scrapers de terceiros, atrás de Akamai: frágil |
| **InfoJobs Brasil**, **Vagas.com** | sem API pública; só scrapers de terceiros |
| **Programathor**, **Remotar** | a pesquisa não achou scraper nem API |
| **Gupy**, **LinkedIn**, **Himalayas**, **Jobicy** | já integradas |

## Dados e ferramentas de apoio (não são fontes vivas)

| O quê | Para quê |
|---|---|
| `elliottdehn/open-jobs` (CC0) | o crawler foi desligado em 10/10/2026; o último retrato (3,1 milhões de vagas, 5/10/2026) continua baixável. Serve de **dataset de calibragem** do score e para medir cobertura AU/BR antes de escrever um tradutor. |
| `Riley-Dorrington/job-board-aggregator` (MIT) | referência de listas de empresas e de ritmo por plataforma; atualizado diariamente |
| `speedyapply/JobSpy` | referência de como cada site é lido (ver Indeed acima) |
| fantastic.jobs, SerpApi, Apify | pagos (por exemplo ~US$ 95 por 20 mil vagas); fecham o dado atrás de um fornecedor. Reavaliar só se o Raio-X precisar de base legal que as fontes abertas não dão. |
| `PaulMcInnis/JobFunnel` | arquivado em dezembro de 2025; ignorar |
