# Builds, tests and publishes frtune as one self-contained frtune.exe in .\publish
# Usage:  .\build.ps1            (Release build + tests + publish)
#         .\build.ps1 -SkipTests
param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet build Frtune.sln -c Release
if (-not $SkipTests) { dotnet test tests/Frtune.Core.Tests -c Release --no-build }
dotnet publish src/Frtune.App -c Release -r win-x64 -o publish

Write-Host ""
Write-Host "Done: $(Resolve-Path publish\frtune.exe)" -ForegroundColor Green
