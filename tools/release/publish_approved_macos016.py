"""Transfer the owner's twelve frozen Mac Beta assets, without building/signing.

Temporary Git blobs transport the exact bytes. The upload manifest records every
chunk; the fixed approved full-file digests are embedded below. Nothing from a
public PR, arbitrary URL or arbitrary filename is executed or published.
"""
import argparse,base64,concurrent.futures,hashlib,json,os,pathlib,time,urllib.error,urllib.parse,urllib.request

REPOSITORY='msVPNco/rovunet-app'
SOURCE='9b2acee98d376a0c9edf50462c0698253dd3caa4'
TAG='macos-v0.1.6-beta'
RELEASE=442151196
APPROVED={'Rovunet-macOS-0.1.6-beta-AppleSilicon-arm64.pkg': [172531604, '7899ccb8734fb1ec56e74ed03ef971a72b28c74fe941b6f851318ce4a2e6a6ee'], 'Rovunet-macOS-0.1.6-beta-AppleSilicon-arm64.dmg': [172792476, 'd080c04a325ce0210b1555a82ae07cdd8510fc1463ce76bb95efa7e1074a8865'], 'Rovunet-macOS-0.1.6-beta-Intel-x64.pkg': [186345504, 'a5e4074dafa27fc06fa27bebd27847b2d8ca9802baf3739291cc3f916f9f9320'], 'Rovunet-macOS-0.1.6-beta-Intel-x64.dmg': [186581944, 'ccd28c53b938c566bbd24206a7b8b05f1321784fe51e52faacd2b5e3e7577aa1'], 'Rovunet-macOS-0.1.6-beta-source.zip': [11231130, 'bb3f2f5d768e11415e7819997713367935e1ceb74d02cc80c013f82c6aca2687'], 'Rovunet-macOS-0.1.6-beta-native-source.zip': [22858193, 'f40ca8934962540b548a703dfece9eef219025086072788b79d9821c16866902'], 'SHA256SUMS.txt': [5027, '9227db3336b1ee911741ec5dfc2899e076e651c42cccefeff33a3332ff3e8c2f'], 'VERIFICATION.json': [9586, '5d3f54e2bb1f6429d03b6d8ff5c876e421b5606663e3b2405b0e0241daf90384'], 'RELEASE-FA.md': [3950, '7f64e1772997772c48273ae5ea90b69c88254cb7e779228034ce4abbfea50f2c'], 'RELEASE-EN.md': [2683, '98a0a904baf565af04009cd651083e59db4303e11aeb471a35141994204b817c'], 'RELEASE-RU.md': [3993, '8b7c5eb266114200c71843c0552edaa625832d7ee1c79437c6fcc7bec4b36c87'], 'RELEASE-ZH-CN.md': [2136, '711cb376588196bf85868fd9d0182983d60a3c9484032967b68397f16b3f2f84']}  # Replaced with the locally verified immutable upload plan.
BODY_SHA='5f17af8db90dca81e4e5259b2e48e33f7f0aea353fdcc2dd85ceb03d4eff957b'  # Replaced with the prepared four-language release body digest.

