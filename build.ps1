# Builds Tweek Pro 0.7 on Windows with the .NET SDK (net48 target, no runtime install needed to run the result).
# Usage:  powershell -ExecutionPolicy Bypass -File .\build.ps1 [-Configuration Release] [-SelfTest]
param(
  [string]$Configuration = 'Release',
  [switch]$SelfTest
)
$ErrorActionPreference = 'Stop'
$version = '0.7.9'
$project = Join-Path $PSScriptRoot 'TweekPro.csproj'

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
  Write-Host ''
  Write-Host 'Khong tim thay .NET SDK (lenh "dotnet").' -ForegroundColor Yellow
  Write-Host 'Tweek Pro 0.7 dung goi NuGet (TraceEvent) nen can .NET SDK 6 tro len de bien dich; ung dung sau khi build vẫn chay tren .NET Framework 4.8 co san trong Windows.'
  Write-Host 'Cai SDK (mot trong hai cach) roi chay lai build.ps1:'
  Write-Host '  1) winget install Microsoft.DotNet.SDK.8'
  Write-Host '  2) https://dotnet.microsoft.com/download  (chon .NET SDK, ban x64)'
  exit 2
}

$sdks = & dotnet --list-sdks 2>$null
if (-not $sdks) {
  Write-Host 'Lenh dotnet co san nhung khong co SDK nao duoc cai (chi co runtime). Cai .NET SDK 8: winget install Microsoft.DotNet.SDK.8' -ForegroundColor Yellow
  exit 2
}

Write-Host "Dang build Tweek Pro $version ($Configuration) bang dotnet build..."
& dotnet build $project -c $Configuration -nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$outDir = Join-Path $PSScriptRoot "bin\$Configuration\net48"
$exe = Join-Path $outDir "TweekPro-$version.exe"
if (-not (Test-Path $exe)) { throw "Khong thay $exe sau khi build." }
Write-Output "Built $exe"
Write-Output "Thu muc phan phoi (copy toan bo, gom cac DLL TraceEvent): $outDir"

if ($SelfTest) {
  Write-Host 'Dang chay --self-test (tao va don fixture TweekProTest* trong AppData va HKCU)...'
  $results = Join-Path $outDir 'test-results.txt'
  if (Test-Path $results) { Remove-Item $results -Force }
  $proc = Start-Process -FilePath $exe -ArgumentList '--self-test' -Wait -PassThru
  $code = $proc.ExitCode
  if (Test-Path $results) { Get-Content $results } else { Write-Host 'test-results.txt was not written.' -ForegroundColor Yellow; if ($code -eq 0) { $code = 1 } }
  if ($code -ne 0) { throw 'Self-test failed.' }
}
