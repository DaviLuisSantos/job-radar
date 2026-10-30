#Requires -RunAsAdministrator
<#
.SYNOPSIS
    LEGACY: production runs in containers since 2026-10-30 (docs/producao.md). Kept for two weeks, until the old
    Windows services are removed.
    One-time machine setup for JobRadar: the two Windows services, their configuration, the firewall rule
    and the rights that let -User deploy new versions without an elevated prompt.

.DESCRIPTION
    1. Creates -Target and gives -User modify rights on it (deploy.ps1 copies the build there).
    2. Creates or updates the services (automatic, delayed start, restart on failure) with the connection
       string and environment in the service registry, not in files.
    3. Lets -User query, start and stop both services (an ACE added to each service's security descriptor).
    4. Opens the site's port to the Tailscale network only (100.64.0.0/10).
    It does not publish or start anything: run deploy.ps1 afterwards, without admin. Run this again only
    when what it sets changes (connection string, port, a new service, another user). See docs/producao.md.
#>
param(
    [string] $Target = 'C:\JobRadar',
    [string] $ConnectionString = 'Host=localhost;Port=5432;Database=jobradar;Username=jobradar;Password=jobradar',
    [int] $WebPort = 5380,
    # Who deploys. Elevating from the author's own session keeps the same identity, so the default is right.
    [string] $User = [Security.Principal.WindowsIdentity]::GetCurrent().Name
)

$ErrorActionPreference = 'Stop'
$sid = (New-Object Security.Principal.NTAccount $User).Translate([Security.Principal.SecurityIdentifier]).Value

$services = @(
    @{ Name = 'JobRadarWorker'; Display = 'JobRadar Worker'; Exe = 'worker\JobRadar.Worker.exe'
       Description = 'Collects Seek listings, marks full-text terms, fetches full ads, scores profiles and takes the market snapshot once a day.' },
    @{ Name = 'JobRadarWeb'; Display = 'JobRadar Web'; Exe = 'web\JobRadar.Web.exe'
       Description = "Triage site on http://localhost:$WebPort (and the Tailscale address)." }
)

Write-Host "== $Target, writable by $User" -ForegroundColor Cyan
New-Item -ItemType Directory -Force $Target | Out-Null
icacls $Target /grant "*${sid}:(OI)(CI)M" | Out-Null
if ($LASTEXITCODE) { throw "Granting $User rights on $Target failed (icacls $LASTEXITCODE)." }

Write-Host '== Services' -ForegroundColor Cyan
foreach ($s in $services) {
    $exe = '"' + (Join-Path $Target $s.Exe) + '"'
    if (-not (Get-Service $s.Name -ErrorAction SilentlyContinue)) {
        # Works in Windows PowerShell 5.1 too (AutomaticDelayedStart only exists from PowerShell 6).
        New-Service -Name $s.Name -BinaryPathName $exe -DisplayName $s.Display -Description $s.Description -StartupType Automatic | Out-Null
    }
    sc.exe config $s.Name binPath= $exe start= delayed-auto | Out-Null
    if ($LASTEXITCODE) { throw "Configuring service $($s.Name) failed (sc.exe $LASTEXITCODE)." }
    sc.exe description $s.Name $s.Description | Out-Null
    # Restart after 1 min, 1 min, then 5 min; the failure count resets after a day.
    sc.exe failure $s.Name reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
    # Configuration the service needs, kept in its registry key instead of files (deploy.ps1 reads it back).
    New-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$($s.Name)" -Name Environment -PropertyType MultiString -Force -Value @(
        'DOTNET_ENVIRONMENT=Production',
        'ASPNETCORE_ENVIRONMENT=Production',
        "ConnectionStrings__JobRadar=$ConnectionString"
    ) | Out-Null

    # Query config and status, enumerate dependents, start, stop, pause, interrogate, user control, read.
    $sddl = (sc.exe sdshow $s.Name | Where-Object { $_ }) -join ''
    if ($sddl -notlike "*;;;$sid)*") {
        $ace = "(A;;CCLCSWRPWPDTLOCRRC;;;$sid)"
        $sacl = $sddl.IndexOf('S:')
        $sddl = if ($sacl -ge 0) { $sddl.Insert($sacl, $ace) } else { $sddl + $ace }
        sc.exe sdset $s.Name $sddl | Out-Null
        if ($LASTEXITCODE) { throw "Granting $User control of $($s.Name) failed (sc.exe $LASTEXITCODE)." }
    }
}

if (-not (Get-NetFirewallRule -Name 'JobRadarWebTailscale' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -Name 'JobRadarWebTailscale' -DisplayName 'JobRadar Web (Tailscale only)' -Direction Inbound `
        -Protocol TCP -LocalPort $WebPort -RemoteAddress 100.64.0.0/10 -Action Allow | Out-Null
}
else {
    Set-NetFirewallRule -Name 'JobRadarWebTailscale' -LocalPort $WebPort -RemoteAddress 100.64.0.0/10 | Out-Null
}

Write-Host "Done. $User can now deploy without admin: .\deploy\deploy.ps1" -ForegroundColor Green
