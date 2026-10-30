# Triagem no SQL Server — medição (passo 1.5, 2026-10-30)

Dados reais copiados da produção (`copy-database SqlServer`) para um SQL Server 2022 local (Docker, na mesma máquina da
produção): 27.666 vagas, 29.270 notas. Medido `EfListingScoreRepository.ListForTriageAsync` com
`IncludeExcluded = true` (o que o `TriageService` pede desde a 5.3), banco aquecido, mediana de 5 execuções depois de uma
de aquecimento. O tempo inclui ida ao banco e materialização em .NET.

## O que a consulta fazia

O `select` trazia a entidade `listings` inteira e a explicação do score (`CASE WHEN score = 0 THEN explanation END`). Por
causa da explicação, o SQL Server lia cada linha de `listing_scores` inteira (índice clusterizado, 2.243 páginas) em vez do
índice `ix_listing_scores_profile_score`, e `listings` era varrida inteira (3.381 páginas).

## O que mudou

- Projeção só das colunas que a linha da triagem mostra (sem `url`, `last_seen_at`, `source_category`, explicação).
- A explicação das excluídas sai numa segunda consulta, só das linhas com nota 0 do perfil.
- Índice: nenhum ajuste. Com a projeção, `ix_listing_scores_profile_score (profile, score)` já cobre a parte de
  `listing_scores` (a chave clusterizada vem junto). `listings` continua varrida: são 8 mil de 27 mil linhas, e o hash join
  sai mais barato que 8 mil buscas; um `INCLUDE` em `listing_scores` não muda isso.

## Antes e depois

| Perfil | Linhas | Antes (ms) | Depois (ms) |
|---|---:|---:|---:|
| construction | 8.242 | 588 | 377 |
| trades | 8.386 | 458 | 307 |
| fifo | 7.201 | 402 | 259 |
| tech | 4.509 | 310 | 192 |
| farm | 790 | 193 | 102 |

Leituras lógicas no banco (construction): `listing_scores` 2.243 → 473; `listings` 3.381 → 3.381.

## O que fica

A triagem ainda traz todas as linhas do perfil e agrupa duplicatas em memória antes de paginar (armadilha registrada no
`CLAUDE.md`): é o que mantém as contagens certas. Com 8 mil linhas em ~0,4 s não vale empurrar o agrupamento ao banco; se um
perfil passar de umas 50 mil linhas, o próximo passo é paginar no banco por grupo (título + empresa normalizados numa
coluna calculada).
