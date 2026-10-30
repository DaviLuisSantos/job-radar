# Shared by the deploy scripts: the machine's settings (.env next to docker-compose.yml) and docker compose with the
# production project. PowerShell 7 on any OS; nothing here knows Windows.

$script:Repo = Split-Path $PSScriptRoot -Parent
# Fixed project name: the database volume (job-radar-au_pgdata) is named after it, whatever the folder is called.
# JOBRADAR_COMPOSE_PROJECT only to try the scripts on a second stack beside production.
$script:Project = if ($env:JOBRADAR_COMPOSE_PROJECT) { $env:JOBRADAR_COMPOSE_PROJECT } else { 'job-radar-au' }

# In a worktree, the machine's files (.env, the profiles) live in the main checkout.
function Get-MainCheckout {
    $common = & git -C $script:Repo rev-parse --path-format=absolute --git-common-dir 2>$null
    if ($LASTEXITCODE -or -not $common) { return $script:Repo }
    return Split-Path $common -Parent
}

function Get-EnvFile {
    foreach ($dir in $script:Repo, (Get-MainCheckout)) {
        $file = Join-Path $dir '.env'
        if (Test-Path $file) { return $file }
    }
    throw "No .env next to docker-compose.yml: copy deploy/prod.env.example to $(Join-Path $script:Repo '.env') and fill it in."
}

function Read-DotEnv([string] $File = (Get-EnvFile)) {
    $values = [ordered]@{}
    foreach ($line in Get-Content $File) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=(.*)$') { $values[$Matches[1]] = $Matches[2].Trim() }
    }
    return $values
}

# Sets keys in .env, keeping its comments and order.
function Set-DotEnv([hashtable] $Values, [string] $File = (Get-EnvFile)) {
    $lines = [Collections.Generic.List[string]](Get-Content $File)
    foreach ($key in $Values.Keys) {
        $index = $lines.FindIndex({ param($l) $l -match "^\s*$key\s*=" })
        if ($index -ge 0) { $lines[$index] = "$key=$($Values[$key])" } else { $lines.Add("$key=$($Values[$key])") }
    }
    Set-Content $File $lines -Encoding utf8NoBOM
}

function Get-StateDir {
    $state = (Read-DotEnv).JOBRADAR_STATE
    if (-not $state) { throw 'JOBRADAR_STATE is not set in .env.' }
    New-Item -ItemType Directory -Force $state | Out-Null
    return $state
}

# docker compose on the production project, with the .env of this machine. Throws when it fails.
function Invoke-Compose {
    $file = Join-Path $script:Repo 'docker-compose.yml'
    & docker compose -p $script:Project -f $file --env-file (Get-EnvFile) --profile prod @args
    if ($LASTEXITCODE) { throw "docker compose $($args -join ' ') failed (exit $LASTEXITCODE)." }
}

function Get-SitePort { $port = (Read-DotEnv).JOBRADAR_PORT; if ($port) { [int]$port } else { 5380 } }

# The site's /status answers once the stack is up.
function Wait-Site([int] $Seconds = 120) {
    $status = "http://localhost:$(Get-SitePort)/status"
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try { if ((Invoke-WebRequest $status -TimeoutSec 5 -SkipHttpErrorCheck).StatusCode -eq 200) { return } } catch { }
        Start-Sleep -Seconds 3
    }
    throw "The status page did not answer at $status. See: docker compose -p $script:Project logs web worker"
}
