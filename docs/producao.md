# Produção — manual de operação

> Documento vivo. Onde roda, como instalar, atualizar, acompanhar e recuperar.
> Decidido em 2026-10-21: roda **no PC do autor (Windows)**; o site é acessível **só localmente e pela
> Tailscale**, sem login no app.

## Como está montado

| Peça | O quê | Onde |
|---|---|---|
| Banco | Postgres 18 no Docker Desktop (`docker-compose.yml`, `restart: unless-stopped`) | `localhost:5432`, volume `pgdata` |
| Worker | serviço do Windows `JobRadarWorker` (automático, início atrasado, reinicia em falha) | `C:\JobRadar\worker` |
| Site | serviço do Windows `JobRadarWeb` | `C:\JobRadar\web` → `http://localhost:5380` e `http://<IP Tailscale>:5380` |
| Migrations | pacote `migrate.exe`, aplicado a cada implantação | `C:\JobRadar\migrate.exe` |
| Versão | commit e hora da última implantação; a anterior, para voltar | `C:\JobRadar\deployed.txt`, `C:\JobRadar\previous` |
| Paridade do score | roda sozinha no começo de cada deploy, com `D:\Desenvolvimento\job-tracker\data\parity\scoring-parity.json`; falhou, o deploy para antes de tocar nos serviços | log do auto-deploy |
| Saúde | `GET http://localhost:5380/health`: 200 com passada nas últimas 26 h e banco de pé; 503 com o motivo | o vigia lê |
| Implantação automática | tarefa `JobRadar Auto-deploy`, a cada 15 min: main com CI verde | `C:\JobRadar\auto-deploy.log` |
| Vigia | tarefa `JobRadar Watchdog`, 2 min após o login e a cada 15 min: inicia serviço parado | `C:\JobRadar\watchdog.log` |

- Conexão com o banco e ambiente (`Production`) ficam nas variáveis do serviço, no registro
  (`HKLM\SYSTEM\CurrentControlSet\Services\<serviço>\Environment`), não em arquivo.
- Perfis reais: `C:\JobRadar\worker\appsettings.Local.json`, copiado de `src/JobRadar.Worker/appsettings.Local.json`.
- O site escuta em `localhost` e no IP da Tailscale (`C:\JobRadar\web\appsettings.Local.json`, gerado pelo
  script). A regra de firewall `JobRadarWebTailscale` só aceita conexões de `100.64.0.0/10`.

## Horário

A passada diária roda às **06:00 de Perth = 19:00 em Brasília** (Perth não tem horário de verão;
Brasília também não, hoje). Se o PC estiver desligado ou dormindo nessa hora, o Worker roda a passada
perdida assim que voltar (`DailySchedule.Missed`). O Docker Desktop só sobe no login: o Worker espera o
banco antes de começar.

A **primeira passada completa** (sem histórico, até 5 páginas por busca) faz até ~1.500 requisições ao
Seek e leva dezenas de minutos. Depois, a janela incremental reduz muito.

## Instalar (uma vez, admin)

Num PowerShell **como administrador**:

```powershell
cd D:\Desenvolvimento\job-radar-au
.\deploy\install.ps1
```

Cria ou atualiza os serviços (início atrasado, reinício em falha, ambiente e conexão no registro), a regra de
firewall da Tailscale, e dá ao usuário que rodou (`-User` para outro) direito de escrita em `C:\JobRadar` e de
consultar, iniciar e parar os dois serviços (uma entrada na ACL de cada serviço: `sc.exe sdshow JobRadarWorker`
mostra). Não publica nem inicia nada. Só precisa rodar de novo se mudar o que ele define: conexão, porta,
serviço novo, outro usuário.

## Atualizar (sem admin)

Com o Docker Desktop aberto e o banco de pé (`docker compose up -d`), num PowerShell comum (o Claude também roda):

```powershell
cd D:\Desenvolvimento\job-radar-au
.\deploy\deploy.ps1
```

Publica o repositório (o checkout ou worktree de onde roda; os perfis vêm do `appsettings.Local.json` do checkout
principal se o worktree não tiver), para os serviços, copia, aplica migrations (conexão lida do registro do
serviço), inicia e espera a página de status responder. Grava o commit implantado em `C:\JobRadar\deployed.txt`.
**Recusa rodar entre 05:55 e 06:45 de Perth**, e também se houve execução gravada em `collection_runs` nos últimos
30 min (passada recuperada fora de hora): cortaria a passada. `-Force` passa por cima.

### Automático (main com CI verde)

