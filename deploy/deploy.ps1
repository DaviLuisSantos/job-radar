#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Publishes JobRadar and installs (or updates) it as two Windows services on this machine.

.DESCRIPTION
    1. Publishes the worker and the triage site (Release) and an EF Core migration bundle.
    2. Stops the services if they exist, copies the new build to -Target.
    3. Applies pending migrations to the database.
    4. Creates or updates the services (automatic, delayed start, restart on failure) with the
       connection string and environment in the service registry, not in files.
    5. Lets the site listen on localhost and on this machine's Tailscale address, and opens the
       port to the Tailscale network only (100.64.0.0/10).
    Run it again to deploy a new version. See docs/producao.md.
#>
param(
    [string] $Target = 'C:\JobRadar',
    [string] $ConnectionString = 'Host=localhost;Port=5432;Database=jobradar;Username=jobradar;Password=jobradar',
    [int] $WebPort = 5380
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$staging = Join-Path $env:TEMP "jobradar-publish-$(Get-Date -Format yyyyMMddHHmmss)"

$services = @(
    @{ Name = 'JobRadarWorker'; Display = 'JobRadar Worker'; Project = 'src\JobRadar.Worker'; Exe = 'JobRadar.Worker.exe'; Dir = 'worker'
       Description = 'Collects Seek listings, marks full-text terms, scores profiles and takes the market snapshot once a day.' },
    @{ Name = 'JobRadarWeb'; Display = 'JobRadar Web'; Project = 'src\JobRadar.Web'; Exe = 'JobRadar.Web.exe'; Dir = 'web'
       Description = "Triage site on http://localhost:$WebPort (and the Tailscale address)." }
)

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

# Real profiles live next to the binary and are not versioned.
$profiles = Join-Path $repo 'src\JobRadar.Worker\appsettings.Local.json'
if (Test-Path $profiles) { Copy-Item $profiles (Join-Path $staging 'worker') -Force }
else { Write-Warning "No ${profiles}: the worker will start with no profiles and collect nothing." }

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
foreach ($s in $services) {
    if (Get-Service $s.Name -ErrorAction SilentlyContinue) { Stop-Service $s.Name -Force -ErrorAction SilentlyContinue }
}

Write-Host "== Copying to $Target" -ForegroundColor Cyan
foreach ($s in $services) {
    $dest = Join-Path $Target $s.Dir
    New-Item -ItemType Directory -Force $dest | Out-Null
    robocopy (Join-Path $staging $s.Dir) $dest /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying to $dest failed (robocopy $LASTEXITCODE)." }
}
Copy-Item (Join-Path $staging 'migrate.exe') $Target -Force

Write-Host '== Applying migrations' -ForegroundColor Cyan
& (Join-Path $Target 'migrate.exe') --connection $ConnectionString
if ($LASTEXITCODE) { throw 'Migrations failed: is the database up (docker compose up -d)?' }

Write-Host '== Installing services' -ForegroundColor Cyan
foreach ($s in $services) {
    $exe = '"' + (Join-Path (Join-Path $Target $s.Dir) $s.Exe) + '"'
    if (-not (Get-Service $s.Name -ErrorAction SilentlyContinue)) {
        # Works in Windows PowerShell 5.1 too (AutomaticDelayedStart only exists from PowerShell 6).
        New-Service -Name $s.Name -BinaryPathName $exe -DisplayName $s.Display -Description $s.Description -StartupType Automatic | Out-Null
    }
    sc.exe config $s.Name binPath= $exe start= delayed-auto | Out-Null
    if ($LASTEXITCODE) { throw "Configuring service $($s.Name) failed (sc.exe $LASTEXITCODE)." }
    # Restart after 1 min, 1 min, then 5 min; the failure count resets after a day.
    sc.exe failure $s.Name reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
    # Configuration the service needs, kept in its registry key instead of files.
    $key = "HKLM:\SYSTEM\CurrentControlSet\Services\$($s.Name)"
    New-ItemProperty $key -Name Environment -PropertyType MultiString -Force -Value @(
        'DOTNET_ENVIRONMENT=Production',
        'ASPNETCORE_ENVIRONMENT=Production',
        "ConnectionStrings__JobRadar=$ConnectionString"
    ) | Out-Null
}

if (-not (Get-NetFirewallRule -Name 'JobRadarWebTailscale' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -Name 'JobRadarWebTailscale' -DisplayName 'JobRadar Web (Tailscale only)' -Direction Inbound `
        -Protocol TCP -LocalPort $WebPort -RemoteAddress 100.64.0.0/10 -Action Allow | Out-Null
}

Write-Host '== Starting' -ForegroundColor Cyan
foreach ($s in $services) { Start-Service $s.Name }
Start-Sleep -Seconds 5
Get-Service ($services | ForEach-Object { $_.Name }) | Format-Table Name, Status, StartType -AutoSize
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Done. Status page: http://localhost:$WebPort/status" -ForegroundColor Green
