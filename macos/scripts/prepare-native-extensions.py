"""Fetch pinned public archives and bundle private Darwin runtime dependencies.

Runs on Windows or macOS; edits only the selected macos/engines directory.
Modified Mach-O load commands require signing after this stage.
"""
import argparse, hashlib, json, pathlib, posixpath, shutil, struct, tarfile
import urllib.parse, urllib.request

parser = argparse.ArgumentParser()
parser.add_argument('--rid', choices=['osx-arm64', 'osx-x64'], required=True)
parser.add_argument('--downloads', type=pathlib.Path, required=True)
args = parser.parse_args()
mac = pathlib.Path(__file__).resolve().parent.parent
pins = json.loads((mac / 'engines/native-extension-pins.json').read_text(encoding='utf-8-sig'))
args.downloads.mkdir(parents=True, exist_ok=True)
destination = mac / 'engines' / args.rid
destination.mkdir(parents=True, exist_ok=True)

def fetch(name, url, expected, repository=None):
    target = args.downloads / name
    if not target.exists():
        headers = {'User-Agent': 'Rovunet-native-build/0.1'}
        if repository:
            query = urllib.parse.urlencode({'service': 'ghcr.io', 'scope': 'repository:' + repository + ':pull'})
            with urllib.request.urlopen('https://ghcr.io/token?' + query, timeout=60) as reply:
                headers['Authorization'] = 'Bearer ' + json.load(reply)['token']
        temporary = target.with_suffix(target.suffix + '.download')
        with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=180) as reply, temporary.open('wb') as out:
            shutil.copyfileobj(reply, out)
        temporary.replace(target)
    if hashlib.sha256(target.read_bytes()).hexdigest() != expected:
        raise RuntimeError('Archive SHA-256 mismatch: ' + name)
    return target

def members(archive, names):
    # No filesystem extraction of archive links or untrusted paths.
    with tarfile.open(archive, 'r:gz') as tf:
        all_members = {m.name: m for m in tf.getmembers()}
        for member in all_members.values():
            output = names(member.name)
            if output is None:
                continue
            source = member
            for _ in range(8):
                if not source.issym() and not source.islnk():
                    break
                link = posixpath.normpath(posixpath.join(posixpath.dirname(source.name), source.linkname)) if source.issym() else source.linkname
                source = all_members[link]
            if not source.isfile():
                raise RuntimeError('Expected a regular archive member')
            target = destination / output
            if not target.resolve().is_relative_to(destination.resolve()):
                raise RuntimeError('Archive path escaped engine destination')
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(tf.extractfile(source).read())

def relocate(target):
    data = bytearray(target.read_bytes())
    if struct.unpack_from('<I', data)[0] != 0xfeedfacf:
        raise RuntimeError('Expected a 64-bit Mach-O')
    offset = 32
    for _ in range(struct.unpack_from('<I', data, 16)[0]):
        command, size = struct.unpack_from('<II', data, offset)
        if command in (0xc, 0xd, 0x80000018, 0x8000001f):
            start = offset + struct.unpack_from('<I', data, offset + 8)[0]
            old = bytes(data[start:data.index(0, start, offset + size)]).decode()
            if '/openssl' in old and old.endswith('.dylib'):
                new = ('@loader_path/lib/' if target.name == 'slipstream-client' else '@loader_path/') + posixpath.basename(old)
                encoded = new.encode()
                if len(encoded) >= offset + size - start:
                    raise RuntimeError('Load-command replacement exceeds reserved bytes')
                data[start:offset + size] = encoded + b'\0' * (offset + size - start - len(encoded))
        offset += size
    target.write_bytes(data)

upstream = json.loads((mac / 'engines/upstream-pins.json').read_text(encoding='utf-8-sig'))
slip = next(p for p in upstream['assets'] if p['rid'] == args.rid and p['engine'] == 'slipstream-client')
members(fetch(slip['name'], slip['url'], slip['sha256']), lambda n: 'slipstream-client' if posixpath.basename(n) == 'slipstream-client' else None)
openssl = next(p for p in pins['openssl'] if p['rid'] == args.rid)
def openssl_name(name):
    basename = posixpath.basename(name)
    if basename in ('libssl.3.dylib', 'libcrypto.3.dylib'):
        return 'lib/' + basename
    return 'licenses/openssl/LICENSE.txt' if basename == 'LICENSE.txt' else None
members(fetch(openssl['opensslArchive'], openssl['url'], openssl['opensslArchiveSHA256'], openssl['repository']), openssl_name)
for name in ['slipstream-client', 'lib/libssl.3.dylib', 'lib/libcrypto.3.dylib']:
    relocate(destination / name)
tor = next(p for p in pins['torAssets'] if p['rid'] == args.rid)
def tor_name(name):
    clean = name.removeprefix('./')
    if clean.startswith('data/') or clean in ('tor/tor', 'tor/libevent-2.1.7.dylib', 'tor/pluggable_transports/lyrebird'):
        return clean
    if clean == 'tor/pluggable_transports/pt_config.json':
        return 'data/pt_config.json'
    if clean.startswith('docs/') and clean.endswith('.txt'):
        return 'licenses/tor/' + posixpath.basename(clean)
    return None
members(fetch(tor['archive'], tor['url'], tor['sha256']), tor_name)
print('Prepared pinned native extensions for ' + args.rid + '; sign before delivery.')
