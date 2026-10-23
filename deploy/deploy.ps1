<#
.SYNOPSIS
    Publishes the current repository and puts it in production. No admin needed.

.DESCRIPTION
    Needs install.ps1 to have run once, as admin: it creates the services and lets this user write to
    -Target and start and stop them.
    1. Publishes the worker and the triage site (Release) and an EF Core migration bundle.
    2. Stops the services, copies the new build to -Target, applies pending migrations (connection string
       read from the worker service's registry key, where install.ps1 put it), starts them again.
    3. Waits for the status page and records what was deployed in -Target\deployed.txt.
    Refuses to run during the daily pass (stopping the worker would cut it short) unless -Force.
    See docs/producao.md.
#>
param(
    [string] $Target = 'C:\JobRadar',
    [int] $WebPort = 5380,
    # Collection:DailyAt in Australia/Perth; the pass takes ~15 min, so the window below is generous.
    [timespan] $PassAt = '06:00',
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$staging = Join-Path $env:TEMP "jobradar-publish-$(Get-Date -Format yyyyMMddHHmmss)"
$services = @(
    @{ Name = 'JobRadarWorker'; Project = 'src\JobRadar.Worker'; Dir = 'worker' },
    @{ Name = 'JobRadarWeb'; Project = 'src\JobRadar.Web'; Dir = 'web' }
)

foreach ($s in $services) {
    if (-not (Get-Service $s.Name -ErrorAction SilentlyContinue)) {
        throw "Service $($s.Name) does not exist: run .\deploy\install.ps1 once, as admin."
    }
}
$environment = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\JobRadarWorker" -Name Environment -ErrorAction SilentlyContinue).Environment
$connection = ($environment | Where-Object { $_ -like 'ConnectionStrings__JobRadar=*' }) -replace '^ConnectionStrings__JobRadar=', ''
if (-not $connection) { throw 'No connection string in the JobRadarWorker service: run .\deploy\install.ps1 once, as admin.' }

$perth = [TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow, 'W. Australia Standard Time').TimeOfDay
if (-not $Force -and $perth -ge $PassAt.Subtract([timespan]'00:05') -and $perth -le $PassAt.Add([timespan]'00:45')) {
    throw "It is $($perth.ToString('hh\:mm')) in Perth: the daily pass ($PassAt) may be running. Wait, or use -Force."
}

$commit = (& git -C $repo rev-parse --short HEAD)
$dirty = [bool](& git -C $repo status --porcelain)
if ($dirty) { Write-Warning 'The working tree has uncommitted changes: they go to production too.' }

Write-Host '== Publishing' -ForegroundColor Cyan
foreach ($s in $services) {
    dotnet publish (Join-Path $repo $s.Project) -c Release -o (Join-Path $staging $s.Dir) --nologo -v quiet
    if ($LASTEXITCODE) { throw "Publishing $($s.Project) failed." }
}
Push-Location $repo
try {
    dotnet tool restore | Out-Null
    dotnet ef migrations bundle --project src/JobRadar.Infrastructure --startup-project src/JobRadar.Infrastructure `
        -o (Join-Path $staging 'migrate.exe') --force --configuration Release | Out-Null
    if ($LASTEXITCODE) { throw 'Building the migration bundle failed.' }
}
finally { Pop-Location }

# Real profiles live next to the binary and are not versioned. In a worktree they are in the main checkout.
$profiles = Join-Path $repo 'src\JobRadar.Worker\appsettings.Local.json'
if (-not (Test-Path $profiles)) {
    $main = Split-Path (& git -C $repo rev-parse --path-format=absolute --git-common-dir) -Parent
    $profiles = Join-Path $main 'src\JobRadar.Worker\appsettings.Local.json'
}
if (Test-Path $profiles) { Copy-Item $profiles (Join-Path $staging 'worker') -Force }
else { throw "No appsettings.Local.json with the profiles: the worker would collect nothing." }

# The site listens on localhost and on the Tailscale address; Host headers are limited to those names.
# In Windows PowerShell 5.1 a native command writing to stderr under 'Stop' throws: keep it local.
$tailscaleIp = $null
$dnsName = $null
try { $tailscaleIp = (& tailscale ip -4 2>$null | Select-Object -First 1) } catch { Write-Warning 'Tailscale not available: the site will listen on localhost only.' }
try { $dnsName = ((& tailscale status --json 2>$null | ConvertFrom-Json).Self.DNSName).TrimEnd('.') } catch { }
$urls = @("http://localhost:$WebPort")
$hosts = @('localhost')
if ($tailscaleIp) { $urls += "http://${tailscaleIp}:$WebPort"; $hosts += $tailscaleIp }
if ($dnsName) { $hosts += $dnsName }
@{ Urls = ($urls -join ';'); AllowedHosts = ($hosts -join ';') } | ConvertTo-Json |
    Set-Content (Join-Path $staging 'web\appsettings.Local.json') -Encoding utf8
Write-Host "Site: $($urls -join ', ')"

Write-Host '== Stopping services' -ForegroundColor Cyan
foreach ($s in $services) { Stop-Service $s.Name -Force }

Write-Host "== Copying to $Target" -ForegroundColor Cyan
foreach ($s in $services) {
    $dest = Join-Path $Target $s.Dir
    # A stopped service can hold its files for a moment: retry instead of failing.
    robocopy (Join-Path $staging $s.Dir) $dest /MIR /R:10 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying to $dest failed (robocopy $LASTEXITCODE). The services are stopped." }
}
Copy-Item (Join-Path $staging 'migrate.exe') $Target -Force

Write-Host '== Applying migrations' -ForegroundColor Cyan
& (Join-Path $Target 'migrate.exe') --connection $connection
if ($LASTEXITCODE) { throw 'Migrations failed: is the database up (docker compose up -d)? The services are stopped.' }

Write-Host '== Starting' -ForegroundColor Cyan
foreach ($s in $services) { Start-Service $s.Name }

$status = "http://localhost:$WebPort/status"
$up = $false
foreach ($i in 1..30) {
    try { $up = (Invoke-WebRequest $status -UseBasicParsing -TimeoutSec 5).StatusCode -eq 200 } catch { }
    if ($up) { break }
    Start-Sleep -Seconds 2
}
Get-Service ($services | ForEach-Object { $_.Name }) | Format-Table Name, Status, StartType -AutoSize
if (-not $up) { throw "The status page did not answer at $status. See the Application event log." }

"$commit$(if ($dirty) { ' (with uncommitted changes)' }) deployed $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')" |
    Set-Content (Join-Path $Target 'deployed.txt') -Encoding utf8
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Done: $commit. Status page: $status" -ForegroundColor Green
