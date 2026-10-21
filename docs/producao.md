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

## Instalar ou atualizar

Com o Docker Desktop aberto e o banco de pé (`docker compose up -d`), num PowerShell **como administrador**:

```powershell
cd D:\Desenvolvimento\job-radar-au
.\deploy\deploy.ps1
```

O script publica, para os serviços, copia, aplica migrations, cria ou atualiza os serviços, libera o
firewall para a Tailscale e inicia tudo. Rodar de novo = implantar a versão atual do repositório.

## Acompanhar

- **Página de status:** `http://localhost:5380/status` (ou pelo IP da Tailscale). Passadas por dia, falhas,
  buscas incompletas e os retratos do Raio-X com o status de qualidade.
- **Logs:** Visualizador de Eventos → Logs do Windows → Aplicativo, fontes `JobRadarWorker` e `JobRadarWeb`.
  Só **avisos e erros** vão para lá (padrão do log de eventos); um log com apenas "Service started" é normal.
  O andamento de cada passada está na página de status e em `collection_runs`.
- **Serviços:** `Get-Service JobRadar*`.

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
2. Veja o que há no arquivo, sem gravar nada:
   ```powershell
   cd D:\Desenvolvimento\job-radar-au
   dotnet run --project src\JobRadar.Worker -- import-prototype C:\Users\davil\Downloads\jobs.db --dry-run
   ```
3. Faça o backup antes (`.\deploy\backup.ps1`) e importe (mesmo comando, sem `--dry-run`). O `dotnet run` usa
   as configurações de desenvolvimento, que apontam para o mesmo banco local.
4. Os scores das vagas importadas saem na próxima passada diária, **desde que os serviços estejam na versão com o`n   importador** (commit `e735126` ou depois): se instalou antes, rode `.\deploy\deploy.ps1` de novo.

Pode repetir: o que já está aqui vence (candidatura, rótulo, dia do Raio-X) e as vagas só juntam datas.
O que vira o quê: vagas (com a data em que o protótipo as viu primeiro e o payload = a linha do protótipo),
perfis (`profiles` → chave `profile: <nome>`, pontuada pelo perfil), termos, rótulos, status (`rejected` com
candidatura → `Declined`, sem → `Dismissed`), notas, retratos do Raio-X com contagens e o histórico de vagas.

## Desligar o protótipo Python

Neste PC não há tarefa agendada do protótipo. Ele rodava em **outra máquina do autor** (Agendador de
Tarefas chamando `job-tracker\run.ps1 --daily`): desative a tarefa lá depois de importar o histórico
(etapa 9).
