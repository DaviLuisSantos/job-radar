<#
.SYNOPSIS
    Dumps the JobRadar database to a dated file and keeps the most recent ones.

.DESCRIPTION
    The market snapshot history cannot be collected again: back it up. Uses pg_dump inside the
    docker compose service, so nothing else needs installing. Restore with:
        docker compose exec -T postgres pg_restore -U jobradar -d jobradar --clean < <file>
#>
param(
    [string] $Destination = (Join-Path $env:USERPROFILE 'Backups\JobRadar'),
    [int] $Keep = 30
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force $Destination | Out-Null
$file = Join-Path $Destination "jobradar_$(Get-Date -Format yyyy-MM-dd_HHmm).dump"

Push-Location $repo
try {
    # Custom format (-Fc): compressed, restorable table by table. Binary output: let cmd redirect it.
    cmd /c "docker compose exec -T postgres pg_dump -U jobradar -d jobradar -Fc > `"$file`""
    if ($LASTEXITCODE -or -not (Test-Path $file) -or (Get-Item $file).Length -eq 0) { throw 'pg_dump failed: is the database up?' }
}
finally { Pop-Location }

Get-ChildItem $Destination -Filter 'jobradar_*.dump' | Sort-Object Name -Descending | Select-Object -Skip $Keep | Remove-Item
Write-Host ("Backup: {0} ({1:N1} MB)" -f $file, ((Get-Item $file).Length / 1MB))
