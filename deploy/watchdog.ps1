<#
.SYNOPSIS
    Starts the JobRadar services when they are stopped. Run by the 'JobRadar Watchdog' task.

.DESCRIPTION
    The services restart by themselves when they crash (install.ps1 sets that), but not when they fail to
    start: after an unexpected shutdown on 2026-10-25 both timed out at boot and stayed stopped for two days.
    This covers that and any other stop. It leaves them alone while a deploy holds -Target\deploying.lock
    (deploy.ps1 stops them on purpose); a lock older than an hour is from a deploy that died, and is ignored.
    It also asks the site's /health whether the system is doing its job, and restarts the worker when the
    database answers but no pass started in 26 h (see below).
    Writes to -Target\watchdog.log only when it acts or fails. No administrator rights needed: install.ps1
    lets this user start and stop the services.
#>
param(
    [string] $Target = 'C:\JobRadar',
    [int] $StaleLockMinutes = 60,
    [int] $WebPort = 5380,
    [int] $HealthEveryHours = 6
)

$ErrorActionPreference = 'Stop'
$log = Join-Path $Target 'watchdog.log'
$lock = Join-Path $Target 'deploying.lock'

function Write-Log([string] $Message) {
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $Message" | Add-Content $log -Encoding utf8
}

if (Test-Path $lock) {
    $age = (Get-Date) - (Get-Item $lock).LastWriteTime
    if ($age.TotalMinutes -lt $StaleLockMinutes) { return }
    Write-Log "Ignoring a deploy lock from $([int]$age.TotalMinutes) min ago: that deploy did not finish."
}

foreach ($name in 'JobRadarWorker', 'JobRadarWeb') {
    $service = Get-Service $name -ErrorAction SilentlyContinue
    if (-not $service -or $service.StartType -eq 'Disabled' -or $service.Status -ne 'Stopped') { continue }
    try {
        Start-Service $name
        Write-Log "$name was stopped: started."
    }
    catch {
        Write-Log "$name was stopped and did not start: $($_.Exception.Message)"
    }
}

# Running is not working. The site's /health says whether the database answers and a pass started within 26 h.
# A late pass with the database up: restart the worker, which catches up on a missed pass at start. The database
# down: restarting would not help, so only say so. Either at most once per -HealthEveryHours, or a stuck worker
# would be restarted, and the log written, every few minutes.
$healthMarker = Join-Path $Target 'watchdog-health.txt'
try {
    $health = Invoke-RestMethod "http://localhost:$WebPort/health" -TimeoutSec 20
}
catch {
    # 503 carries the report in the body; anything else (the site not answering) was handled above or will be.
    if (-not $_.ErrorDetails -or -not $_.ErrorDetails.Message) { return }
    try { $health = $_.ErrorDetails.Message | ConvertFrom-Json } catch { return }
}
if ($health.healthy) { return }
if ((Test-Path $healthMarker) -and ((Get-Date) - (Get-Item $healthMarker).LastWriteTime).TotalHours -lt $HealthEveryHours) { return }
Set-Content $healthMarker (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') -Encoding utf8

if (-not $health.databaseReachable) {
    Write-Log "Unhealthy: $($health.problem). Not restarting the worker: it waits for the database by itself."
    return
}
try {
    Restart-Service JobRadarWorker
    Write-Log "Unhealthy: $($health.problem). Worker restarted to catch up on the missed pass."
}
catch {
    Write-Log "Unhealthy: $($health.problem). Worker did not restart: $($_.Exception.Message)"
}
