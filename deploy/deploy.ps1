#Requires -Version 7
<#
.SYNOPSIS
    Builds the current repository into images and puts them in production with docker compose. Any OS with Docker.

.DESCRIPTION
    Needs docker-compose.yml's .env filled in once (deploy/prod.env.example).
    1. Before anything else, when -ParityFile exists, runs the scoring parity test against it: a change that moves
       the scores of the real listings stops the deploy before production is touched.
    2. Builds the worker, site and migration images, tagged with the short commit.
    3. Brings the stack up on that tag: the migrations run first (the "migrate" service), then the worker and the
       site. Waits for the status page and records what was deployed in the state folder (deployed.txt), which the
       site shows on /status.
    The tag it replaces is kept (previous-tag.txt). -Rollback goes back to it, without building: run it again to
    undo. A rollback does not undo migrations, and it tells auto-deploy.ps1 to leave the rolled-back commit alone
    (rollback-skip.txt) until a newer one reaches main.
    Refuses to run during the daily pass, or while runs are still being recorded (a pass recovered at another hour),
    unless -Force: recreating the worker would cut the pass short.
    While the stack is replaced on purpose it holds deploying.lock in the state folder, so watchdog.ps1 leaves it be.
    See docs/producao.md.
#>
param(
    # Collection:DailyAt in Australia/Perth; the pass takes ~15 min, so the window below is generous.
    [timespan] $PassAt = '06:00',
    [switch] $Force,
    [switch] $Rollback,
    # Real listings exported from the prototype; only on the author's machine, never in the repository.
    [string] $ParityFile = $env:JOBRADAR_PARITY_FILE
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$state = Get-StateDir
$record = Join-Path $state 'deployed.txt'
$previousTag = Join-Path $state 'previous-tag.txt'
$skip = Join-Path $state 'rollback-skip.txt'
$lock = Join-Path $state 'deploying.lock'
if (-not $ParityFile) { $ParityFile = (Read-DotEnv).JOBRADAR_PARITY_FILE }

$perth = [TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow, 'Australia/Perth').TimeOfDay
if (-not $Force -and $perth -ge $PassAt.Subtract([timespan]'00:05') -and $perth -le $PassAt.Add([timespan]'00:45')) {
    throw "It is $($perth.ToString('hh\:mm')) in Perth: the daily pass ($PassAt) may be running. Wait, or use -Force."
}
if (-not $Force) {
    # A pass recovered after a restart runs at any hour. The steps after the last run (descriptions, scores,
    # snapshot) take ~15 min, so 30 min of quiet means it is over.
    $recent = $null
    try {
        $recent = Invoke-Compose exec -T postgres psql -U jobradar -d jobradar -Atc `
            "select count(*) from collection_runs where started_at > now() - interval '30 minutes'" 2>$null
    }
    catch { }
    if ($null -eq $recent) { Write-Warning 'Could not ask the database whether a pass is running.' }
    elseif ([int]$recent -gt 0) { throw "$recent runs recorded in the last 30 minutes: a pass may be running. Wait, or use -Force." }
}

function Use-Tag([string] $Tag, [string] $Commit) {
    Set-DotEnv @{ JOBRADAR_TAG = $Tag; JOBRADAR_COMMIT = $Commit }
    New-Item $lock -ItemType File -Force | Out-Null
    try {
        Write-Host "== Starting $Tag" -ForegroundColor Cyan
        Invoke-Compose up -d --remove-orphans
        Wait-Site
    }
    finally { Remove-Item $lock -Force -ErrorAction SilentlyContinue }
    Invoke-Compose ps
}

if ($Rollback) {
    $back = Get-Content $previousTag -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $back) { throw "Nothing to roll back to: $previousTag is filled by the next deploy." }
    $current = (Read-DotEnv).JOBRADAR_TAG
    Write-Host "== Rolling back from $current to $back" -ForegroundColor Cyan
    Write-Warning 'Migrations are not undone: an older build may not run on a newer schema.'
    Use-Tag $back $back
    $current | Set-Content $previousTag -Encoding utf8NoBOM
    "$back (rolled back from $current on $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz'))" | Set-Content $record -Encoding utf8NoBOM
    $current | Set-Content $skip -Encoding utf8NoBOM
    Write-Host "Rolled back. auto-deploy.ps1 skips $current until main moves past it." -ForegroundColor Green
    return
}

$commit = (& git -C $Repo rev-parse HEAD).Trim()
$tag = $commit.Substring(0, 7)
$dirty = [bool](& git -C $Repo status --porcelain --untracked-files=no)
if ($dirty) { Write-Warning 'The working tree has uncommitted changes: they go to production too.' }

if ($ParityFile -and (Test-Path $ParityFile)) {
    Write-Host '== Scoring parity' -ForegroundColor Cyan
    $env:JOBRADAR_PARITY_FILE = $ParityFile
    # The profiles production mounts hold each market's eligibility rules, which the parity scores with.
    $profiles = (Read-DotEnv).JOBRADAR_PROFILES
    if ($profiles) { $env:JOBRADAR_LOCAL_SETTINGS = $profiles }
    try {
        # No --nologo: the testing platform hands it to the test app, which then runs zero tests and fails.
        $parity = dotnet test --project (Join-Path $Repo 'tests/JobRadar.Domain.Tests') 2>&1
        if ($LASTEXITCODE) {
            $parity | Select-Object -Last 30 | Write-Host
            throw 'Scoring parity failed: the real listings would score differently. Not deploying.'
        }
    }
    finally { Remove-Item Env:JOBRADAR_PARITY_FILE, Env:JOBRADAR_LOCAL_SETTINGS -ErrorAction SilentlyContinue }
}
else { Write-Warning 'No parity file (JOBRADAR_PARITY_FILE): deploying without the parity check.' }

Write-Host "== Building $tag" -ForegroundColor Cyan
$env:JOBRADAR_TAG = $tag
$env:JOBRADAR_COMMIT = $commit
try { Invoke-Compose build }
finally { Remove-Item Env:JOBRADAR_TAG, Env:JOBRADAR_COMMIT -ErrorAction SilentlyContinue }

$running = (Read-DotEnv).JOBRADAR_TAG
if ($running -and $running -ne $tag -and $running -ne 'local') { $running | Set-Content $previousTag -Encoding utf8NoBOM }
Use-Tag $tag $commit

"$tag$(if ($dirty) { ' (with uncommitted changes)' }) deployed $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')" |
    Set-Content $record -Encoding utf8NoBOM
Remove-Item $skip -Force -ErrorAction SilentlyContinue   # a deploy by hand overrides an earlier rollback

# Keep the images of this deploy and the previous one; older ones only take disk.
$keep = @($tag, (Get-Content $previousTag -ErrorAction SilentlyContinue | Select-Object -First 1)) | Where-Object { $_ }
& docker images --format '{{.Repository}}:{{.Tag}}' |
    Where-Object { $_ -match '^jobradar-(worker|web|migrate):(.+)$' -and $Matches[2] -notin $keep } |
    ForEach-Object { & docker rmi $_ | Out-Null }

Write-Host "Done: $tag. Status page: http://localhost:$(Get-SitePort)/status" -ForegroundColor Green
