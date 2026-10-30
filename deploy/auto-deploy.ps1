#Requires -Version 7
<#
.SYNOPSIS
    Deploys main when it has moved and its CI run passed. Meant for the host's scheduler, every few minutes.

.DESCRIPTION
    Production pulls; nothing outside reaches in. Any OS with PowerShell 7, git and Docker. Each run:
    1. fetches origin/main and compares it with the commit in the state folder's deployed.txt;
    2. leaves alone a commit rolled back by deploy.ps1 -Rollback (rollback-skip.txt);
    3. asks GitHub (public API, no token) for the CI run of that commit on main: waits while it runs,
       gives up on that commit if it failed;
    4. fast-forwards this checkout (refuses local changes to tracked files, another branch, or local commits);
    5. runs deploy.ps1, which itself refuses the daily pass window and a pass still recording runs.
    Quiet when there is nothing to do; everything else goes to auto-deploy.log in the state folder.
    -DryRun says what it would do and changes nothing.
#>
param(
    [string] $Repository = 'DaviLuisSantos/job-radar',
    [string] $Workflow = 'CI',
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$state = Get-StateDir
$log = Join-Path $state 'auto-deploy.log'
$lastOutput = Join-Path $state 'auto-deploy-last.txt'   # everything deploy.ps1 printed, last time it ran
$output = $null

function Write-Log([string] $message) {
    $line = "{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $message
    if ($DryRun) { Write-Host "(dry run) $line" } else { Add-Content -Path $log -Value $line -Encoding utf8NoBOM }
}

function Invoke-Git {
    $result = & git -C $Repo @args 2>&1
    if ($LASTEXITCODE) { throw "git $($args -join ' ') failed: $result" }
    return $result
}

try {
    Invoke-Git fetch --quiet origin main | Out-Null
    $head = "$(Invoke-Git rev-parse origin/main)".Trim()
    $short = $head.Substring(0, 7)

    $deployed = (Get-Content (Join-Path $state 'deployed.txt') -ErrorAction SilentlyContinue | Select-Object -First 1) -split ' ' | Select-Object -First 1
    if ($deployed -and $head.StartsWith($deployed)) {
        if ($DryRun) { Write-Log "$short is already deployed: nothing to do." }
        return
    }
    $skipped = Get-Content (Join-Path $state 'rollback-skip.txt') -ErrorAction SilentlyContinue | Select-Object -First 1
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
        $marker = "CI $($run.conclusion) for $short"
        if (-not (Select-String -Path $log -SimpleMatch $marker -Quiet -ErrorAction SilentlyContinue)) { Write-Log "$marker ($($run.html_url)): not deploying." }
        return
    }

    $branch = "$(Invoke-Git branch --show-current)".Trim()
    if ($branch -ne 'main') { Write-Log "Checkout is on '$branch', not main: not deploying $short."; return }
    if (Invoke-Git status --porcelain --untracked-files=no) { Write-Log "Checkout has uncommitted changes: not deploying $short."; return }
    & git -C $Repo merge-base --is-ancestor HEAD origin/main
    if ($LASTEXITCODE) { Write-Log "Checkout has commits that are not on origin/main: not deploying $short."; return }

    if ($DryRun) { Write-Log "Would fast-forward to $short and run deploy.ps1 (CI: $($run.html_url))."; return }

    Invoke-Git merge --ff-only --quiet origin/main | Out-Null
    Write-Log "Deploying $short (CI: $($run.html_url))."
    $output = & (Join-Path $PSScriptRoot 'deploy.ps1') *>&1 | Out-String
    $now = (Get-Content (Join-Path $state 'deployed.txt') -ErrorAction SilentlyContinue | Select-Object -First 1) -split ' ' | Select-Object -First 1
    if (-not ($now -and $head.StartsWith($now))) { throw "deploy.ps1 ended without recording $short (still $now)." }
    Write-Log "Deployed $short."
}
catch {
    Write-Log "FAILED $($_.Exception.Message)$(if ($output) { " Output: $lastOutput" })"
    exit 1
}
finally {
    if ($output) { Set-Content -Path $lastOutput -Value $output -Encoding utf8NoBOM }
}
