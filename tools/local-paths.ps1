# Where the heavy, per-PC folders live (backup\ build\ testbed\ exchange\): a LOCAL ROOT outside the repository.
# Dot-source it from a script:   . (Join-Path $PSScriptRoot 'local-paths.ps1')
#   Get-LocalRoot                        the local root
#   Get-LocalPath backup|build|testbed|exchange [-Create]   a folder under it (-Create makes it, never for testbed)
#   Resolve-LocalOutPath <path>          a script's -Out: relative = under the local root, absolute = kept
# Order: 1. $env:MODULA_LOCAL if set and the folder exists: an ABSOLUTE path (C:\... or \\server\share; quotes and a trailing \
#           are tolerated, C:\ stays C:\); a relative value (. foo ..\x ~\x C:) is ignored with a warning.
#           Empty in this process: the User-scope value is read (a shell started before the variable existed).
#        2. %USERPROFILE%\Documents\Playnite if it exists, with one info line naming it; refused (warning) when it holds
#           .git, layout-studio, Playnite.DesktopApp.exe or theme (a code clone or a Playnite install)
#        3. the repository root, with a warning (the old layout: the folders stay git-ignored there)
# MODULA_LOCAL_NO_USER_LOOKUP=1 skips the User-scope read (the tests set it so "unset" stays unset).
# Same logic in Python: tools\local_paths.py.
# The public Layout Studio repository gets a copy with the flag below set to $true (tools\export-public.py): there, step 2 is
# skipped and nothing is printed (MODULA_LOCAL if usable, else the repository root).

$script:LocalPathsPublic = $true   # $true in the public copy written by tools\export-public.py (this exact line)

$script:LocalPathsWarned = @{}
function Write-LocalPathsWarning([string]$Message) {
    # once per message and per script run (Get-LocalPath calls Get-LocalRoot each time); never in the public copy
    if ($script:LocalPathsPublic) { return }
    if (-not $script:LocalPathsWarned) { $script:LocalPathsWarned = @{} }
    if (-not $script:LocalPathsWarned.ContainsKey($Message)) { $script:LocalPathsWarned[$Message] = $true; Write-Warning $Message }
}
function Write-LocalPathsNote([string]$Message) {
    # an informational line, once per script run (host only: never in the output of Get-LocalRoot); never in the public copy
    if ($script:LocalPathsPublic) { return }
    if (-not $script:LocalPathsWarned) { $script:LocalPathsWarned = @{} }
    if (-not $script:LocalPathsWarned.ContainsKey($Message)) { $script:LocalPathsWarned[$Message] = $true; Write-Host $Message }
}

# a Documents\Playnite holding one of these is a code clone or a Playnite install, not a local root
$script:LocalPathsNotARoot = '.git', 'layout-studio', 'Playnite.DesktopApp.exe', 'theme'

function Get-LocalPathsUserValue {
    # the User-scope MODULA_LOCAL (registry), '' when there is none or it cannot be read
    try { [string][Environment]::GetEnvironmentVariable('MODULA_LOCAL', 'User') } catch { '' }
}

function ConvertTo-LocalRootCandidate([string]$Raw) {
    # -> .Path (normalised; a drive root keeps its \, other trailing separators go) or .Problem (why it is refused); both $null = blank
    $v = "$Raw".Trim().Trim('"').Trim()
    if (-not $v) { return [pscustomobject]@{ Path = $null; Problem = $null } }
    # absolute only: C:\... or \\server\share (C: alone is the current folder of that drive; . foo ~ depend on where the script runs)
    $absolute = ($v -match '^[A-Za-z]:[\\/]') -or ($v -match '^[\\/]{2}[^\\/]+[\\/][^\\/]+') -or ([IO.Path]::DirectorySeparatorChar -eq '/' -and $v.StartsWith('/'))
    if (-not $absolute) { return [pscustomobject]@{ Path = $null; Problem = "MODULA_LOCAL is not an absolute path (C:\... or \\server\share): '$v' ignored" } }
    try { $full = [IO.Path]::GetFullPath($v) }
    catch { return [pscustomobject]@{ Path = $null; Problem = "MODULA_LOCAL is not a valid path: '$v' ignored" } }
    $top = [IO.Path]::GetPathRoot($full)
    $trimmed = $full.TrimEnd('\', '/')
    $path = if ($trimmed.Length -le $top.TrimEnd('\', '/').Length) { $top } else { $trimmed }
    [pscustomobject]@{ Path = $path; Problem = $null }
}

function Get-LocalRoot {
    [CmdletBinding()]
    param()
    $repo = Split-Path $PSScriptRoot -Parent
    $raw = $env:MODULA_LOCAL
    # a shell started before the variable was created has it only in the registry
    if (-not "$raw".Trim() -and $env:MODULA_LOCAL_NO_USER_LOOKUP -ne '1') { $raw = Get-LocalPathsUserValue }
    $c = ConvertTo-LocalRootCandidate $raw
    if ($c.Problem) { Write-LocalPathsWarning $c.Problem }
    elseif ($c.Path) {
        if (Test-Path -LiteralPath $c.Path -PathType Container) { return $c.Path }
        Write-LocalPathsWarning "MODULA_LOCAL points to a missing folder ($($c.Path)): ignored"
    }
    if (-not $script:LocalPathsPublic -and $env:USERPROFILE) {
        $docs = Join-Path $env:USERPROFILE 'Documents\Playnite'
        if (Test-Path -LiteralPath $docs -PathType Container) {
            $docs = (Resolve-Path -LiteralPath $docs).ProviderPath.TrimEnd('\')
            # a code clone or a (portable) Playnite of that name must not receive testbed\ and build\
            $marker = $script:LocalPathsNotARoot | Where-Object { Test-Path -LiteralPath (Join-Path $docs $_) } | Select-Object -First 1
            if ($marker) { Write-LocalPathsWarning "$docs contains $marker (a code clone or a Playnite install?): not used as the local root, set MODULA_LOCAL" }
            else {
                Write-LocalPathsNote "local root: $docs (%USERPROFILE%\Documents\Playnite; set MODULA_LOCAL to choose another)"
                return $docs
            }
        }
    }
    Write-LocalPathsWarning 'no local folder (set MODULA_LOCAL or create %USERPROFILE%\Documents\Playnite): using the repo root (backup\ build\ testbed\ are git-ignored there)'
    return $repo
}

function Get-LocalPath {
    [CmdletBinding()]
    param([Parameter(Mandatory, Position = 0)][ValidateSet('backup', 'build', 'testbed', 'exchange')][string]$Name, [switch]$Create)
    $path = Join-Path (Get-LocalRoot) $Name
    if ($Create -and $Name -ne 'testbed' -and -not (Test-Path -LiteralPath $path)) { New-Item -ItemType Directory -Force $path | Out-Null }
    return $path
}

function Resolve-LocalOutPath {
    # the rule for the -Out of the testbed scripts: a relative path is under the local root, an absolute one is kept
    [CmdletBinding()]
    param([Parameter(Mandatory, Position = 0)][string]$Path)
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return (Join-Path (Get-LocalRoot) $Path)
}
