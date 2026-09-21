<#
.SYNOPSIS
  Cuts a Tweek Pro release from the terminal: bumps the version everywhere, builds + self-tests, commits, tags v<version>
  and pushes. GitHub Actions (.github/workflows/release.yml) then builds the installer + portable zip on windows-latest
  and publishes the GitHub Release for that tag, which the in-app update check (Update/UpdateCheck.cs) reads.

.DESCRIPTION
  Version is written to TweekPro.csproj (<TweekVersion>), App.cs (MainForm.Version), build.ps1 ($version) and
  installer\TweekPro.iss (#define AppVersion). Files are rewritten with their original UTF-8/BOM encoding so Vietnamese
  text is preserved. Nothing is pushed unless the local build and --self-test pass (self-test needs an elevated shell).

.EXAMPLE
  .\release.ps1                                 # next patch version automatically (0.7.1 -> 0.7.2 -> 0.7.3 ...)
  .\release.ps1 -Notes "Icon Cong cu, Startup Insight, kiem tra cap nhat"
  .\release.ps1 -Version 0.8                    # explicit version (minor/major bump)
  .\release.ps1 -DryRun                         # only validate and show what would change
  .\release.ps1 -SkipTests                      # not recommended; Actions still runs the self-test
#>
[CmdletBinding()]
param(
  [string]$Version = '',
  [string]$Notes = '',
  [string]$Remote = 'origin',
  [switch]$SkipTests,
  [switch]$AllowDirty,
  [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Step($text) { Write-Host "== $text" -ForegroundColor Cyan }
function Fail($text) { Write-Host $text -ForegroundColor Red; exit 1 }
function Run-Git([string[]]$arguments) {
  # git writes progress (push, fetch) to stderr; only the exit code decides success.
  $previous = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
  try { $output = & git.exe @arguments 2>&1 | ForEach-Object { "$_" } } finally { $ErrorActionPreference = $previous }
  if ($LASTEXITCODE -ne 0) { throw "git $($arguments -join ' ') failed: $($output -join "`n")" }
  return $output
}
function HighestTag([string]$remote) {
  $tags = @(Run-Git @('ls-remote', '--tags', '--refs', $remote, 'refs/tags/v*')) + @(Run-Git @('tag', '--list', 'v*'))
  $versions = $tags | ForEach-Object { if ($_ -match '(?:refs/tags/)?v(\d+\.\d+(?:\.\d+)?)\s*$') { PadVersion $Matches[1] } } | Where-Object { $_ }
  if ($versions) { return ($versions | Sort-Object -Descending | Select-Object -First 1) } else { return $null }
}
function PadVersion([string]$v) { $parts = $v.Split('.'); while ($parts.Count -lt 3) { $parts += '0' }; return [version]($parts -join '.') }
function ReplaceInFile([string]$path, [string]$pattern, [string]$replacement) {
  $bytes = [IO.File]::ReadAllBytes($path)
  $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
  $text = [Text.Encoding]::UTF8.GetString($bytes); if ($bom) { $text = $text.Substring(1) }
  if ($text -notmatch $pattern) { throw "Pattern not found in $path : $pattern" }
  $updated = [regex]::Replace($text, $pattern, $replacement, 1)
  if (-not $DryRun) { [IO.File]::WriteAllText($path, $updated, (New-Object Text.UTF8Encoding($bom))) }
  Write-Host "   $path"
}

# --- pick / validate version --------------------------------------------------------------------------------------------
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Fail 'git is not installed.' }
$csproj = Join-Path $PSScriptRoot 'TweekPro.csproj'
$current = ([xml](Get-Content $csproj)).Project.PropertyGroup.TweekVersion | Where-Object { $_ } | Select-Object -First 1
if (-not $current) { Fail 'TweekVersion not found in TweekPro.csproj.' }
if (-not $Version) {
  # Auto-increment: next patch after the highest of the project version and every published v* tag (0.7 -> 0.7.1 -> 0.7.2 ...).
  $base = PadVersion $current
  $highest = HighestTag $Remote
  if ($highest -and $highest -gt $base) { $base = $highest }
  $Version = '{0}.{1}.{2}' -f $base.Major, $base.Minor, ($base.Build + 1)
  Write-Host "No -Version given; next version is $Version (project $current, highest tag $(if ($highest) { "v$highest" } else { 'none' }))." -ForegroundColor Yellow
}
if ($Version -notmatch '^\d+\.\d+(\.\d+)?$') { Fail "Version must look like 0.7.1 (got '$Version')." }
if ((PadVersion $Version) -le (PadVersion $current)) { Fail "New version $Version must be greater than current $current." }
$tag = "v$Version"

# --- validate git state -----------------------------------------------------------------------------------------------
Step "Checking git state (current $current -> $Version, tag $tag)"
$branch = (Run-Git @('rev-parse', '--abbrev-ref', 'HEAD')).Trim()
if ($branch -eq 'HEAD') { Fail 'Detached HEAD; check out a branch first.' }
$dirty = @(Run-Git @('status', '--porcelain', '--untracked-files=no'))
if ($dirty.Count -gt 0 -and -not $AllowDirty) { Fail "Working tree has uncommitted changes; commit or stash them first (or pass -AllowDirty):`n$($dirty -join "`n")" }
Run-Git @('fetch', $Remote, '--tags', '--quiet') | Out-Null
$upstreamExists = $true
try { Run-Git @('rev-parse', '--verify', '--quiet', "$Remote/$branch") | Out-Null } catch { $upstreamExists = $false }
if ($upstreamExists) {
  $behind = [int](Run-Git @('rev-list', '--count', "HEAD..$Remote/$branch"))
  if ($behind -gt 0) { Fail "Local $branch is $behind commit(s) behind $Remote/$branch; pull first." }
}
if ((Run-Git @('tag', '--list', $tag)) -or (Run-Git @('ls-remote', '--tags', $Remote, "refs/tags/$tag"))) { Fail "Tag $tag already exists." }
$remoteUrl = (Run-Git @('remote', 'get-url', $Remote)).Trim()
$repoPath = if ($remoteUrl -match 'github\.com[:/](?<path>[^/]+/[^/]+?)(\.git)?$') { $Matches.path } else { $null }
$userName = (& git.exe config user.name); $userEmail = (& git.exe config user.email)
$identity = @()
if (-not $userName -or -not $userEmail) {
  $owner = if ($repoPath) { $repoPath.Split('/')[0] } else { $env:USERNAME }
  $identity = @('-c', "user.name=$owner", '-c', "user.email=$owner@users.noreply.github.com")
  Write-Host "   git user.name/email not configured; committing as $owner <$owner@users.noreply.github.com>" -ForegroundColor Yellow
}
Write-Host "   branch $branch, remote $remoteUrl"

# --- bump version -----------------------------------------------------------------------------------------------------
Step ("Writing version $Version" + $(if ($DryRun) { ' (dry run, no files changed)' } else { '' }))
ReplaceInFile $csproj '<TweekVersion>[^<]+</TweekVersion>' "<TweekVersion>$Version</TweekVersion>"
ReplaceInFile (Join-Path $PSScriptRoot 'App.cs') 'public const string Version="[^"]+";' "public const string Version=`"$Version`";"
ReplaceInFile (Join-Path $PSScriptRoot 'build.ps1') "\`$version = '[^']+'" "`$version = '$Version'"
ReplaceInFile (Join-Path $PSScriptRoot 'installer\TweekPro.iss') '#define AppVersion "[^"]+"' "#define AppVersion `"$Version`""
if ($DryRun) { Write-Host "Dry run complete. Would commit, tag $tag and push to $Remote/$branch." -ForegroundColor Green; exit 0 }

# --- build + self-test ------------------------------------------------------------------------------------------------
$elevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $SkipTests -and -not $elevated) {
  & git.exe checkout -- $csproj App.cs build.ps1 installer/TweekPro.iss 2>$null
  Fail 'The self-test needs an elevated PowerShell (the exe manifest requires administrator). Re-run from "Run as administrator", or pass -SkipTests.'
}
Step ('Building' + $(if ($SkipTests) { ' (self-test skipped)' } else { ' and running --self-test' }))
$buildArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'build.ps1'))
if (-not $SkipTests) { $buildArgs += '-SelfTest' }
& powershell @buildArgs
if ($LASTEXITCODE -ne 0) {
  & git.exe checkout -- $csproj App.cs build.ps1 installer/TweekPro.iss 2>$null
  Fail 'Build or self-test failed; version bump reverted, nothing committed.'
}

# --- commit, tag, push ------------------------------------------------------------------------------------------------
Step "Committing and tagging $tag"
Run-Git @('add', '--', 'TweekPro.csproj', 'App.cs', 'build.ps1', 'installer/TweekPro.iss') | Out-Null
$message = "Release $tag"; if ($Notes) { $message += "`n`n$Notes" }
Run-Git ($identity + @('commit', '--quiet', '-m', $message)) | Out-Null
Run-Git ($identity + @('tag', '-a', $tag, '-m', "Tweek Pro $Version$(if ($Notes) { "`n`n$Notes" })")) | Out-Null
Step "Pushing $branch and $tag to $Remote"
Run-Git @('push', $Remote, "HEAD:refs/heads/$branch") | Out-Null
Run-Git @('push', $Remote, "refs/tags/$tag") | Out-Null

Write-Host ''
Write-Host "Released $tag from $branch." -ForegroundColor Green
if ($repoPath) {
  Write-Host "  Actions (builds installer + zip, publishes the Release): https://github.com/$repoPath/actions"
  Write-Host "  Release page (ready in a few minutes):                    https://github.com/$repoPath/releases/tag/$tag"
}
Write-Host "  Local exe: bin\Release\net48\TweekPro-$Version.exe"
Write-Host '  The in-app "Kiem tra cap nhat" button reads releases/latest, so it reports this version once Actions finishes.'
