#Requires -Version 7
<#
.SYNOPSIS
    Keeps the production stack up and doing its job. Run every few minutes by the host's scheduler.

.DESCRIPTION
    The containers restart by themselves when they crash (restart: unless-stopped), but not when Docker came up
    without them or a deploy died half way. This brings the stack up when the worker or the site is not running,
    except while a deploy holds deploying.lock (a lock older than an hour is from a deploy that died, and is ignored).
    It also asks the site's /health whether the system is doing its job, and restarts the worker when the database
    answers but no pass started in 26 h: the worker catches up on a missed pass at start.
    Writes to watchdog.log in the state folder only when it acts or fails.
#>
param(
    [int] $StaleLockMinutes = 60,
    [int] $HealthEveryHours = 6
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$state = Get-StateDir
$log = Join-Path $state 'watchdog.log'
$lock = Join-Path $state 'deploying.lock'

function Write-Log([string] $Message) {
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $Message" | Add-Content $log -Encoding utf8NoBOM
}

if (Test-Path $lock) {
    $age = (Get-Date) - (Get-Item $lock).LastWriteTime
    if ($age.TotalMinutes -lt $StaleLockMinutes) { return }
    Write-Log "Ignoring a deploy lock from $([int]$age.TotalMinutes) min ago: that deploy did not finish."
}

try {
    $running = Invoke-Compose ps --status running --services 2>$null
}
catch {
    Write-Log "Docker did not answer: $($_.Exception.Message)"
    return
}
$missing = 'worker', 'web' | Where-Object { $_ -notin $running }
if ($missing) {
    try {
        Invoke-Compose up -d | Out-Null
        Write-Log "Not running: $($missing -join ', '). Stack brought up."
    }
    catch { Write-Log "Not running: $($missing -join ', '). Bringing the stack up failed: $($_.Exception.Message)" }
    return
}

# Running is not working. /health says whether the database answers and a pass started within 26 h. A late pass with
# the database up: restart the worker. The database down: restarting would not help, so only say so. Either at most
# once per -HealthEveryHours, or a stuck worker would be restarted, and the log written, every few minutes.
$healthMarker = Join-Path $state 'watchdog-health.txt'
try {
    $response = Invoke-WebRequest "http://localhost:$(Get-SitePort)/health" -TimeoutSec 20 -SkipHttpErrorCheck
    $health = $response.Content | ConvertFrom-Json
}
catch { return }   # the site not answering: Docker's restart policy and the check above deal with it
if ($health.healthy) { return }
if ((Test-Path $healthMarker) -and ((Get-Date) - (Get-Item $healthMarker).LastWriteTime).TotalHours -lt $HealthEveryHours) { return }
Set-Content $healthMarker (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') -Encoding utf8NoBOM

if (-not $health.databaseReachable) {
    Write-Log "Unhealthy: $($health.problem). Not restarting the worker: it waits for the database by itself."
    return
}
try {
    Invoke-Compose restart worker | Out-Null
    Write-Log "Unhealthy: $($health.problem). Worker restarted to catch up on the missed pass."
}
catch { Write-Log "Unhealthy: $($health.problem). Worker did not restart: $($_.Exception.Message)" }
