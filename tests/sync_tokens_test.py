#!/usr/bin/env python3
"""Tests of tools/sync_tokens.py (needs PyYAML, so it runs with the project venv, not from tests/run.ps1):
   python tests/sync_tokens_test.py   (from the repository root, with a Python that has PyYAML)
"""
import os, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, 'tools'))
import sync_tokens

failed = 0


def check(name, cond, detail=''):
    global failed
    print(('PASS ' if cond else 'FAIL ') + name + ('' if cond else ' - ' + detail))
    if not cond:
        failed += 1


# the generated blocks are up to date and a second run changes nothing (idempotent)
r = subprocess.run([sys.executable, os.path.join(ROOT, 'tools', 'sync_tokens.py'), '--check'], capture_output=True, text=True)
check('generated blocks are up to date', r.returncode == 0, r.stdout)

# scale: the Fullscreen default is the Desktop default times the scale, clamped to max
tok = {'type': 'CornerRadius', 'scale': 0.62}
check('radius scale', sync_tokens.scaled('26', tok) == '16', sync_tokens.scaled('26', tok))
check('radius zero stays zero', sync_tokens.scaled('0', tok) == '0')
tok = {'type': 'Double', 'scale': 7.6667, 'max': 1}
check('strength is clamped to max', sync_tokens.scaled('0.2', tok) == '1', sync_tokens.scaled('0.2', tok))
check('strength default', sync_tokens.scaled('0.12', tok) == '0.92', sync_tokens.scaled('0.12', tok))
check('light end of a colour', sync_tokens.mix_white('A78BFA') == 'D0C2FC', sync_tokens.mix_white('A78BFA'))

# every generated constant exists in the Fullscreen Constants.xaml with the generated value
g = sync_tokens.Gen()
f = sync_tokens.constants_defaults(sync_tokens.FULL)
for name, const, value, kind in g.generated():
    check('constant ' + const, f.get(const) is not None and float(f[const]) == float(value), '%s vs %s' % (f.get(const), value))

# the Desktop presets are copied: every Desktop choice of a shared group exists in the Fullscreen group
import yaml
dy = yaml.safe_load(open(os.path.join(sync_tokens.DESKTOP, 'options.yaml'), encoding='utf-8-sig'))
fy = yaml.safe_load(open(os.path.join(sync_tokens.FULL, 'options.yaml'), encoding='utf-8-sig'))
for group in sync_tokens.SHARED_GROUPS:
    missing = [c for c in dy['Presets'][group]['Presets'] if c not in fy['Presets'][group]['Presets']]
    check('group ' + group + ' has every Desktop choice', not missing, str(missing))

print('%d failed' % failed)
sys.exit(1 if failed else 0)