Tarefa `JobRadar Auto-deploy` (**a cada 15 min**, com o usuário do autor logado, sem janela): roda
`deploy\auto-deploy.ps1`, que busca `origin/main` e, se o commit não é o do `deployed.txt` e o CI dele no GitHub
passou, avança o checkout (`--ff-only`) e chama o `deploy.ps1`. Basta dar push em main, de qualquer máquina. Não
implanta com o checkout fora da main, com mudança local em arquivo versionado ou com commit local que não está no
`origin/main`; CI falho fica registrado uma vez e o commit é ignorado até main andar. Log em
`C:\JobRadar\auto-deploy.log` (só ação e problema); a saída inteira do último deploy fica em
`C:\JobRadar\auto-deploy-last.txt`, e só vale como implantado se o `deployed.txt` passar a ter o commit. Conferir sem mudar nada: `.\deploy\auto-deploy.ps1 -DryRun`.
Instalar ou mudar o intervalo: `.\deploy\schedule-auto-deploy.ps1 -EveryMinutes 15`. Pausar:
`Disable-ScheduledTask -TaskName 'JobRadar Auto-deploy'`.

**Perfis não viajam:** o `appsettings.Local.json` não está no git. Mudança de perfil se faz no trabalhopc, e só
entra em produção na próxima implantação (`.\deploy\deploy.ps1` à mão, ou o próximo commit em main).

### Voltar a versão anterior

```powershell
.\deploy\deploy.ps1 -Rollback
```

Cada implantação guarda a que estava rodando em `C:\JobRadar\previous`. O `-Rollback` troca as duas (rodar de
novo desfaz), sem publicar nada, e grava em `C:\JobRadar\rollback-skip.txt` o commit desfeito: a implantação
automática não o reinstala; volta a implantar quando main andar ou numa implantação à mão. **Migrations não são
desfeitas**: uma versão anterior a uma migration pode não rodar no schema novo (aí, restaurar o backup).

## Acompanhar

- **Página de status:** `http://localhost:5380/status` (ou pelo IP da Tailscale, de qualquer máquina). A linha
  do `deployed.txt` (commit e hora; a hora diz se a mudança de perfil, que não é commit, já foi implantada), a
  **última passada** com cada busca ou termo que falhou ou parou no teto (com o erro), passadas por dia e os
  retratos do Raio-X com o status de qualidade. A última passada começa na primeira execução que sai mais de
  15 min depois do fim de todas as anteriores (`CollectionPass.Gap`).
- **Logs:** Visualizador de Eventos → Logs do Windows → Aplicativo, fontes `JobRadarWorker` e `JobRadarWeb`.
  Só **avisos e erros** vão para lá (padrão do log de eventos); um log com apenas "Service started" é normal.
  O andamento de cada passada está na página de status e em `collection_runs`.
- **Serviços:** `Get-Service JobRadar*`.
- **Vigia** (desde 2026-10-27): a tarefa `JobRadar Watchdog` (`deploy\watchdog.ps1`) inicia os serviços quando
  estão parados, 2 min depois do login e a cada 15 min, com o usuário do autor e sem janela. Motivo: depois de
  uma queda de energia em 2026-10-25, os dois serviços passaram dos 30 s de partida no boot e ficaram parados
  dois dias (a recuperação do serviço só cobre queda depois de rodar, não falha na partida). O vigia respeita
  `C:\JobRadar\deploying.lock`, que o `deploy.ps1` mantém enquanto para os serviços de propósito; um deploy
  que falha deixa a trava, e o vigia só volta a agir depois de 1 h. Escreve em `watchdog.log` só quando age
  ou falha. Recriar: `.\deploy\schedule-watchdog.ps1`. A publicação é ReadyToRun (win-x64): metade do tempo
  de partida (1,3 s contra 2,6 s num comando curto), o que ajuda no boot.
  Antes do login nada roda: o banco vive no Docker Desktop, na sessão do autor.

## Backup

```powershell
.\deploy\backup.ps1
```

Gera `%USERPROFILE%\Backups\JobRadar\jobradar_<data_hora>.dump`, mantém os 30 mais recentes e registra
cada execução (ok ou FAILED, com o erro) em `backup.log` na mesma pasta. O histórico do Raio-X não se
recoleta.

**Agendado** (2026-10-21): tarefa `JobRadar Backup` no Agendador, **todo dia às 23:00**, com o usuário do autor
e só com ele logado (o banco vive no Docker Desktop, na sessão dele); se o PC estiver desligado, roda assim
que puder. Recriar ou mudar o horário: `.\deploy\schedule-backup.ps1 -At 23:00`. Leva ~20 s.

Restaurar: `docker compose exec -T postgres pg_restore -U jobradar -d jobradar --clean < <arquivo>`.

## Parar ou remover

Parar e iniciar não pedem admin depois do `install.ps1`; remover pede:

```powershell
Stop-Service JobRadarWorker, JobRadarWeb
sc.exe delete JobRadarWorker; sc.exe delete JobRadarWeb
Remove-NetFirewallRule -Name JobRadarWebTailscale
```

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

Em VM Linux o empacotamento muda: imagens Docker do Worker e do site + `docker compose` (Postgres, Worker,
site) + Tailscale, no lugar dos serviços do Windows. Domínio, testes e configuração não mudam. Leve o banco
com `deploy/backup.ps1` → `pg_restore`.

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
