$ErrorActionPreference = 'Stop'
$version = '0.5'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @('Engine.cs', 'App.cs', 'Presentation.cs', 'Advanced.cs', 'AdvancedUI.cs', 'AdvancedTests.cs', 'ScanWindow.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compilerPath /nologo /target:winexe /platform:x64 "/out:$PSScriptRoot\AppCare-$version.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:Microsoft.CSharp.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built AppCare-$version.exe"
