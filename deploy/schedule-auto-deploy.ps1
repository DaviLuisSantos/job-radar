<#
.SYNOPSIS
    Registers (or updates) a Task Scheduler task that runs auto-deploy.ps1 every few minutes.

.DESCRIPTION
    Runs as the current user, only while logged on: the database lives in Docker Desktop, which runs in the
    user session, and deploy.ps1 needs the service rights install.ps1 gave this user. Started through a
    headless console host, so no window flashes on screen. No administrator rights needed. Pause with
    Disable-ScheduledTask, remove with:
        Unregister-ScheduledTask -TaskName 'JobRadar Auto-deploy' -Confirm:$false
#>
param(
    [int] $EveryMinutes = 15,
    [string] $TaskName = 'JobRadar Auto-deploy'
)

$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'auto-deploy.ps1'

$action = New-ScheduledTaskAction -Execute 'conhost.exe' `
    -Argument "--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$script`"" -WorkingDirectory (Split-Path $PSScriptRoot -Parent)
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval (New-TimeSpan -Minutes $EveryMinutes)
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 30)
$principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
    -Description 'Deploys main when it moved and CI passed (deploy\auto-deploy.ps1). Log: C:\JobRadar\auto-deploy.log.' -Force | Out-Null

Get-ScheduledTask -TaskName $TaskName | Get-ScheduledTaskInfo | Select-Object TaskName, NextRunTime
