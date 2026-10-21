<#
.SYNOPSIS
    Dumps the JobRadar database to a dated file and keeps the most recent ones.

.DESCRIPTION
    The market snapshot history cannot be collected again: back it up. Uses pg_dump inside the
    docker compose service, so nothing else needs installing. Every run, good or bad, is appended
    to backup.log next to the dumps (a scheduled run has no console to look at). Restore with:
        docker compose exec -T postgres pg_restore -U jobradar -d jobradar --clean < <file>
#>
param(
    [string] $Destination = (Join-Path $env:USERPROFILE 'Backups\JobRadar'),
    [int] $Keep = 30
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force $Destination | Out-Null
$log = Join-Path $Destination 'backup.log'
# Seconds in the name: two runs in the same minute must not overwrite each other.
$file = Join-Path $Destination "jobradar_$(Get-Date -Format yyyy-MM-dd_HHmmss).dump"

function Write-Log([string] $message) {
    $line = "{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $message
    Add-Content -Path $log -Value $line
    Write-Host $line
}

try {
    Push-Location $repo
    try {
        # Custom format (-Fc): compressed, restorable table by table. Binary output: let cmd redirect it,
        # with stderr in its own file so error text never ends up inside the dump.
        $stderr = "$file.err"
        cmd /c "docker compose exec -T postgres pg_dump -U jobradar -d jobradar -Fc > `"$file`" 2> `"$stderr`""
        $code = $LASTEXITCODE
        $errors = if (Test-Path $stderr) { (Get-Content $stderr -Raw); Remove-Item $stderr } else { '' }
        if ($code -or -not (Test-Path $file) -or (Get-Item $file).Length -eq 0) {
            throw "pg_dump failed (exit $code): $errors"
        }
    }
    finally { Pop-Location }

    Get-ChildItem $Destination -Filter 'jobradar_*.dump' | Sort-Object Name -Descending | Select-Object -Skip $Keep | Remove-Item
    Write-Log ("ok {0} ({1:N1} MB)" -f (Split-Path $file -Leaf), ((Get-Item $file).Length / 1MB))
}
catch {
    if ((Test-Path $file) -and (Get-Item $file).Length -eq 0) { Remove-Item $file }
    Write-Log "FAILED $($_.Exception.Message)"
    exit 1
}
