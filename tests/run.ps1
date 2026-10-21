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
# Sample art: every game's pictures exist and the bright ones are bright (generates all art into a temp folder, ~20 s).
$artTests = Join-Path $PSScriptRoot 'sample-art.tests.ps1'
if (Test-Path $artTests) {
    & pwsh -NoProfile -ExecutionPolicy Bypass -File $artTests | Select-String -Pattern '^(FAIL|sample-art)' | ForEach-Object { $_.Line }
    if ($LASTEXITCODE -ne 0) { $code = [Math]::Max($code, 1) }
}
# Screenshot manifest: valid, and every picture of the READMEs / submission files is listed (tools/refresh-screenshots.ps1 -Check -NoDisk).
$shotTests = Join-Path $PSScriptRoot 'screenshots.tests.ps1'
if (Test-Path $shotTests) {
    & pwsh -NoProfile -ExecutionPolicy Bypass -File $shotTests | Select-String -Pattern '^(FAIL|screenshots)' | ForEach-Object { $_.Line }
    if ($LASTEXITCODE -ne 0) { $code = [Math]::Max($code, 1) }
}
# The option-home rules (theme\Modula\ui-layout.yaml) need PyYAML: MODULA_PYTHON, the project venv next to the repository, or any python that has it.
$yamlPython = $null
foreach ($candidate in @($env:MODULA_PYTHON, (Join-Path (Split-Path $root -Parent) '.venv\Scripts\python.exe'), (Join-Path (Split-Path (Split-Path $root -Parent) -Parent) '.venv\Scripts\python.exe'), 'python')) {
    if (-not $candidate) { continue }
    $found = Get-Command $candidate -ErrorAction SilentlyContinue
    if ($found) { & $found.Source -I -c 'import yaml' 2>$null; if ($LASTEXITCODE -eq 0) { $yamlPython = $found.Source; break } }
}
$layoutTests = Join-Path $PSScriptRoot 'ui_layout_test.py'
if ($yamlPython -and (Test-Path $layoutTests)) {
    & $yamlPython -I $layoutTests | Select-String -Pattern '^(FAIL|ui-layout: \d+ passed)' | ForEach-Object { $_.Line }
    if ($LASTEXITCODE -ne 0) { $code = [Math]::Max($code, 1) }
}
elseif (Test-Path $layoutTests) { Write-Warning 'no python with PyYAML found (set MODULA_PYTHON): tests\ui_layout_test.py skipped' }
exit $code
