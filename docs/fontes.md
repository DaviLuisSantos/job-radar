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
- User-Agent identificado (`JobRadarAU/0.1 (+repo)`), pausa de ~1 s com variação entre páginas,
  backoff em 429.
- Não abre páginas de vaga (Cloudflare). O texto completo só entra pela busca por frase (etapa 5).

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
