# Produção — manual de operação

> Documento vivo. Onde roda, como instalar, atualizar, acompanhar e recuperar.
> Decidido em 2026-10-21: roda **no PC do autor**; o site é acessível **só localmente e pela Tailscale**, sem
> login no app. Desde 2026-10-30 tudo roda em containers (`docker compose`, perfil `prod`): o mesmo em qualquer
> SO com Docker, sem serviço nem registro do Windows.

## Como está montado

Tudo é `docker compose` com o projeto `job-radar-au` e o perfil `prod` (`docker-compose.yml`, imagens do
`Dockerfile`). Os scripts de `deploy/` são PowerShell 7 e só chamam `docker compose`, `git` e `dotnet`: rodam em
Windows, Linux e macOS. O que é da máquina fica no `.env` ao lado do `docker-compose.yml` (fora do git; modelo em
`deploy/prod.env.example`).

| Peça | O quê | Onde |
|---|---|---|
| Banco | container `postgres` (Postgres 18, `restart: unless-stopped`) | volume `job-radar-au_pgdata`, só `127.0.0.1:5432` |
| Migrations | container `migrate` (bundle do EF), roda e sai antes dos outros a cada subida | imagem `jobradar-migrate:<commit>` |
| Worker | container `worker` | imagem `jobradar-worker:<commit>` |
| Site | container `web`, publicado em `127.0.0.1:5380` e no IP da Tailscale (`JOBRADAR_TAILSCALE_IP`) | imagem `jobradar-web:<commit>` |
| Perfis reais | o `appsettings.Local.json` apontado por `JOBRADAR_PROFILES`, montado só para leitura no worker e no site | fora do git |
| Estado | `deployed.txt`, tag anterior, travas e logs dos scripts | pasta `JOBRADAR_STATE` (no PC: `C:\JobRadar`) |
| Paridade do score | roda no começo de cada deploy com o arquivo de `JOBRADAR_PARITY_FILE`; falhou, o deploy para antes de tocar na produção | log do auto-deploy |
| Saúde | `GET http://localhost:5380/health`: 200 com passada nas últimas 26 h e banco de pé; 503 com o motivo | o vigia lê |
| Implantação automática | `deploy/auto-deploy.ps1` a cada 15 min pelo agendador do host: main com CI verde | `auto-deploy.log` |
| Vigia | `deploy/watchdog.ps1` a cada 15 min: sobe a pilha se o worker ou o site não estão rodando | `watchdog.log` |

- Conexão com o banco e ambiente (`Production`) estão no `docker-compose.yml` (a senha é a local de sempre: o banco
  só escuta na máquina).
- O site responde só aos nomes de `JOBRADAR_ALLOWED_HOSTS` (localhost, o IP e o nome MagicDNS da Tailscale) e nunca
  escuta em todas as interfaces.

## Horário

A passada diária roda às **06:00 de Perth = 19:00 em Brasília** (Perth não tem horário de verão;
Brasília também não, hoje). Se a máquina estiver desligada ou dormindo nessa hora, o Worker roda a passada
perdida assim que voltar (`DailySchedule.Missed`). O Worker espera o banco antes de começar.

