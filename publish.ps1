<#
.SYNOPSIS
  Builds the Release exe, runs the self-tests, then produces two distributables in .\dist:
    TweekPro-<version>-portable.zip  (unzip and run)
    TweekPro-<version>-Setup.exe     (normal Windows installer, requires Inno Setup 6.3+)

.DESCRIPTION
  The exe manifest requires administrator, so the self-test does too: when started from a normal PowerShell the script
  relaunches itself elevated (UAC prompt) and keeps that window open until you press Enter. Pass -SkipTests to stay unelevated.
  Inno Setup is installed automatically via winget when -InstallInno is passed; otherwise it is looked up in the usual locations.

.EXAMPLE
  .\publish.ps1                 # build + test + zip + installer
  .\publish.ps1 -SkipTests      # faster, no self-test
  .\publish.ps1 -InstallInno    # install Inno Setup with winget first
#>
[CmdletBinding()]
param(
  [switch]$SkipTests,
  [switch]$InstallInno,
  [switch]$NoInstaller,
  [switch]$Child
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $elevated -and -not $SkipTests) {
  # The exe manifest requires administrator, so the self-test can only run elevated: relaunch this script through UAC and wait for it.
  Write-Host 'Self-tests need administrator rights; requesting elevation (UAC)...' -ForegroundColor Yellow
  $forward = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-Child')
  if ($InstallInno) { $forward += '-InstallInno' }
  if ($NoInstaller) { $forward += '-NoInstaller' }
  $child = Start-Process -FilePath 'powershell.exe' -ArgumentList $forward -Verb RunAs -PassThru -Wait
  exit $child.ExitCode
}

$failed = $false
try {
$version = ([xml](Get-Content .\TweekPro.csproj)).Project.PropertyGroup.TweekVersion | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'TweekVersion not found in TweekPro.csproj.' }
$outDir = Join-Path $PSScriptRoot 'bin\Release\net48'
$dist = Join-Path $PSScriptRoot 'dist'
$exe = Join-Path $outDir "TweekPro-$version.exe"
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host "== Building Tweek Pro $version (Release)" -ForegroundColor Cyan
dotnet build -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }

if (-not $SkipTests) {
  Write-Host '== Running self-tests' -ForegroundColor Cyan
  $results = Join-Path $outDir 'test-results.txt'
  if (Test-Path $results) { Remove-Item $results -Force }
  $proc = Start-Process -FilePath $exe -ArgumentList '--self-test' -Wait -PassThru
  if (Test-Path $results) { Get-Content $results }
  if ($proc.ExitCode -ne 0 -or -not (Test-Path $results)) { throw "Self-tests failed ($($proc.ExitCode))." }
}

Write-Host '== Refreshing TweekPro.ico from the code-drawn logo' -ForegroundColor Cyan
Start-Process -FilePath $exe -ArgumentList @('--export-icon', (Join-Path $PSScriptRoot 'TweekPro.ico')) -Wait | Out-Null

Write-Host '== Portable zip' -ForegroundColor Cyan
$zip = Join-Path $dist "TweekPro-$version-portable.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
$stage = Join-Path $dist 'stage'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$exclude = @('test-results.txt', 'TweekPro-preview.png', 'preview-error.txt', 'scan-smoke.txt')
Get-ChildItem $outDir -Recurse -File | Where-Object { $exclude -notcontains $_.Name -and $_.Extension -notin '.pdb', '.xml' -and $_.Name -notlike 'mono_crash*' } | ForEach-Object {
  $target = Join-Path $stage $_.FullName.Substring($outDir.Length).TrimStart('\')
  New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
  Copy-Item $_.FullName $target
}
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force
Write-Host "   $zip"

if ($NoInstaller) { return }

$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $iscc) {
  $candidates = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")
  $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc -and $InstallInno) {
  Write-Host '== Installing Inno Setup via winget' -ForegroundColor Cyan
  winget install --id JRSoftware.InnoSetup -e --accept-source-agreements --accept-package-agreements --silent
  $iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) {
  Write-Host 'Inno Setup not found. Install it with: winget install JRSoftware.InnoSetup   (or re-run with -InstallInno)' -ForegroundColor Yellow
  Write-Host 'Portable zip was produced; installer skipped.' -ForegroundColor Yellow
  return
}

Write-Host '== Compiling installer' -ForegroundColor Cyan
& $iscc "/DAppVersion=$version" "/DSourceDir=$outDir" (Join-Path $PSScriptRoot 'installer\TweekPro.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed ($LASTEXITCODE)." }
Write-Host "   $(Join-Path $dist "TweekPro-$version-Setup.exe")"
Write-Host 'Done. Share dist\TweekPro-*-Setup.exe (installer) or dist\TweekPro-*-portable.zip (no install).' -ForegroundColor Green
}
catch {
  Write-Host $_ -ForegroundColor Red
  $failed = $true
}
finally {
  if ($Child) { Read-Host 'Press Enter to close this window' | Out-Null }
}
if ($failed) { exit 1 }
