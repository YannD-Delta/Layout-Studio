#!/usr/bin/env python3
"""Where the heavy, per-PC folders live (backup/ build/ testbed/ exchange/): a LOCAL ROOT outside the repository.
   local_root()                 the local root
   local_path(name, create=False)   a folder under it (create=True makes it, never for testbed)
Order: 1. MODULA_LOCAL if set and the folder exists: an ABSOLUTE path (C:\\... or \\\\server\\share; quotes and a trailing
          slash are tolerated, C:\\ stays C:\\); a relative value (. foo ..\\x ~\\x C:) is ignored with a warning.
          Empty in this process: the User-scope value is read (Windows registry; a shell started before the variable existed).
       2. %USERPROFILE%/Documents/Playnite if it exists, with one info line on stderr naming it; refused (warning) when it
          holds .git, layout-studio, Playnite.DesktopApp.exe or theme (a code clone or a Playnite install)
       3. the repository root, with a warning on stderr (the old layout: the folders stay git-ignored there)
MODULA_LOCAL_NO_USER_LOOKUP=1 skips the User-scope read (the tests set it so "unset" stays unset).
Same logic in PowerShell: tools/local-paths.ps1.
The public Layout Studio repository gets a copy with PUBLIC = True (tools/export-public.py): there, step 2 is skipped and
nothing is printed (MODULA_LOCAL if usable, else the repository root).

The tools run as `python -I`, which does not put the script's folder on sys.path, so a tool imports this module with:
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import local_paths
   python -I tools/local_paths.py     prints the resolved paths as JSON
"""
import json, os, re, sys

PUBLIC = True   # True in the public copy written by tools/export-public.py (this exact line)
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NAMES = ('backup', 'build', 'testbed', 'exchange')
# a Documents/Playnite holding one of these is a code clone or a Playnite install, not a local root
NOT_A_ROOT = ('.git', 'layout-studio', 'Playnite.DesktopApp.exe', 'theme')
_warned = set()


def _warn(message):
    # once per message and per run; never in the public copy
    if not PUBLIC and message not in _warned:
        _warned.add(message)
        print('WARNING: ' + message, file=sys.stderr)


def _note(message):
    # an informational line, once per run, on stderr (stdout stays clean for the tools' own output); never in the public copy
    if not PUBLIC and message not in _warned:
        _warned.add(message)
        print(message, file=sys.stderr)


# absolute only: C:\... or \\server\share (C: alone is the current folder of that drive; . foo ~ depend on where the script runs)
_ABSOLUTE = re.compile(r'^(?:[A-Za-z]:[\\/]|[\\/]{2}[^\\/]+[\\/][^\\/]+)')


def _candidate(raw):
    """-> (path, None) normalised (a drive root keeps its separator, other trailing ones go), (None, problem), or (None, None) if blank."""
    v = (raw or '').strip().strip('"').strip()
    if not v:
        return None, None
    if not (_ABSOLUTE.match(v) or (os.sep == '/' and v.startswith('/'))):
        return None, "MODULA_LOCAL is not an absolute path (C:\\... or \\\\server\\share): '%s' ignored" % v
    path = os.path.normpath(v)
    drive, rest = os.path.splitdrive(path)
    if drive[:2] in ('\\\\', '//') and not rest.strip('\\/'):
        path = drive   # a UNC root: \\server\share, like PowerShell
    return path, None


def _user_value():
    """The User-scope MODULA_LOCAL (HKCU\\Environment), '' off Windows, when there is none or it cannot be read."""
    try:
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, 'Environment') as key:
            value, kind = winreg.QueryValueEx(key, 'MODULA_LOCAL')
        if kind == winreg.REG_EXPAND_SZ:
            value = winreg.ExpandEnvironmentStrings(value)
        return value if isinstance(value, str) else ''
    except Exception:
        return ''


def local_root():
    raw = os.environ.get('MODULA_LOCAL', '')
    if not raw.strip() and os.environ.get('MODULA_LOCAL_NO_USER_LOOKUP') != '1':
        raw = _user_value()   # a shell started before the variable was created has it only in the registry
    path, problem = _candidate(raw)
    if problem:
        _warn(problem)
    elif path:
        if os.path.isdir(path):
            return path
        _warn('MODULA_LOCAL points to a missing folder (%s): ignored' % path)
    profile = '' if PUBLIC else os.environ.get('USERPROFILE', '')
    if profile:
        docs = os.path.join(profile, 'Documents', 'Playnite')
        if os.path.isdir(docs):
            docs = os.path.abspath(docs)
            # a code clone or a (portable) Playnite of that name must not receive testbed/ and build/
            marker = next((m for m in NOT_A_ROOT if os.path.exists(os.path.join(docs, m))), None)
            if marker:
                _warn('%s contains %s (a code clone or a Playnite install?): not used as the local root, set MODULA_LOCAL' % (docs, marker))
            else:
                _note('local root: %s (%%USERPROFILE%%\\Documents\\Playnite; set MODULA_LOCAL to choose another)' % docs)
                return docs
    _warn('no local folder (set MODULA_LOCAL or create %USERPROFILE%\\Documents\\Playnite): using the repo root '
          '(backup/ build/ testbed/ are git-ignored there)')
    return REPO


def local_path(name, create=False):
    if name not in NAMES:
        raise ValueError('unknown local folder: %r (expected one of %s)' % (name, ', '.join(NAMES)))
    path = os.path.join(local_root(), name)
    if create and name != 'testbed':
        os.makedirs(path, exist_ok=True)
    return path


if __name__ == '__main__':
    print(json.dumps({'root': local_root(), **{n: local_path(n) for n in ('backup', 'build', 'testbed')}}))