**Sem login:** no PC do autor (Windows), o Docker Desktop só sobe com a sessão dele, e a pilha com ele: antes do
login nada roda (decisão de 2026-10-30, opção C do [#29]). Num host Linux o Docker é serviço do sistema e a pilha
sobe no boot.

## Instalar (uma vez)

Precisa de Docker (com Compose v2), PowerShell 7, git e o SDK .NET (só para a paridade). Na raiz do checkout:

```powershell
Copy-Item deploy/prod.env.example .env   # e preencha: perfis, pasta de estado, IP da Tailscale
pwsh deploy/deploy.ps1
```

No PC do autor o `.env` aponta `JOBRADAR_PROFILES` para `src/JobRadar.Worker/appsettings.Local.json` do checkout
principal, `JOBRADAR_STATE` para `C:\JobRadar`, `JOBRADAR_PARITY_FILE` para o arquivo da paridade e
`JOBRADAR_TIMEZONE` para `America/Sao_Paulo` (o fuso em que o site mostra os horários; sem ele, UTC).

Agendar os scripts (o agendador é do host):
- **Windows:** `pwsh deploy/schedule-auto-deploy.ps1`, `pwsh deploy/schedule-watchdog.ps1`,
  `pwsh deploy/schedule-backup.ps1` (Agendador de Tarefas, com o usuário logado, sem janela).
- **Linux:** no `crontab` do usuário que roda o Docker:
  ```
  */15 * * * * cd /srv/job-radar && pwsh deploy/auto-deploy.ps1
  */15 * * * * cd /srv/job-radar && pwsh deploy/watchdog.ps1
  0 23 * * *   cd /srv/job-radar && pwsh deploy/backup.ps1
  ```

## Atualizar

```powershell
pwsh deploy/deploy.ps1
```

Roda a paridade, constrói as três imagens com a tag do commit (`docker compose build`), grava a tag no `.env` e
sobe a pilha nela: o `migrate` aplica as migrations e sai, e o worker e o site são recriados. Espera a página de
status e grava o commit em `deployed.txt` (que o site mostra na `/status`). **Recusa rodar entre 05:55 e 06:45 de
Perth**, e também se houve execução gravada em `collection_runs` nos últimos 30 min (passada recuperada fora de
hora): recriar o worker cortaria a passada. `-Force` passa por cima. Guarda as imagens desta implantação e da
anterior; apaga as mais velhas.

### Automático (main com CI verde)

`deploy/auto-deploy.ps1`, a cada 15 min: busca `origin/main` e, se o commit não é o do `deployed.txt` e o CI dele
no GitHub passou, avança o checkout (`--ff-only`) e chama o `deploy.ps1`. Basta dar push em main, de qualquer
máquina. Não implanta com o checkout fora da main, com mudança local em arquivo versionado ou com commit local que
não está no `origin/main`; CI falho fica registrado uma vez e o commit é ignorado até main andar. Log em
`auto-deploy.log` (só ação e problema); a saída inteira do último deploy em `auto-deploy-last.txt`. Conferir sem
mudar nada: `pwsh deploy/auto-deploy.ps1 -DryRun`.

**Perfis:** o `appsettings.Local.json` não está no git e é montado direto nos containers: uma mudança vale quando o
worker e o site reiniciam (o próximo deploy, ou `docker compose -p job-radar-au --profile prod restart worker web`).
Além dos perfis, ele guarda quem pode se candidatar em cada mercado (`Matching:Markets:<mercado>:Eligibility`) e os
termos do Raio-X (`Market:Terms`): sem o bloco do AU, worker e site não sobem e a paridade do deploy falha. Para ver o
bloco em uso: `dotnet run --project src/JobRadar.Worker -- eligibility AU`. Os casos com texto real que conferem o
bloco ficam em `scoring-cases.Local.json`, ao lado; o deploy roda os dois com a paridade.

### Voltar a versão anterior

```powershell
pwsh deploy/deploy.ps1 -Rollback
```

Sobe a pilha na tag anterior (`previous-tag.txt`), sem construir nada; rodar de novo desfaz. Grava em
`rollback-skip.txt` o commit desfeito: a implantação automática não o reinstala até main andar. **Migrations não
são desfeitas**: uma versão anterior a uma migration pode não rodar no schema novo (aí, restaurar o backup).

## Acompanhar

- **Página de status:** `http://localhost:5380/status` (ou pelo IP da Tailscale). A linha do `deployed.txt`, a
  **última passada** com cada busca ou termo que falhou ou parou no teto, passadas por dia e os retratos do Raio-X.
- **Logs:** `docker compose -p job-radar-au logs -f worker web` (o andamento de cada passada também está na página
  de status e em `collection_runs`).
- **Containers:** `docker compose -p job-radar-au --profile prod ps`.
- **Vigia:** os containers reiniciam sozinhos quando caem (`restart: unless-stopped`); o `watchdog.ps1` cobre o
  resto: sobe a pilha quando o worker ou o site não estão rodando (Docker que voltou sem eles, deploy que morreu no
  meio) e reinicia o worker quando o `/health` diz que o banco responde mas nenhuma passada começou em 26 h (no
  máximo a cada 6 h). Respeita `deploying.lock`, que o `deploy.ps1` mantém enquanto troca a pilha.

## Backup

```powershell
pwsh deploy/backup.ps1
```

Gera `~/Backups/JobRadar/jobradar_<data_hora>.dump` (`pg_dump` dentro do container, copiado para fora), mantém os
30 mais recentes e registra cada execução em `backup.log` na mesma pasta. O histórico do Raio-X não se recoleta.
Agendado todo dia às 23:00 (acima). Restaurar:

```powershell
docker compose -p job-radar-au cp <arquivo> postgres:/tmp/restore.dump
docker compose -p job-radar-au exec postgres pg_restore -U jobradar -d jobradar --clean /tmp/restore.dump
```

## Parar

```powershell
docker compose -p job-radar-au --profile prod stop worker web   # o banco continua de pé
```

Os serviços do Windows de antes de 2026-10-30 (`JobRadarWorker`, `JobRadarWeb`, criados pelo `install.ps1`) ficam
desativados por duas semanas, como volta de emergência; depois, removê-los (admin):
`sc.exe delete JobRadarWorker; sc.exe delete JobRadarWeb`.

## Migrar para uma VM (depois)

Decidido em 2026-10-21: fica no PC por enquanto. O ganho de uma VM é não depender do PC ligado e não
deixar lacunas na série do Raio-X (dia perdido = buraco na newsletter). Pesquisa de outubro de 2026:

| Opção | Resumo |
|---|---|
| Google Cloud e2-micro (Always Free) | 1 GB RAM, 30 GB disco, só regiões dos EUA; o IPv4 público custa ~US$ 3,65/mês e é necessário para acessar o Seek |
| Oracle Ampere A1 | cota cortada para 2 OCPU / 12 GB em jun/2026; o autor já usa o limite (Sirene). Não dividir VM com a Sirene |
| AWS | contas novas: créditos de US$ 100–200 por 6 meses; não é permanente |
| Postgres gerenciado grátis (Neon, Supabase) | ~0,5 GB: o banco passa disso em meses |
| VPS paga pequena (Hetzner e similares) | ~€ 4/mês, mais RAM, menos manutenção |

Desde 2026-10-30 o empacotamento já é o de uma VM: imagens + `docker compose`. Numa VM Linux basta Docker,
PowerShell 7, git, o `.env` e o agendador do host (acima); leve o banco com `deploy/backup.ps1` → `pg_restore`.
Num host Linux o sistema sobe sem login (o Docker é serviço do sistema), o que resolve o [#29] de vez.

[#29]: https://github.com/DaviLuisSantos/job-radar/issues/29

## Importar o histórico do protótipo (etapa 9)

O banco de produção está **neste PC**: traga o `jobs.db` da outra máquina para cá e importe aqui.

1. Na outra máquina, **desative a tarefa agendada** do protótipo (para o histórico não continuar crescendo lá)
   e copie `job-tracker\data\jobs.db` para este PC (ex.: `C:\Users\davil\Downloads\jobs.db`).
   **Feito em 2026-10-23:** tarefa desativada e cópia final (backup do SQLite, `integrity_check` ok) em
   `D:\Projetos\job-tracker\data\export\jobs-2026-10-23.db` na outra máquina (CASAPC): 14.607 vagas, 5 rótulos,
   150 `market_counts`, 10.244.096 bytes, SHA-256 `818A04C5…7281F53`. Confira o hash depois de copiar
   (`Get-FileHash`).
2. Veja o que há no arquivo, sem gravar nada:
   ```powershell
   cd D:\Desenvolvimento\job-radar-au
   dotnet run --project src\JobRadar.Worker -- import-prototype C:\Users\davil\Downloads\jobs.db --dry-run
   ```
3. Faça o backup antes (`.\deploy\backup.ps1`) e importe (mesmo comando, sem `--dry-run`). O `dotnet run` usa
   as configurações de desenvolvimento, que apontam para o mesmo banco local.
4. Os scores das vagas importadas saem na próxima passada diária, **desde que os serviços estejam na versão com o
   importador** (commit `e735126` ou depois): se instalou antes, rode `.\deploy\deploy.ps1` de novo.

Pode repetir: o que já está aqui vence (candidatura, rótulo, dia do Raio-X) e as vagas só juntam datas.
O que vira o quê: vagas (com a data em que o protótipo as viu primeiro e o payload = a linha do protótipo),
perfis (`profiles` → chave `profile: <nome>`, pontuada pelo perfil), termos, rótulos, status (`rejected` com
candidatura → `Declined`, sem → `Dismissed`), notas, retratos do Raio-X com contagens e o histórico de vagas.
Os dias do Raio-X são renomeados para a data de Perth em que o retrato foi tirado (`market_runs.ran_at`): o
protótipo usava a data do Brasil, e à noite lá já é a manhã seguinte em Perth. O dry-run lista os dias já assim.

## Desligar o protótipo Python

Neste PC não há tarefa agendada do protótipo. Ele rodava em **outra máquina do autor** (CASAPC, tarefa
`JobTracker Daily` chamando `D:\Projetos\job-tracker\run.ps1 --daily` às 08:00 de Brasília). **Desativada em
2026-10-23** (última execução 18:35, resultado 0); não foi apagada: `Enable-ScheduledTask -TaskName
'JobTracker Daily'` lá a reativa.
