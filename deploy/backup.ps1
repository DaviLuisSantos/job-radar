#Requires -Version 7
<#
.SYNOPSIS
    Dumps the JobRadar database to a dated file and keeps the most recent ones. Any OS with Docker.

.DESCRIPTION
    The market snapshot history cannot be collected again: back it up. Runs pg_dump inside the postgres service
    and copies the file out, so nothing else needs installing. Every run, good or bad, is appended to backup.log
    next to the dumps (a scheduled run has no console to look at). Restore with:
        docker compose -p job-radar-au cp <file> postgres:/tmp/restore.dump
        docker compose -p job-radar-au exec postgres pg_restore -U jobradar -d jobradar --clean /tmp/restore.dump
#>
param(
    [string] $Destination = (Join-Path $HOME 'Backups/JobRadar'),
    [int] $Keep = 30
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

New-Item -ItemType Directory -Force $Destination | Out-Null
$log = Join-Path $Destination 'backup.log'
# Seconds in the name: two runs in the same minute must not overwrite each other.
$name = "jobradar_$(Get-Date -Format yyyy-MM-dd_HHmmss).dump"
$file = Join-Path $Destination $name

function Write-Log([string] $message) {
    $line = "{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $message
    Add-Content -Path $log -Value $line
    Write-Host $line
}

try {
    # Custom format (-Fc): compressed, restorable table by table. Written inside the container and copied out: a
    # binary dump through a pipe would depend on the shell.
    Invoke-Compose exec -T postgres pg_dump -U jobradar -d jobradar -Fc -f "/tmp/$name" | Out-Null
    try { Invoke-Compose cp "postgres:/tmp/$name" $file | Out-Null }
    finally { Invoke-Compose exec -T postgres rm -f "/tmp/$name" | Out-Null }
    if (-not (Test-Path $file) -or (Get-Item $file).Length -eq 0) { throw 'pg_dump wrote nothing.' }

    Get-ChildItem $Destination -Filter 'jobradar_*.dump' | Sort-Object Name -Descending | Select-Object -Skip $Keep | Remove-Item
    Write-Log ("ok {0} ({1:N1} MB)" -f $name, ((Get-Item $file).Length / 1MB))
}
catch {
    if ((Test-Path $file) -and (Get-Item $file).Length -eq 0) { Remove-Item $file }
    Write-Log "FAILED $($_.Exception.Message)"
    exit 1
}
