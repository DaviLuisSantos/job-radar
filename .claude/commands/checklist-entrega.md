---
description: Checklist pre-entrega — build, testes, convencoes e qual validacao manual aplicar
allowed-tools: Bash(dotnet build:*), Bash(dotnet test:*), Bash(git diff:*), Bash(git status:*), Bash(git log:*)
---

Nao pule etapas; reporte cada uma com o resultado.

1. `dotnet build` — 0 erros e 0 warnings (warnings sao erros neste repo).
2. `dotnet test` — se falhar, cole a saida e PARE. Se o diff toca `src/JobRadar.Infrastructure/`,
   rode tambem com `JOBRADAR_DB_TESTS=1` (precisa de Docker).
3. Convencoes do CLAUDE.md que o diff pode ter violado (idioma, camadas, commit sem atribuicao a IA).
4. Mudou entidade ou configuration do EF? Confira se existe migration nova no diff.
5. Resumo: build, testes X/Y (contrato rodou ou nao), pendencias.
