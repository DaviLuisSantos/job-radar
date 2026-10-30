---
name: banco-e-migrations
description: Persistência do JobRadar com EF Core em dois bancos (Postgres e SQL Server): migrations, ProviderModel, collation, repositórios EF, mappers e armadilhas do Npgsql. Use antes de mudar uma entidade ou configuration do EF, criar migration, escrever um repositório ou consulta LINQ, ou mexer em src/JobRadar.Infrastructure.
---

# Banco e migrations

## Migrations

```bash
dotnet ef migrations add <Nome> --project src/JobRadar.Infrastructure.Migrations.Postgres --output-dir .
```

```bash
dotnet ef migrations add <Nome> --project src/JobRadar.Infrastructure.Migrations.SqlServer --output-dir .
```

- Mudou o modelo? Uma migration em **cada** projeto de migrations, com o mesmo nome. `DatabaseProviderTests` (sem banco)
  falha se um deles ficar para trás.
- Migration gerada pelo EF pode escolher a renomeação errada. Leia antes de commitar
  (`SplitLocationIntoSuburbAndRegion` foi corrigida à mão: `city` → `region`).
- O contexto só encontra o banco por `UseJobRadarDatabase` (`Database:Provider`, padrão Postgres), que aponta o assembly
  de migrations do provider pelo nome. Quem aplica migrations (o bundle do `deploy.ps1`, a fixture dos testes) referencia
  esse assembly; o site e o worker não aplicam e não precisam dele.

## Armadilhas

- Npgsql só grava `timestamptz` com offset 0: o mapper converte com `ToUniversalTime()`
  (`ListingMapper.Refresh`).
- O `DbContext` vive o pass inteiro de coleta (um escopo): `EfListingRepository` limpa o
  `ChangeTracker` depois de cada gravação, com sucesso ou falha. Sem isso, uma falha derrubava o registro da
  execução e centenas de buscas acumulavam entidades rastreadas.
- Dois bancos (`Database:Provider`): o mapeamento que difere por banco mora **só** no `ProviderModel` (jsonb e identity do
  Postgres, collation do SQL Server); a configuração de cada entidade é a mesma para os dois. Sem `ILike` nem SQL cru:
  busca de texto é `ToLower().Contains()`, que o EF escapa nos dois. O SQL Server usa collation sensível a caixa e
  acento (`Latin1_General_100_CS_AS_SC_UTF8`), como o Postgres; ela só vale se o banco for **criado** pelas migrations
  (`Migrate` num banco que não existe): a migration não altera a collation de um banco já criado.
  A collation não basta: o provider do SQL Server compara chaves de texto **sem** caixa no change tracker, e o
  `ProviderModel` troca por ordinal (`Text_keys_compare_by_case_and_accent_in_memory_too`). JSON no SQL Server é
  `nvarchar(max)` com `CHECK (ISJSON(...) = 1)`, para recusar o que o jsonb recusa.
- Parâmetro chamado `from` dentro de consulta LINQ precisa de `@from` (`EfMarketRepository`).
- Paralelismo: acesso ao banco dentro de um serviço paralelo passa pelo `SemaphoreSlim` (um `DbContext`
  por escopo não aceita duas operações).
