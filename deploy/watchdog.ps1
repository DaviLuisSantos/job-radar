<#
.SYNOPSIS
    Starts the JobRadar services when they are stopped. Run by the 'JobRadar Watchdog' task.

.DESCRIPTION
    The services restart by themselves when they crash (install.ps1 sets that), but not when they fail to
    start: after an unexpected shutdown on 2026-10-25 both timed out at boot and stayed stopped for two days.
    This covers that and any other stop. It leaves them alone while a deploy holds -Target\deploying.lock
    (deploy.ps1 stops them on purpose); a lock older than an hour is from a deploy that died, and is ignored.
    Writes to -Target\watchdog.log only when it acts or fails. No administrator rights needed: install.ps1
    lets this user start the services.
#>
param(
    [string] $Target = 'C:\JobRadar',
    [int] $StaleLockMinutes = 60
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