def digest(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()

def request(url,method='GET',data=None,raw=False):
    assert url.startswith(('https://api.github.com/repos/'+REPOSITORY+'/', 'https://uploads.github.com/repos/'+REPOSITORY+'/'))
    headers={'Authorization':'Bearer '+os.environ['GH_TOKEN'],'Accept':'application/vnd.github+json','X-GitHub-Api-Version':'2022-11-28','User-Agent':'Rovunet-approved-macos016-transfer'}
    if isinstance(data,dict):data=json.dumps(data).encode();headers['Content-Type']='application/json'
    elif data is not None:headers['Content-Type']='application/octet-stream'
    for attempt in range(5):
        try:
            with urllib.request.urlopen(urllib.request.Request(url,data=data,headers=headers,method=method),timeout=240) as response:
                payload=response.read()
            return payload if raw else json.loads(payload)
        except urllib.error.HTTPError as error:
            if error.code not in (429,500,502,503,504) or attempt==4:raise
            time.sleep(min(30,2**attempt))

def api(path,**kwargs):return request('https://api.github.com/repos/'+REPOSITORY+path,**kwargs)

def approved_release(manifest):
    assert os.environ.get('GITHUB_REPOSITORY',REPOSITORY)==REPOSITORY
    assert manifest['repository']==REPOSITORY and manifest['sourceCommit']==SOURCE and manifest['tag']==TAG and manifest['draftReleaseId']==RELEASE
    assert manifest['bodySHA256']==BODY_SHA
    assert len(manifest['files'])==len(APPROVED)==12
    assert {f['name']:(f['bytes'],f['sha256']) for f in manifest['files']}=={name:tuple(value) for name,value in APPROVED.items()}
    current=api('/releases/'+str(RELEASE))
    assert current['tag_name']==TAG and current['name']=='Rovunet macOS 0.1.6 Beta' and current['prerelease']
    assert hashlib.sha256(current['body'].replace('\r\n','\n').strip().encode()).hexdigest()==BODY_SHA
    source=api('/git/ref/heads/macos/release-0.1.6-beta')
    assert source['object']['sha']==SOURCE
    return current

def restore(entry,directory,cache):
    name=entry['name'];assert pathlib.PurePosixPath(name).name==name
    target=directory/name
    if target.exists() and digest(target)==entry['sha256']:return target
    with target.open('wb') as f:
        offset=0
        for chunk in entry['chunks']:
            assert chunk['offset']==offset and 0<chunk['bytes']<=2*1024*1024
            chunk_file=cache/chunk['sha']
            if chunk_file.exists():raw=chunk_file.read_bytes()
            else:
                blob=api('/git/blobs/'+chunk['sha']);assert blob['encoding']=='base64' and blob['sha']==chunk['sha']
                raw=base64.b64decode(blob['content']);chunk_file.write_bytes(raw)
            assert len(raw)==chunk['bytes'] and hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==chunk['sha']
            f.write(raw);offset+=len(raw)
    assert offset==entry['bytes'] and digest(target)==entry['sha256'],name
    return target

def assert_asset(asset,entry):
    assert asset['state']=='uploaded' and asset['size']==entry['bytes'],entry['name']
    assert asset.get('digest')=='sha256:'+entry['sha256'],entry['name']

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--manifest',type=pathlib.Path,required=True);parser.add_argument('--output',type=pathlib.Path,required=True);args=parser.parse_args()
    manifest=json.loads(args.manifest.read_text(encoding='utf8'));current=approved_release(manifest)
    if not manifest['ready']:
        chunk=manifest['files'][0]['chunks'][0]
        blob=api('/git/blobs/'+chunk['sha']);raw=base64.b64decode(blob['content'])
        assert len(raw)==chunk['bytes'] and hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==chunk['sha']
        print(json.dumps({'preflight':True,'release':RELEASE,'draft':current['draft'],'prerelease':True,'fixedFileDigestsChecked':12,'source':SOURCE,'transportBlobReadAndVerified':True,'buildOrSigningCommands':False}));return
    args.output.mkdir(parents=True,exist_ok=True);cache=args.output/'chunks';cache.mkdir(exist_ok=True)
    existing={a['name']:a for a in current['assets']}
    assert set(existing)<=set(APPROVED),'Unexpected draft assets require review'
    for entry in manifest['files']:
        if entry['name'] in existing:assert_asset(existing[entry['name']],entry)
    pending=[e for e in manifest['files'] if e['name'] not in existing]
    def transfer(entry):
        target=restore(entry,args.output,cache)
        url='https://uploads.github.com/repos/'+REPOSITORY+'/releases/'+str(RELEASE)+'/assets?name='+urllib.parse.quote(entry['name'],safe='')
        asset=request(url,method='POST',data=target.read_bytes());assert_asset(asset,entry)
        print(json.dumps({'uploaded':entry['name'],'bytes':entry['bytes'],'sha256':entry['sha256'],'rebuilt':False,'resigned':False}),flush=True)
        return asset
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:list(pool.map(transfer,pending))
    current=approved_release(manifest);assets={a['name']:a for a in current['assets']};assert set(assets)==set(APPROVED)
    for entry in manifest['files']:assert_asset(assets[entry['name']],entry)
    if current['draft']:
        current=api('/releases/'+str(RELEASE),method='PATCH',data={'draft':False,'prerelease':True,'make_latest':'false','target_commitish':SOURCE})
    assert not current['draft'] and current['prerelease'] and current['tag_name']==TAG
    ref=api('/git/ref/tags/'+TAG);target=ref['object']
    if target['type']=='tag':target=api('/git/tags/'+target['sha'])['object']
    assert target['sha']==SOURCE
    latest=api('/releases/latest');assert latest['id']!=RELEASE
    report={'published':True,'prerelease':True,'url':current['html_url'],'releaseId':RELEASE,'tag':TAG,'sourceCommit':SOURCE,'assets':[{k:a[k] for k in ['id','name','size','digest','browser_download_url']} for a in current['assets']],'rebuilt':False,'resigned':False,'latestUnchangedByMacRelease':True,'latestTag':latest['tag_name']}
    (args.output/'publication.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf8');print(json.dumps(report),flush=True)

if __name__=='__main__':main()
