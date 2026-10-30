---
name: operar-worker
description: Operar o JobRadar: comandos do worker (status, label, calibrate, market-report, copy-database, requirement...), rodar uma passada local, configuração (appsettings.Local, variáveis de ambiente), produção em docker compose, deploy e telemetria. Use ao rodar ou mudar um comando do worker, mexer em Program.cs ou configuração, em deploy/ ou docker-compose, ou ao precisar de dados para testar.
---

# Operar o worker e a produção

## Comandos do worker

**Comandos** (o mesmo executável; sem comando, roda o pipeline): `status <id> <status>`, `notes <id> <texto>`,
`label <id> good|bad|clear [nota]`, `calibrate [--rescore [--assume item=estado,years:perfil=n,...]]`, `market`, `market-report [YYYY-MM] [arquivo]`,
`import-prototype <jobs.db> [--dry-run]` (histórico do protótipo; ver `docs/producao.md`), `tech-count [AU|BR] [YYYY-MM]`
(tecnologias pedidas nos anúncios inteiros do mês; padrão BR), `copy-database Postgres|SqlServer <conexão>`
(todas as linhas para outro banco vazio e migrado; abaixo dos repositórios, em `DatabaseCopy`), `requirements`,
`requirement <item> have|obtainable|missing|out-of-reach|default`, `experience <perfil> <anos>|default`,
`eligibility <mercado>` (o bloco de elegibilidade em uso, em JSON de configuração),
`import-candidate <candidate.json> [--dry-run]`
(estado do candidato em cada item do `RequirementCatalog`, em `candidate_requirements`; item sem estado fica com o
padrão do tipo, que é o peso de antes; mudar um estado repontua na hora as vagas que podem pedir o item; no site,
`/me` faz isso em linguagem simples e `/requirements` mostra o impacto de ter cada item). `<id>` = `fonte:id` (`seek:81234567`). Saída 0 = ok,
1 = vaga nunca coletada, 2 = argumentos inválidos. Calibragem: ver a skill `regras-do-score`.

## Passada local

Para rodar uma passada local: `docker compose up -d`, `dotnet ef database update --project src/JobRadar.Infrastructure.Migrations.Postgres`,
e o Worker com `Collection__RunOnStartup=true` (e `Seek__MaxPages=1` para uma prova leve).

## Produção

- Esta máquina (TRABALHOPC) **é** a produção: o Postgres do `docker compose` é o banco de produção, e o auto-deploy
  implanta a partir deste checkout (recusa com mudança local em arquivo versionado). Trabalhe num worktree. Para olhar o
  site com dados, rode-o contra uma cópia (`copy-database` para o SQL Server local), nunca grave no banco de produção à
  mão. Argumento de um **comando** do worker não é configuração (`collect --ConnectionStrings:...` é ignorado): use
  variável de ambiente. Para testar a pilha de produção ao lado, use outro projeto e outras portas
  (`docker compose -p jobradar-test --env-file <arquivo>` com `JOBRADAR_DB_PORT` e `JOBRADAR_PORT`).
- Produção é `docker compose --profile prod` (desde 2026-10-30), igual em qualquer SO; nada de serviço, registro ou
  firewall do Windows nos scripts, que são PowerShell 7. O que é da máquina fica no `.env` (`deploy/prod.env.example`).
  O site escuta em `127.0.0.1:5380` e no IP da Tailscale, nunca em todas as interfaces, e só responde aos nomes de
  `JOBRADAR_ALLOWED_HOSTS`. A 5080 é de outro serviço local do autor.
- O worker pode subir antes do banco responder (boot, Docker voltando): o `DailyPipelineJob` espera o banco
  (`IStorageHealth`) e recupera a passada perdida. Não remova a espera achando que é lentidão.

## Configuração

- `appsettings.Local.json` é adicionado depois das fontes padrão; por isso o `Program.cs` reaplica variáveis
  de ambiente e linha de comando em seguida, para elas continuarem vencendo.
- `ContentRootPath = AppContext.BaseDirectory` no `Program.cs`: sem isso, o `appsettings` é procurado no
  diretório corrente.
- Os argumentos de um comando não entram na configuração do host (`Program.cs`): o texto livre de `notes`
  poderia ser lido como chave=valor.
- Padrões de opções ficam na classe (`SeekOptions`), não no `appsettings.json`.
- `Market` na configuração: cada lista presente **substitui** o padrão do código (`MarketDefaults`), não soma.
- A máquina do autor tem uma fonte NuGet extra; `nuget.config` limpa e usa só nuget.org.

## Telemetria

Telemetria (`JobRadarTelemetry`): só sai com `Telemetry:OtlpEndpoint` (local: Aspire, `--profile telemetry`). Tag de
métrica ou trace leva fonte, país, etapa, nunca título, empresa ou dado do candidato. O `Meter` é estático e do
processo: teste que escuta métricas filtra pela fonte que ele mesmo criou.
