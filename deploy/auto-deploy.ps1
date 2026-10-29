<#
.SYNOPSIS
    Deploys main when it has moved and its CI run passed. Meant for a scheduled task (schedule-auto-deploy.ps1).

.DESCRIPTION
    Production pulls; nothing outside reaches in. Each run:
    1. fetches origin/main and compares it with the commit in -Target\deployed.txt;
    2. leaves alone a commit rolled back by deploy.ps1 -Rollback (-Target\rollback-skip.txt);
    3. asks GitHub (public API, no token) for the CI run of that commit on main: waits while it runs,
       gives up on that commit if it failed;
    4. fast-forwards this checkout (refuses local changes to tracked files, another branch, or local commits);
    5. runs deploy.ps1, which itself refuses the daily pass window and a pass still recording runs.
    Quiet when there is nothing to do; everything else goes to -Target\auto-deploy.log.
    -DryRun says what it would do and changes nothing.
#>
param(
    [string] $Target = 'C:\JobRadar',
    [string] $Repository = 'DaviLuisSantos/job-radar',
    [string] $Workflow = 'CI',
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$log = Join-Path $Target 'auto-deploy.log'
$lastOutput = Join-Path $Target 'auto-deploy-last.txt'   # everything deploy.ps1 printed, last time it ran
$output = $null

function Write-Log([string] $message) {
    $line = "{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $message
    if ($DryRun) { Write-Host "(dry run) $line" } else { Add-Content -Path $log -Value $line -Encoding utf8 }
}

# Git writes progress to stderr; under 'Stop' in Windows PowerShell 5.1 that throws. Run it through cmd.
function Invoke-Git([string] $arguments) {
    $output = cmd /c "git -C `"$repo`" $arguments 2>&1"
    if ($LASTEXITCODE) { throw "git $arguments failed: $output" }
    return $output
}

try {
    Invoke-Git 'fetch --quiet origin main' | Out-Null
    $head = (Invoke-Git 'rev-parse origin/main').Trim()
    $short = $head.Substring(0, 7)

    $deployed = (Get-Content (Join-Path $Target 'deployed.txt') -ErrorAction SilentlyContinue | Select-Object -First 1) -split ' ' | Select-Object -First 1
    if ($deployed -and $head.StartsWith($deployed)) {
        if ($DryRun) { Write-Log "$short is already deployed: nothing to do." }
        return
    }
    $skipped = Get-Content (Join-Path $Target 'rollback-skip.txt') -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($skipped -and $head.StartsWith($skipped.Trim())) {
        if ($DryRun) { Write-Log "$short was rolled back: waiting for a newer commit on main." }
        return
    }

    $runs = Invoke-RestMethod "https://api.github.com/repos/$Repository/actions/runs?head_sha=$head&branch=main&event=push&per_page=20" `
        -Headers @{ 'User-Agent' = 'jobradar-auto-deploy'; 'Accept' = 'application/vnd.github+json' } -TimeoutSec 30
    $run = $runs.workflow_runs | Where-Object { $_.name -eq $Workflow } | Sort-Object run_attempt -Descending | Select-Object -First 1
    if (-not $run -or $run.status -ne 'completed') {
        if ($DryRun) { Write-Log "$short has no finished CI run yet: waiting." }
        return
    }
    if ($run.conclusion -ne 'success') {
        # Logged once: the commit stays skipped until main moves.
        $marker = "CI $($run.conclusion) for $short"
        if (-not (Select-String -Path $log -SimpleMatch $marker -Quiet -ErrorAction SilentlyContinue)) { Write-Log "$marker ($($run.html_url)): not deploying." }
        return
    }

    $branch = (Invoke-Git 'branch --show-current').Trim()
    if ($branch -ne 'main') { Write-Log "Checkout is on '$branch', not main: not deploying $short."; return }
    if (Invoke-Git 'status --porcelain --untracked-files=no') { Write-Log "Checkout has uncommitted changes: not deploying $short."; return }
    cmd /c "git -C `"$repo`" merge-base --is-ancestor HEAD origin/main"
    if ($LASTEXITCODE) { Write-Log "Checkout has commits that are not on origin/main: not deploying $short."; return }

    if ($DryRun) { Write-Log "Would fast-forward to $short and run deploy.ps1 (CI: $($run.html_url))."; return }

    Invoke-Git 'merge --ff-only --quiet origin/main' | Out-Null
    Write-Log "Deploying $short (CI: $($run.html_url))."
    $output = & (Join-Path $PSScriptRoot 'deploy.ps1') -Target $Target *>&1 | Out-String
    # Trust the record, not a quiet exit: deploy.ps1 writes it only after the site answered.
    $now = (Get-Content (Join-Path $Target 'deployed.txt') -ErrorAction SilentlyContinue | Select-Object -First 1) -split ' ' | Select-Object -First 1
    if (-not ($now -and $head.StartsWith($now))) { throw "deploy.ps1 ended without recording $short (still $now)." }
    Write-Log "Deployed $short."
}
catch {
    Write-Log "FAILED $($_.Exception.Message)$(if ($output) { " Output: $lastOutput" })"
    exit 1
}
finally {
    if ($output) { Set-Content -Path $lastOutput -Value $output -Encoding utf8 }
}
