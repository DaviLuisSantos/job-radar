# JobRadar AU

Tracks the Australian job market for people arriving on a Working Holiday visa (subclass 417/462):
which jobs they can actually take, and how the market moves month to month.

Two products share one core:

- **Matching** (private): ranks new listings against a candidate profile, explains every score,
  and tracks applications.
- **Market snapshot** (public): aggregated numbers only, such as listings per city and area,
  the share that accepts working holiday makers, and the share that requires citizenship or PR.
  It feeds a monthly newsletter in Portuguese for Brazilians moving to Australia.

> **Status:** early. Daily collection from Seek, full-text clause detection, scoring per profile
> and application tracking work end to end; the scorer matches the Python prototype on 5,388 real
> listings. Triage UI and the market snapshot are next.
> Scope: [docs/visao.md](docs/visao.md) · roadmap: [docs/plano.md](docs/plano.md) (Portuguese).

## Architecture

```
src/
  JobRadar.Domain           pure model: listings, locations, work types (no EF, no HTTP)
  JobRadar.Application      business services + ports (Abstractions/) they depend on
  JobRadar.Infrastructure   Postgres: entity → configuration → mapper → repository, migrations
  JobRadar.Integrations.Seek  Seek search API: resilient HTTP client, translator to the neutral model
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

Describe your profiles first: copy `src/JobRadar.Worker/appsettings.Local.example.json` to
`appsettings.Local.json` (git-ignored). Every day at 06:00 Perth time the worker searches each
profile's keywords in its locations, then scores what each profile found. Set
`Collection__RunOnStartup=true` to run a pass immediately.

Track applications and check the score against your judgement with the same executable:

```bash
dotnet run --project src/JobRadar.Worker -- status seek:81234567 applied
dotnet run --project src/JobRadar.Worker -- label seek:81234567 good "exactly my stack"
dotnet run --project src/JobRadar.Worker -- calibrate
```

`calibrate` reports, per profile, how often a listing you liked outscores one you did not, and
lists where the score disagrees with you, with the rules that weighed most.

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
anti-bot protection and does not open protected job pages. Requests identify the bot in the
User-Agent, are paced with jitter, and back off on HTTP 429.

## License

[MIT](LICENSE)
