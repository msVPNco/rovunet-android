#!/usr/bin/env python3
"""Compatibility entry point for the modern Windows WPF review installer."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys
from datetime import datetime

root = Path(__file__).resolve().parents[1]
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet')
p.add_argument('--makensis')
p.add_argument('--output')
p.add_argument('--nsisdir', help=argparse.SUPPRESS)
p.add_argument('--go', help=argparse.SUPPRESS)
a = p.parse_args()
if os.name != 'nt':
    p.error('Build the modern WPF installer on Windows with scripts/package-review.ps1.')

def executable(value, name, local):
    candidate = value or shutil.which(name) or str(root.parents[1] / local)
    if not Path(candidate).is_file():
        p.error(f'{name} was not found; supply its full path.')
    return str(Path(candidate).resolve())

dotnet = executable(a.dotnet, 'dotnet', 'tools/dotnet/dotnet.exe')
nsis = executable(a.makensis, 'makensis', 'tools/nsis-tauri/nsis-3.09/makensis.exe')
shell = shutil.which('pwsh') or shutil.which('powershell')
if not shell:
    p.error('PowerShell is required.')
output = Path(a.output).resolve() if a.output else root / 'artifacts' / ('private-review17-' + datetime.now().strftime('%Y%m%d-%H%M%S'))
sys.exit(subprocess.call([shell, '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
    str(root / 'scripts/package-review.ps1'), '-Dotnet', dotnet, '-MakeNsis', nsis,
    '-EngineDirectory', str(root / 'engines/windows-x64'), '-OutputDirectory', str(output)], cwd=root))
