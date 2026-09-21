# Pulls the latest Tweek Pro source into this folder (or clones it when empty), builds, runs the self-tests and optionally launches the app.
# Usage (from any PowerShell window):
#   powershell -ExecutionPolicy Bypass -File C:\Users\ADMIN\TweekPro\get-latest.ps1 [-Branch cursor/integration-all-features-e772] [-Run]
# Or, when the folder does not exist yet:
#   git clone https://github.com/tuantien0001/TweekPro.git C:\Users\ADMIN\TweekPro; then run the line above.
param(
  [string]$Branch = 'cursor/integration-all-features-e772',
  [string]$Repo = 'https://github.com/tuantien0001/TweekPro.git',
  [string]$Folder = $PSScriptRoot,
  [switch]$Run
)
$ErrorActionPreference = 'Stop'

function Require($name, $hint) {
  if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
    Write-Host "Missing '$name'. $hint" -ForegroundColor Yellow
    exit 2
  }
}
Require git 'Install with: winget install Git.Git'
Require dotnet 'Install with: winget install Microsoft.DotNet.SDK.8'

if (-not (Test-Path (Join-Path $Folder '.git'))) {
  Write-Host "No git repository in $Folder; cloning $Repo ..."
  git clone $Repo $Folder
  if ($LASTEXITCODE -ne 0) { throw 'git clone failed.' }
}
Set-Location $Folder

Write-Host "Fetching origin/$Branch ..."
git fetch origin $Branch
if ($LASTEXITCODE -ne 0) { throw 'git fetch failed.' }

$dirty = git status --porcelain
if ($dirty) {
  Write-Host 'Local changes detected; stashing them as "get-latest autostash" so the update cannot overwrite your work.' -ForegroundColor Yellow
  git stash push -u -m 'get-latest autostash' | Out-Null
  $stashed = $true
}

$exists = git branch --list $Branch
if ($exists) { git checkout $Branch } else { git checkout -b $Branch "origin/$Branch" }
if ($LASTEXITCODE -ne 0) { throw 'git checkout failed.' }
git pull --ff-only origin $Branch
if ($LASTEXITCODE -ne 0) { throw 'git pull failed (non fast-forward). Resolve manually with git status.' }

Write-Host ''
Write-Host ('Now at ' + (git log --oneline -1))
if ($stashed) {
  Write-Host 'Re-applying your stashed local changes...'
  git stash pop
  if ($LASTEXITCODE -ne 0) { Write-Host 'Stash pop had conflicts; your changes are kept in "git stash list" as "get-latest autostash".' -ForegroundColor Yellow }
}
Write-Host ''

# The exe carries a requireAdministrator manifest, so --self-test only works from an elevated PowerShell.
$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($elevated) {
  & powershell -ExecutionPolicy Bypass -File (Join-Path $Folder 'build.ps1') -Configuration Release -SelfTest
} else {
  Write-Host 'Not running as administrator: building only. Re-run this script from an elevated PowerShell to also execute --self-test.' -ForegroundColor Yellow
  & powershell -ExecutionPolicy Bypass -File (Join-Path $Folder 'build.ps1') -Configuration Release
}
if ($LASTEXITCODE -ne 0) { throw 'Build or self-test failed; see output above.' }

$exe = Join-Path $Folder 'bin\Release\net48\TweekPro-0.7.exe'
Write-Host ''
Write-Host "Build OK: $exe" -ForegroundColor Green
Write-Host 'Self-test results: bin\Release\net48\test-results.txt'
Write-Host 'Next steps for an agent: read HANDOFF.md and AGENTS.md in this folder.'
if ($Run) {
  Write-Host 'Launching Tweek Pro...'
  Start-Process $exe
}
