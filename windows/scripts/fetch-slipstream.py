#!/usr/bin/env python3
"""Fetch only the pinned official x64 client; never install a server."""
import hashlib, json, pathlib, urllib.request, zipfile
root = pathlib.Path(__file__).resolve().parents[1]
pin = json.loads((root / 'engines/transports-pins.json').read_text())['slipstream']
dest = root / 'engines/windows-x64'
dest.mkdir(exist_ok=True, parents=True)
archive = dest / 'slipstream-windows-x86_64.zip'
def digest(data):
    return hashlib.sha256(data).hexdigest()
if not archive.exists() or digest(archive.read_bytes()) != pin['archive_sha256']:
    req = urllib.request.Request(pin['asset_api'], headers={'Accept':'application/octet-stream','User-Agent':'Rovunet-local-build'})
    with urllib.request.urlopen(req,timeout=60) as response:
        data = response.read(20*1024*1024+1)
    if digest(data) != pin['archive_sha256']: raise SystemExit('SLIPSTREAM_ARCHIVE_HASH_MISMATCH')
    archive.write_bytes(data)
with zipfile.ZipFile(archive) as z:
    data = z.read('slipstream-client.exe')
if digest(data) != pin['executable_sha256']: raise SystemExit('SLIPSTREAM_CLIENT_HASH_MISMATCH')
(dest/'slipstream-client.exe').write_bytes(data)
