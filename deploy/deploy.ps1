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
    The build it replaces is kept in -Target\previous. -Rollback swaps the two back, without publishing:
    run it again to undo. A rollback does not undo migrations, and it tells auto-deploy.ps1 to leave the
    rolled-back commit alone (-Target\rollback-skip.txt) until a newer one reaches main.
    Refuses to run during the daily pass, or while runs are still being recorded (a pass recovered at another
    hour), unless -Force: stopping the worker would cut the pass short.
    See docs/producao.md.
#>
param(
    [string] $Target = 'C:\JobRadar',
    [int] $WebPort = 5380,
    # Collection:DailyAt in Australia/Perth; the pass takes ~15 min, so the window below is generous.
    [timespan] $PassAt = '06:00',
    [switch] $Force,
    [switch] $Rollback
)

$ErrorActionPreference = 'Stop'
if (-not [IO.Path]::IsPathRooted($Target)) { throw "-Target must be an absolute path, not '$Target'." }
$repo = Split-Path $PSScriptRoot -Parent
$staging = Join-Path $env:TEMP "jobradar-publish-$(Get-Date -Format yyyyMMddHHmmss)"
$services = @(
    @{ Name = 'JobRadarWorker'; Project = 'src\JobRadar.Worker'; Dir = 'worker' },
    @{ Name = 'JobRadarWeb'; Project = 'src\JobRadar.Web'; Dir = 'web' }
)
$previous = Join-Path $Target 'previous'
$record = Join-Path $Target 'deployed.txt'
$skip = Join-Path $Target 'rollback-skip.txt'

function Copy-Tree([string] $From, [string] $To) {
    # A stopped service can hold its files for a moment: retry instead of failing.
    robocopy $From $To /MIR /R:10 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying $From to $To failed (robocopy $LASTEXITCODE). The services are stopped." }
}

function Start-AndWait {
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
}

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
if (-not $Force) {
    # A pass recovered after a restart runs at any hour. The steps after the last run (descriptions, scores,
    # snapshot) take ~15 min, so 30 min of quiet means it is over. Fixed project name: it works from a worktree.
    $recent = $null
    try {
        $recent = & docker compose -p job-radar-au -f (Join-Path $repo 'docker-compose.yml') exec -T postgres `
            psql -U jobradar -d jobradar -Atc "select count(*) from collection_runs where started_at > now() - interval '30 minutes'" 2>$null
    }
    catch { }
    if ($null -eq $recent) { Write-Warning 'Could not ask the database whether a pass is running.' }
    elseif ([int]$recent -gt 0) { throw "$recent runs recorded in the last 30 minutes: a pass may be running. Wait, or use -Force." }
}

if ($Rollback) {
    if (-not (Test-Path (Join-Path $previous 'deployed.txt'))) { throw "Nothing to roll back to: $previous is filled by the next deploy." }
    $current = Get-Content $record -ErrorAction SilentlyContinue | Select-Object -First 1
    $back = Get-Content (Join-Path $previous 'deployed.txt') | Select-Object -First 1
    Write-Host "== Rolling back to: $back" -ForegroundColor Cyan
    Write-Warning 'Migrations are not undone: an older build may not run on a newer schema.'
    foreach ($s in $services) { Stop-Service $s.Name -Force }
    $swap = Join-Path $Target 'rollback-swap'
    foreach ($s in $services) {
        Copy-Tree (Join-Path $Target $s.Dir) (Join-Path $swap $s.Dir)
        Copy-Tree (Join-Path $previous $s.Dir) (Join-Path $Target $s.Dir)
        Copy-Tree (Join-Path $swap $s.Dir) (Join-Path $previous $s.Dir)
    }
    Copy-Item (Join-Path $Target 'migrate.exe') $swap -Force
    Copy-Item (Join-Path $previous 'migrate.exe') $Target -Force
    Copy-Item (Join-Path $swap 'migrate.exe') $previous -Force
    Start-AndWait
    $undone = ($current -split ' ')[0]
    $current | Set-Content (Join-Path $previous 'deployed.txt') -Encoding utf8
    "$back (rolled back from $undone on $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz'))" | Set-Content $record -Encoding utf8
    $undone | Set-Content $skip -Encoding utf8
    Remove-Item $swap -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Rolled back. auto-deploy.ps1 skips $undone until main moves past it." -ForegroundColor Green
    return
}

$commit = (& git -C $repo rev-parse --short HEAD)
$dirty = [bool](& git -C $repo status --porcelain --untracked-files=no)
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

if (Test-Path $record) {
    Write-Host "== Keeping the running build in $previous" -ForegroundColor Cyan
    foreach ($s in $services) { Copy-Tree (Join-Path $Target $s.Dir) (Join-Path $previous $s.Dir) }
    Copy-Item (Join-Path $Target 'migrate.exe'), $record $previous -Force
}

Write-Host "== Copying to $Target" -ForegroundColor Cyan
foreach ($s in $services) { Copy-Tree (Join-Path $staging $s.Dir) (Join-Path $Target $s.Dir) }
Copy-Item (Join-Path $staging 'migrate.exe') $Target -Force

Write-Host '== Applying migrations' -ForegroundColor Cyan
& (Join-Path $Target 'migrate.exe') --connection $connection
if ($LASTEXITCODE) { throw 'Migrations failed: is the database up (docker compose up -d)? The services are stopped.' }

Start-AndWait

"$commit$(if ($dirty) { ' (with uncommitted changes)' }) deployed $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')" |
    Set-Content $record -Encoding utf8
Remove-Item $skip -Force -ErrorAction SilentlyContinue   # a deploy by hand overrides an earlier rollback
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Done: $commit. Status page: http://localhost:$WebPort/status" -ForegroundColor Green
