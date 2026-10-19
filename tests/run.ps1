# Builds and runs the test harness. Usage: tests\run.ps1 [name-filter]
# Without a filter, the local-paths helper tests (tests\local-paths.tests.ps1, tests\local_paths_test.py) run after the C# summary.
param([string]$Filter)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $root 'tools\local-paths.ps1')
& (Join-Path $root 'tools\build.ps1') -Target tests
$exe = Join-Path (Get-LocalPath build) 'tests.exe'
$env:MODULA_REPO = $root   # tests.exe lives outside the repository (TestPaths.RepoRoot reads this); this process only
if ($Filter) { & $exe $Filter; exit $LASTEXITCODE }
& $exe
$code = $LASTEXITCODE
$psTests = Join-Path $PSScriptRoot 'local-paths.tests.ps1'
if (Test-Path $psTests) {
    & pwsh -NoProfile -File $psTests | Select-String -Pattern '^(FAIL|local-paths)' | ForEach-Object { $_.Line }
    if ($LASTEXITCODE -ne 0) { $code = [Math]::Max($code, 1) }
}
$pyTests = Join-Path $PSScriptRoot 'local_paths_test.py'
if ((Test-Path $pyTests) -and (Get-Command python -ErrorAction SilentlyContinue)) {
    & python -I $pyTests | Select-String -Pattern '^(FAIL|local-paths)' | ForEach-Object { $_.Line }
    if ($LASTEXITCODE -ne 0) { $code = [Math]::Max($code, 1) }
}
elseif (Test-Path $pyTests) { Write-Warning 'python not found: tests\local_paths_test.py skipped' }
exit $code
