# Builds and runs the test harness. Usage: tests\run.ps1 [name-filter]
param([string]$Filter)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $root 'tools\build.ps1') -Target tests
$exe = Join-Path $root 'build\tests.exe'
if ($Filter) { & $exe $Filter } else { & $exe }
exit $LASTEXITCODE
