# Fontes de vagas

> Avaliação inicial (2026-10-21). Itens marcados *verificar* não foram confirmados:
> confirme termos de uso e forma de acesso antes de integrar.

## Decidido (2026-10-21)

- **Domínio comum, peculiaridades por integração.** O núcleo não conhece nenhum site; cada um
  entra como `Integrations/<Site>` traduzindo para o modelo neutro.
- **v1: só Seek.** **v2: Adzuna primeiro, Workforce Australia junto com os 88 dias.**
- **Fora:** Indeed e LinkedIn (sem API de busca, anti-bot, risco jurídico). Jora não planejado.

## Candidatas

| Fonte | Acesso | Valor para o público WHV | Risco | Quando |
|---|---|---|---|---|
| **Seek** | API de busca não documentada (JSON); páginas de vaga atrás de Cloudflare | maior volume; busca no texto completo (termos de direito de trabalho) | termos proíbem acesso automatizado; só agregado em público | v1 |
| **Adzuna** | API pública oficial com chave (*verificar* termos para uso em newsletter) | agregador com categoria e salário; base legal melhor para números publicados | baixo | v2 (primeira nova fonte) |
| **Workforce Australia** (governo, inclui Harvest Trail) | site público (*verificar* se há API ou feed) | trabalho rural e colheita, que conversa com os 88 dias | baixo (dado público) | v2, junto dos 88 dias |
| **Quadros para backpackers** | sites pequenos (*verificar* um a um) | vagas casuais que não aparecem no Seek | variável | v2, se o Raio-X pedir |
| **Jora** | pertence ao Seek; agregador | pouco além do Seek | o mesmo do Seek | não planejado |
| **Indeed** | sem API pública para busca; anti-bot forte | volume alto | alto | fora |
| **LinkedIn** | anti-scraping agressivo, histórico de processos | vagas de tecnologia | alto | fora |

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
4. **Local normalizado.** Cidade, estado e postcode canônicos; pré-requisito para os 88 dias.
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

**Adzuna** (agregador com API oficial e chave grátis): os termos permitem "personal job search" e pesquisa pessoal;
pedem crédito à Adzuna ao publicar. Existe `adzuna.com.br`; *verificar* com uma chave se a API responde para `br`
(sem chave a autenticação falha antes de olhar o país). Já é a primeira fonte nova planejada para a Austrália: uma
integração serviria aos dois países. A chave tem de ser criada pelo autor.
