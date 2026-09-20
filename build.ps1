$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compilerPath /nologo /target:winexe /platform:x64 "/out:$PSScriptRoot\AppCare-0.4.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:Microsoft.CSharp.dll "$PSScriptRoot\Engine.cs" "$PSScriptRoot\App.cs" "$PSScriptRoot\Presentation.cs" "$PSScriptRoot\Advanced.cs" "$PSScriptRoot\AdvancedUI.cs" "$PSScriptRoot\AdvancedTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output 'Built AppCare-0.4.exe'
