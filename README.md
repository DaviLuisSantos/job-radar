# JobRadar AU

Tracks the Australian job market for people arriving on a Working Holiday visa (subclass 417/462):
which jobs they can actually take, and how the market moves month to month.

Two products share one core:

- **Matching** (private): ranks new listings against a candidate profile, explains every score,
  and tracks applications.
- **Market snapshot** (public): aggregated numbers only, such as listings per city and area,
  the share that accepts working holiday makers, and the share that requires citizenship or PR.
  It feeds a monthly newsletter in Portuguese for Brazilians moving to Australia.

> **Status:** early. The architecture skeleton and the listing store are in place; collection,
> scoring and the market snapshot are being ported from a Python prototype.
> Scope and phases: [docs/visao.md](docs/visao.md) (Portuguese).

## Architecture

```
src/
  JobRadar.Domain           pure model: listings, locations, work types (no EF, no HTTP)
  JobRadar.Application      business services + ports (Abstractions/) they depend on
  JobRadar.Infrastructure   Postgres: entity → configuration → mapper → repository, migrations
  JobRadar.Worker           composition root and scheduled jobs
tests/
  *.Domain.Tests            pure unit tests
  *.Infrastructure.Tests    mapper tests + repository contract tests on real Postgres (opt-in)
  *.Worker.Tests            DI composition: the container builds and every port is registered
```

- Dependencies point inward; one project per layer lets the compiler enforce it.
- Repositories answer business questions and return domain models, never EF entities or `IQueryable`.
- Each job site will be an integration translating its data into the neutral `Listing`;
  the core never knows which site a listing came from. See [docs/fontes.md](docs/fontes.md).

## Running locally

Requires the .NET 10 SDK and Docker.

```bash
docker compose up -d
dotnet tool restore
dotnet ef database update --project src/JobRadar.Infrastructure
dotnet run --project src/JobRadar.Worker
```

## Tests

```bash
dotnet test
```

Contract tests start a real Postgres in a container and are skipped unless `JOBRADAR_DB_TESTS=1`:

```bash
JOBRADAR_DB_TESTS=1 dotnet test
```

## Data and ethics

Only aggregated numbers are published, never individual listings. The system does not bypass
anti-bot protection and does not open protected job pages.

## License

[MIT](LICENSE)
