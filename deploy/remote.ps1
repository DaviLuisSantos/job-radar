<#
.SYNOPSIS
  Operates the production homelab from the development machine, over SSH (key login, no prompts).

.EXAMPLE
  pwsh deploy/remote.ps1 status
  pwsh deploy/remote.ps1 logs worker 200
  pwsh deploy/remote.ps1 config-get
  pwsh deploy/remote.ps1 config-get Matching:Profiles
  pwsh deploy/remote.ps1 config-set Matching:Profiles ./profiles.json
  pwsh deploy/remote.ps1 restart
  pwsh deploy/remote.ps1 auto-deploy-dry-run
  pwsh deploy/remote.ps1 run 'df -h /srv'

  Host: $env:JOBRADAR_HOST (default davi@192.168.1.44). Config API: $env:JOBRADAR_URL (default http://100.97.218.37:5380). Paths on the host follow deploy/setup-debian.sh.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('status', 'logs', 'config-get', 'config-set', 'restart', 'auto-deploy-dry-run', 'run')]
    [string] $Action,
    [Parameter(Position = 1, ValueFromRemainingArguments)]
    [string[]] $Rest
)

$ErrorActionPreference = 'Stop'
$hostName = if ($env:JOBRADAR_HOST) { $env:JOBRADAR_HOST } else { 'davi@192.168.1.44' }
$app = '/srv/job-radar'
$baseUrl = if ($env:JOBRADAR_URL) { $env:JOBRADAR_URL.TrimEnd('/') } else { 'http://100.97.218.37:5380' }
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
    'config-get' {
        $uri = "$baseUrl/api/config"
        if ($Rest.Count -ge 1) { $uri += '/' + [uri]::EscapeDataString($Rest[0]) }
        (Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 15).Content
    }
    'config-set' {
        if ($Rest.Count -lt 2) { throw 'Usage: config-set <key> <file>' }
        $body = Get-Content -Raw $Rest[1]
        $null = $body | ConvertFrom-Json -ErrorAction Stop # refuses invalid JSON
        $uri = "$baseUrl/api/config/" + [uri]::EscapeDataString($Rest[0])
        $null = Invoke-WebRequest -Uri $uri -Method Put -Body $body -ContentType 'application/json' -UseBasicParsing -TimeoutSec 30
        Write-Host "Saved $($Rest[0]); the site and worker restart on their own within about a minute."
    }
    'restart' { Invoke-Remote "cd $app && $compose restart worker web" }
    'auto-deploy-dry-run' { Invoke-Remote "cd $app && pwsh -NoProfile -File deploy/auto-deploy.ps1 -DryRun" }
    'run' {
        if ($Rest.Count -lt 1) { throw 'Usage: run <command>' }
        Invoke-Remote ($Rest -join ' ')
    }
}
