# Builds the Layout Studio plugin and/or the test harness.
#   tools\build.ps1 [-Target plugin|tests|all]
# Output: <local root>\build\LayoutStudio\LayoutStudio.dll (+ extension.yaml) and <local root>\build\tests.exe (local root: tools\local-paths.ps1)
# Compiler: tools\roslyn\csc.exe (Roslyn toolset, ignored by git); falls back to the legacy C# 5 csc.exe.
param([ValidateSet('plugin', 'tests', 'all')][string]$Target = 'all')

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'local-paths.ps1')
$out = Get-LocalPath build -Create
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$playnite = if ($env:PLAYNITE_DIR) { $env:PLAYNITE_DIR } else { Join-Path $env:LOCALAPPDATA 'Playnite' }

$csc = Join-Path $root 'tools\roslyn\csc.exe'
if (-not (Test-Path $csc)) {
    Write-Warning 'tools\roslyn\csc.exe not found: using the legacy C# 5 compiler'
    $csc = Join-Path $fw 'csc.exe'
}

function Invoke-Csc([string[]]$CompilerArgs) {
    & $csc @CompilerArgs
    if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
}

function Get-Sources([string]$Dir) {
    Get-ChildItem $Dir -Filter *.cs -Recurse | ForEach-Object FullName
}

New-Item -ItemType Directory -Force $out | Out-Null
$srcRoot = Join-Path $root 'layout-studio\src'

if ($Target -in 'tests', 'all') {
    # Core is pure logic; Engine\WpfValues.cs and DependentBrushes.cs only need WPF value types (no UI thread), so they are tested here too.
    $sources = @(Get-Sources (Join-Path $srcRoot 'Core')) + @((Join-Path $srcRoot 'Engine\WpfValues.cs'), (Join-Path $srcRoot 'Engine\DependentBrushes.cs'), (Join-Path $srcRoot 'Engine\OptionsBridge.cs')) + @(Get-Sources (Join-Path $srcRoot 'Editor\ViewModels')) + @(Get-ChildItem (Join-Path $root 'tests') -Filter *.cs | ForEach-Object FullName)
    $wpfTests = @((Join-Path $fw 'WPF\PresentationCore.dll'), (Join-Path $fw 'WPF\PresentationFramework.dll'), (Join-Path $fw 'WPF\WindowsBase.dll')) | ForEach-Object { "-r:$_" }
    Invoke-Csc (@('-nologo', '-target:exe', '-debug', "-out:$(Join-Path $out 'tests.exe')",
            '-r:System.dll', '-r:System.Core.dll', '-r:System.Xml.dll', '-r:System.Runtime.Serialization.dll', '-r:System.Web.Extensions.dll') + $wpfTests + $sources)
    Write-Host "built $(Join-Path $out 'tests.exe')"
}

if ($Target -in 'plugin', 'all') {
    $dest = Join-Path $out 'LayoutStudio'
    New-Item -ItemType Directory -Force $dest | Out-Null
    $wpf = Join-Path $fw 'WPF'
    $refs = @(
        (Join-Path $playnite 'Playnite.SDK.dll'),
        (Join-Path $wpf 'PresentationFramework.dll'), (Join-Path $wpf 'PresentationCore.dll'),
        (Join-Path $wpf 'WindowsBase.dll'), (Join-Path $fw 'System.Xaml.dll'),
        'System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Runtime.Serialization.dll', 'System.Web.Extensions.dll', 'Microsoft.CSharp.dll'
    ) | ForEach-Object { "-r:$_" }
    # XAML files are embedded and loaded at runtime (csc cannot compile BAML): LayoutStudio.<folder>.<file>
    $resources = Get-ChildItem $srcRoot -Filter *.xaml -Recurse | ForEach-Object {
        $rel = $_.FullName.Substring($srcRoot.Length + 1).Replace([IO.Path]::DirectorySeparatorChar, '.')
        "-resource:$($_.FullName),LayoutStudio.$rel"
    }
    Invoke-Csc (@('-nologo', '-target:library', '-debug', "-out:$(Join-Path $dest 'LayoutStudio.dll')") + $refs + $resources + @(Get-Sources $srcRoot))
    Copy-Item (Join-Path $root 'layout-studio\extension.yaml') $dest -Force
    Write-Host "built $(Join-Path $dest 'LayoutStudio.dll')"
}
