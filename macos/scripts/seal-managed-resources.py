"""Seal managed PE assemblies/data skipped by rcodesign's default nested-code rule.

PE assemblies are resources for Apple's code-signing system. Keep native nested
code records and add SHA-256 seals for every other app resource. The main Mach-O
must then be re-signed with this CodeResources file and the app Info.plist.
"""
import argparse, hashlib, pathlib, plistlib, re

parser = argparse.ArgumentParser()
parser.add_argument('app', type=pathlib.Path)
args = parser.parse_args()
contents = args.app/'Contents'
path = contents/'_CodeSignature/CodeResources'
with path.open('rb') as source:
    resources = plistlib.load(source)
files = resources.setdefault('files2', {})
rules = resources.setdefault('rules2', {})
sealed = 0
for file in sorted(contents.rglob('*')):
    if not file.is_file():
        continue
    relative = file.relative_to(contents).as_posix()
    if relative in ('Info.plist','MacOS/Rovunet') or relative.startswith('_CodeSignature/'):
        continue
    with file.open('rb') as source:
        magic = source.read(4)
    if magic in (b'\xcf\xfa\xed\xfe', b'\xca\xfe\xba\xbe', b'\xca\xfe\xba\xbf'):
        if 'cdhash' not in files.get(relative, {}):
            raise RuntimeError('Missing nested native code seal: '+relative)
        continue
    files[relative] = {'hash2':hashlib.sha256(file.read_bytes()).digest()}
    if relative.startswith('MacOS/'):
        rules['^'+re.escape(relative)+'$'] = {'weight':50.0}
    sealed += 1
with path.open('wb') as destination:
    plistlib.dump(resources,destination,sort_keys=True)
print('Sealed '+str(sealed)+' managed assemblies/data resources; re-sign main executable next.')
