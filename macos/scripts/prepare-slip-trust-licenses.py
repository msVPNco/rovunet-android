"""Collect the isolated Slipstream discovery helper's vendored license texts."""
from pathlib import Path
import shutil

mac = Path(__file__).resolve().parents[1]
source = mac / 'native/slipcert'
target = mac / 'licenses/SlipTrust'
target.mkdir(parents=True, exist_ok=True)
shutil.copyfile(source / 'SOURCE.md', target / 'SOURCE.md')
for item in (source / 'vendor').rglob('*'):
    if item.is_file() and item.name.upper().startswith(('LICENSE', 'COPYING', 'NOTICE', 'PATENTS')):
        destination = target / 'dependencies' / item.relative_to(source / 'vendor')
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(item, destination)
