param([string]$DotNet = 'dotnet', [switch]$SkipInstaller, [switch]$NativeSmoke)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
function RunDotNet([string[]]$Arguments) { & $DotNet @Arguments; if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $Arguments" } }
RunDotNet @('restore','InputBlocker.sln')
RunDotNet @('build','InputBlocker.sln','-c','Release','--no-restore')
RunDotNet @('test','tests/InputBlocker.Tests','-c','Release','--no-build')
if ($NativeSmoke) { RunDotNet @('run','--project','tests/InputBlocker.SmokeTests','-c','Release') }
RunDotNet @('publish','src/InputBlocker','-c','Release','-r','win-x64','--self-contained','true','-o','artifacts/win-x64','-p:PublishSingleFile=false')
Copy-Item README.md artifacts/win-x64/README.md -Force
Copy-Item docs/MANUAL-TESTS.md artifacts/win-x64/MANUAL-TESTS.md -Force
Copy-Item docs/VERIFICATION.md artifacts/win-x64/VERIFICATION.md -Force
Compress-Archive -Path artifacts/win-x64/* -DestinationPath artifacts/InputBlocker-1.0.0-win-x64.zip -Force
if (!$SkipInstaller) { & "$PSScriptRoot/installer/package.ps1" -DotNet $DotNet }
Get-FileHash artifacts/*.zip, artifacts/*.msi -Algorithm SHA256 -ErrorAction SilentlyContinue | Format-Table
