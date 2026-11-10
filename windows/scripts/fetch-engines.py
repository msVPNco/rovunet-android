#!/usr/bin/env python3
"""Download pinned official assets; never trust a version string alone."""
import hashlib,json,pathlib,urllib.request,zipfile,sys,time,tempfile,os
root=pathlib.Path(__file__).resolve().parents[1]
pins=json.loads((root/'engines/pins.json').read_text())
for asset in pins['assets']:
    if '--windows-only' in sys.argv and 'linux' in asset['name']: continue
    target=root/'engines'/('windows-x64' if 'windows' in asset['name'] else 'linux-x64')
    target.mkdir(parents=True,exist_ok=True)
    archive=target/asset['name']
    if not archive.exists() or hashlib.sha256(archive.read_bytes()).hexdigest()!=asset['sha256']:
        asset_id={'Xray-linux-64.zip':695486844,'Xray-windows-64.zip':695488368}[asset['name']]
        sources=[asset['url'],f'https://api.github.com/repos/XTLS/Xray-core/releases/assets/{asset_id}?download=1&t={int(time.time())}']
        for source in sources:
            try:
                request=urllib.request.Request(source,headers={'Accept':'application/octet-stream','User-Agent':'Rovunet-build'})
                with urllib.request.urlopen(request,timeout=45) as response, archive.open('wb') as output:
                    size=0
                    while block:=response.read(1024*1024):
                        size+=len(block)
                        if size>30*1024*1024:raise ValueError('ENGINE_TOO_LARGE')
                        output.write(block)
                if hashlib.sha256(archive.read_bytes()).hexdigest()!=asset['sha256']:raise ValueError('ENGINE_HASH_MISMATCH')
                break
            except Exception as error: print('Official source unavailable:',type(error).__name__,flush=True)
        else:raise SystemExit('No checksum-verified official engine available')
    if hashlib.sha256(archive.read_bytes()).hexdigest()!=asset['sha256']:raise SystemExit('ENGINE_HASH_MISMATCH')
    with zipfile.ZipFile(archive) as z:
        # Extract only needed executable/legal documents. No geo data or auto-updates.
        for name in z.namelist():
            if name in ('xray','xray.exe','LICENSE','README.md'):z.extract(name,target)
    exe=target/('xray.exe' if 'windows' in asset['name'] else 'xray')
    if 'linux' in asset['name']:exe.chmod(0o755)
    (target/'verification.json').write_text(json.dumps(dict(asset,executable_sha256=hashlib.sha256(exe.read_bytes()).hexdigest()),indent=2))
    print(asset['name'], 'verified',hashlib.sha256(exe.read_bytes()).hexdigest(),flush=True)
