<#
.SYNOPSIS
    Registers (or updates) a Task Scheduler task that runs watchdog.ps1 at logon and every few minutes.

.DESCRIPTION
    Runs as the current user, only while logged on: the database lives in Docker Desktop, which runs in the
    user session, so before a logon there is nothing for the services to do anyway. Started through a
    headless console host, so no window flashes on screen. No administrator rights needed. Pause with
    Disable-ScheduledTask, remove with:
        Unregister-ScheduledTask -TaskName 'JobRadar Watchdog' -Confirm:$false
#>
param(
    [int] $EveryMinutes = 15,
    [int] $AfterLogonMinutes = 2,
    [string] $TaskName = 'JobRadar Watchdog'
)

$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'watchdog.ps1'
$user = [Security.Principal.WindowsIdentity]::GetCurrent().Name

$action = New-ScheduledTaskAction -Execute 'conhost.exe' `
    -Argument "--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$script`"" -WorkingDirectory (Split-Path $PSScriptRoot -Parent)
# At logon (after a reboot: services that timed out at boot), then every few minutes (any other stop).
$atLogon = New-ScheduledTaskTrigger -AtLogOn -User $user
$atLogon.Delay = "PT$($AfterLogonMinutes)M"
$repeat = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval (New-TimeSpan -Minutes $EveryMinutes)
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 5)
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $atLogon, $repeat -Settings $settings -Principal $principal `
    -Description 'Starts the JobRadar services when they are stopped (deploy\watchdog.ps1). Log: C:\JobRadar\watchdog.log.' -Force | Out-Null

Get-ScheduledTask -TaskName $TaskName | Get-ScheduledTaskInfo | Select-Object TaskName, NextRunTime
