#!/usr/bin/env python3
"""Read-only download of pinned official NaiveProxy binaries, with two hashes."""
import hashlib,json,pathlib,sys,tarfile,time,urllib.request,zipfile
root=pathlib.Path(__file__).resolve().parents[1]
pins=json.loads((root/'engines/transports-pins.json').read_text())['naive']
for arch,asset in pins['assets'].items():
    if '--windows-only' in sys.argv and arch!='windows-x64':continue
    target=root/'engines'/arch;target.mkdir(parents=True,exist_ok=True)
    archive=target/asset['name']
    if not archive.exists() or hashlib.sha256(archive.read_bytes()).hexdigest()!=asset['archive_sha256']:
        request=urllib.request.Request('https://api.github.com/repos/klzgrad/naiveproxy/releases/assets/'+str(asset['asset_id'])+'?download=1&t='+str(int(time.time())),headers={'Accept':'application/octet-stream','User-Agent':'Rovunet-read-only-build'})
        with urllib.request.urlopen(request,timeout=60) as response,archive.open('wb') as output:
            count=0
            while b:=response.read(1024*1024):
                count+=len(b)
                if count>16*1024*1024:raise SystemExit('ENGINE_TOO_LARGE')
                output.write(b)
    if hashlib.sha256(archive.read_bytes()).hexdigest()!=asset['archive_sha256']:raise SystemExit('ENGINE_HASH_MISMATCH')
    name='naive.exe' if arch=='windows-x64' else 'naive'
    if archive.suffix=='.zip':
        with zipfile.ZipFile(archive) as z:data=z.read(next(n for n in z.namelist() if n.endswith('/'+name)))
    else:
        with tarfile.open(archive) as z:data=z.extractfile(next(m for m in z.getmembers() if m.name.endswith('/'+name))).read()
    if hashlib.sha256(data).hexdigest()!=asset['executable_sha256']:raise SystemExit('ENGINE_HASH_MISMATCH')
    (target/name).write_bytes(data);(target/name).chmod(0o755)
    (target/'naive-verification.json').write_text(json.dumps(dict(asset,version=pins['version'],repo='klzgrad/naiveproxy',verified=True),indent=2)+'\n')
    print(arch,'NaiveProxy verified')
