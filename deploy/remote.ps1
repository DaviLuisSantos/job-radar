<#
.SYNOPSIS
  Operates the production homelab from the development machine, over SSH (key login, no prompts).

.EXAMPLE
  pwsh deploy/remote.ps1 status
  pwsh deploy/remote.ps1 logs worker 200
  pwsh deploy/remote.ps1 config-pull ./appsettings.Local.json
  pwsh deploy/remote.ps1 config-push ./appsettings.Local.json
  pwsh deploy/remote.ps1 restart
  pwsh deploy/remote.ps1 auto-deploy-dry-run
  pwsh deploy/remote.ps1 run 'df -h /srv'

  Host: $env:JOBRADAR_HOST (default davi@192.168.1.44). Paths on the host follow deploy/setup-debian.sh.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('status', 'logs', 'config-pull', 'config-push', 'restart', 'auto-deploy-dry-run', 'run')]
    [string] $Action,
    [Parameter(Position = 1, ValueFromRemainingArguments)]
    [string[]] $Rest
)

$ErrorActionPreference = 'Stop'
$hostName = if ($env:JOBRADAR_HOST) { $env:JOBRADAR_HOST } else { 'davi@192.168.1.44' }
$app = '/srv/job-radar'
$config = '/srv/jobradar/appsettings.Local.json'
$compose = 'docker compose -p job-radar-au --profile prod'
$sshOpts = @('-o', 'BatchMode=yes', '-o', 'ConnectTimeout=8')

function Invoke-Remote([string] $Command) {
    & ssh @sshOpts $hostName $Command
    if ($LASTEXITCODE -ne 0) { throw "ssh failed ($LASTEXITCODE): $Command" }
}

switch ($Action) {
    'status' {
        Invoke-Remote "cd $app && git log --oneline -1 && cat /srv/jobradar/state/deployed.txt 2>/dev/null; $compose ps --format '{{.Service}} {{.Status}}'; curl -s -o /dev/null -w 'health %{http_code}\n' http://localhost:5380/health"
    }
    'logs' {
        $service = if ($Rest.Count -ge 1) { $Rest[0] } else { 'worker' }
        $lines = if ($Rest.Count -ge 2) { [int]$Rest[1] } else { 100 }
        if ($service -notmatch '^(worker|web|postgres|migrate)$') { throw "Unknown service: $service" }
        Invoke-Remote "$compose logs --tail $lines $service"
    }
    'config-pull' {
        $dest = if ($Rest.Count -ge 1) { $Rest[0] } else { './appsettings.Local.json' }
        & scp @sshOpts "${hostName}:$config" $dest
        if ($LASTEXITCODE -ne 0) { throw 'scp failed' }
        Write-Host "Saved to $dest"
    }
    'config-push' {
        if ($Rest.Count -lt 1) { throw 'Usage: config-push <file>' }
        $file = $Rest[0]
        $null = Get-Content -Raw $file | ConvertFrom-Json -ErrorAction Stop # refuses invalid JSON
        $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
        Invoke-Remote "cp $config $config.bak.$stamp"
        & scp @sshOpts $file "${hostName}:$config"
        if ($LASTEXITCODE -ne 0) { throw 'scp failed' }
        Invoke-Remote "chmod 644 $config && cd $app && $compose restart worker web"
        Write-Host "Config applied; previous one kept as $config.bak.$stamp"
    }
    'restart' { Invoke-Remote "cd $app && $compose restart worker web" }
    'auto-deploy-dry-run' { Invoke-Remote "cd $app && pwsh -NoProfile -File deploy/auto-deploy.ps1 -DryRun" }
    'run' {
        if ($Rest.Count -lt 1) { throw 'Usage: run <command>' }
        Invoke-Remote ($Rest -join ' ')
    }
}
