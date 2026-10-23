# JobRadar AU

Tracks the Australian job market for people arriving on a Working Holiday visa (subclass 417/462):
which jobs they can actually take, and how the market moves month to month.

Two products share one core:

- **Matching** (private): ranks new listings against a candidate profile, explains every score,
  and tracks applications.
- **Market snapshot** (public): aggregated numbers only, such as listings per city and area,
  the share that accepts working holiday makers, and the share that requires citizenship or PR.
  It feeds a monthly newsletter in Portuguese for Brazilians moving to Australia.

> **Status:** v1 running in production since October 2026. Daily collection from Seek, full-text clause
> detection, full ad text for the listings that matter, scoring per profile, application tracking, a
> triage UI and the monthly market snapshot work end to end; the scorer matches the Python prototype on
> 5,388 real listings. Left for v1: importing the prototype history.
> Scope: [docs/visao.md](docs/visao.md) · roadmap: [docs/plano.md](docs/plano.md) (Portuguese).

![Triage: new listings for a profile, best first, with one-key labelling](docs/img/triage.png)

![A listing: the score explained step by step, labelling and application status](docs/img/listing.png)

<sub>Screenshots use fictitious listings and the example profile from
`src/JobRadar.Worker/appsettings.Local.example.json`.</sub>

## Architecture

```
src/
  JobRadar.Domain           pure model: listings, locations, work types (no EF, no HTTP)
  JobRadar.Application      business services + ports (Abstractions/) they depend on
  JobRadar.Infrastructure   Postgres: entity → configuration → mapper → repository, migrations
  JobRadar.Integrations.Seek  Seek search API: resilient HTTP client, translator to the neutral model
  JobRadar.Worker           composition root, daily pipeline and CLI commands
  JobRadar.Web              triage UI (Blazor, server rendering)
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

Triage in the browser at http://localhost:5380:

```bash
dotnet run --project src/JobRadar.Web
```

Each profile gets a queue, best score first, with the same job posted several times shown once.
Mark a listing good or bad, applied or dismissed from the list; the detail page explains the score
rule by rule from the base of 50 and keeps notes and the application history. The site has no
login yet and only listens on localhost.

The worker executable also takes commands:

```bash
dotnet run --project src/JobRadar.Worker -- status seek:81234567 applied
dotnet run --project src/JobRadar.Worker -- label seek:81234567 good "exactly my stack"
dotnet run --project src/JobRadar.Worker -- calibrate
```

`calibrate` reports, per profile, how often a listing you liked outscores one you did not, and
lists where the score disagrees with you, with the rules that weighed most.

### Market snapshot (Raio-X)

Every day the worker also counts open listings per official Seek category and city, and how many
mention terms such as "sponsorship" or "working rights" anywhere in the ad. Each daily snapshot is
checked (failed counts, cut listings, implausible swings) and only good ones feed the monthly
report, written in Portuguese for the newsletter:

```bash
dotnet run --project src/JobRadar.Worker -- market-report 2026-10 raio-x_2026-10.md
```

The report holds aggregates only: counts, shares, salary medians. Never an individual listing.

## Production

It runs on the author's Windows PC as two Windows services (worker and site), with Postgres in
Docker. The worker waits for the database at boot and catches up on a run missed while the
machine was off; the site is reachable on localhost and over Tailscale only. One script publishes,
migrates and installs everything: see [docs/producao.md](docs/producao.md) (Portuguese).

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
anti-bot protection and does not open protected job pages. Ad text, for the listings that score well,
comes from the same data endpoint the source's own site uses; the first refusal stops that stage.
Requests identify the bot in the User-Agent, share one paced budget (a few per second), and back off
on HTTP 429. Ad text stays in the owner's database for personal use.

## License

[MIT](LICENSE)
