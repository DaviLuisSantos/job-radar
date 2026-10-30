<#
.SYNOPSIS
    Registers (or updates) a daily Task Scheduler task that runs backup.ps1.

.DESCRIPTION
    Runs as the current user, only while logged on: the database lives in Docker Desktop, which
    runs in the user session. If the machine is off at the set time, the task runs as soon as it is
    available. No administrator rights needed. Remove with:
        Unregister-ScheduledTask -TaskName 'JobRadar Backup' -Confirm:$false
#>
param(
    [string] $At = '23:00',
    [string] $TaskName = 'JobRadar Backup'
)

$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'backup.ps1'

$action = New-ScheduledTaskAction -Execute 'pwsh.exe' `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`"" -WorkingDirectory (Split-Path $PSScriptRoot -Parent)
$trigger = New-ScheduledTaskTrigger -Daily -At $At
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 30)
$principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
    -Description 'Dumps the JobRadar database (deploy\backup.ps1); keeps the latest 30.' -Force | Out-Null

Get-ScheduledTask -TaskName $TaskName | Get-ScheduledTaskInfo | Select-Object TaskName, NextRunTime
