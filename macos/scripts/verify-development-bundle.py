"""Independent integrity checks, not Apple trust/Gatekeeper verification.

Checks every Mach-O code-page hash and embedded special blob, app Info.plist and
CodeResources seals, nested cdhashes, all regular resources and engine manifests.
Ad-hoc signatures intentionally have no certificate/CMS identity.
"""
import argparse, hashlib, json, pathlib, plistlib, struct

parser = argparse.ArgumentParser()
parser.add_argument('app',type=pathlib.Path)
parser.add_argument('--report',type=pathlib.Path)
args=parser.parse_args()
contents=args.app/'Contents'
rows=[]
def require(value,message):
    if not value: raise RuntimeError(message)
def digest(data,kind):
    if kind==1:return hashlib.sha1(data).digest()
    if kind in (2,3):return hashlib.sha256(data).digest()[:20 if kind==3 else 32]
    if kind==4:return hashlib.sha384(data).digest()
    raise RuntimeError('Unknown signature hash algorithm')
def be(data,offset):return struct.unpack_from('>I',data,offset)[0]
def thin(data,info=None,resources=None,runtime_host=False):
    require(struct.unpack_from('<I',data)[0]==0xfeedfacf,'Expected Mach-O 64-bit')
    cursor=32;signature=None
    for _ in range(struct.unpack_from('<I',data,16)[0]):
        command,size=struct.unpack_from('<II',data,cursor)
        require(size>=8 and cursor+size<=len(data),'Invalid load command bounds')
        if command==0x1d:
            start,length=struct.unpack_from('<II',data,cursor+8);signature=data[start:start+length]
        cursor+=size
    require(signature is not None and be(signature,0)==0xfade0cc0,'Missing embedded signature')
    blobs={}
    for index in range(be(signature,8)):
        slot,offset=struct.unpack_from('>II',signature,12+index*8);length=be(signature,offset+4)
        require(length>=8 and offset+length<=len(signature),'Invalid signature blob bounds')
        blobs[slot]=signature[offset:offset+length]
    cds=[]
    for slot,cd in blobs.items():
        if be(cd,0)!=0xfade0c02:continue
        version,flags,hash_offset,identifier_offset,specials,pages,limit=struct.unpack_from('>7I',cd,8)
        require(flags&2,'Expected an ad-hoc development signature')
        hash_size,kind,platform,page_log=struct.unpack_from('>4B',cd,36)
        if version>=0x20300 and limit==0:limit=struct.unpack_from('>Q',cd,56)[0]
        require(limit<=len(data) and page_log<=30,'Invalid code page limit')
        page_size=1<<page_log
        require(pages==(limit+page_size-1)//page_size,'Wrong code page count')
        for page in range(pages):
            expected=cd[hash_offset+page*hash_size:hash_offset+(page+1)*hash_size]
            require(digest(data[page*page_size:min((page+1)*page_size,limit)],kind)[:hash_size]==expected,'Code page hash mismatch')
        for special in range(1,specials+1):
            expected=cd[hash_offset-special*hash_size:hash_offset-(special-1)*hash_size]
            if expected==b'\0'*hash_size:continue
            source=info if special==1 else resources if special==3 else blobs.get(special)
            require(source is not None,'Unresolved special signature slot '+str(special))
            require(digest(source,kind)[:hash_size]==expected,'Special signature slot hash mismatch '+str(special))
        identifier=cd[identifier_offset:cd.index(0,identifier_offset)].decode()
        if info is not None:
            require(specials>=5 and 5 in blobs,'Main app entitlement/resource slots missing')
            for number in (1,3,5):require(cd[hash_offset-number*hash_size:hash_offset-(number-1)*hash_size]!=b'\0'*hash_size,'Missing Info.plist/resource/entitlement seal')
            require(plistlib.loads(blobs[5][8:]).get('com.apple.security.cs.allow-jit') is True,'Main app JIT entitlement missing')
        if runtime_host:require(5 in blobs and plistlib.loads(blobs[5][8:]).get('com.apple.security.cs.allow-jit') is True,'Agent JIT entitlement missing')
        cds.append({'hashType':kind,'cdhash':digest(cd,kind)[:20],'identifier':identifier,'flags':flags,'codePages':pages})
    require(len(cds)>0,'No CodeDirectory')
    return cds
def verify_macho(file,main=False):
    data=file.read_bytes();slices=[]
    if data[:4]==b'\xca\xfe\xba\xbe':
        for i in range(be(data,4)):
            start,size=struct.unpack_from('>II',data,8+i*20+8);slices.append(data[start:start+size])
    else:slices=[data]
    cds=[]
    for sliced in slices:cds.extend(thin(sliced,(contents/'Info.plist').read_bytes() if main else None,(contents/'_CodeSignature/CodeResources').read_bytes() if main else None,file.name=='Rovunet.Agent'))
    return cds

resource_bytes=(contents/'_CodeSignature/CodeResources').read_bytes()
resources=plistlib.loads(resource_bytes)['files2']
for file in sorted(contents.rglob('*')):
    if not file.is_file():continue
    relative=file.relative_to(contents).as_posix()
    if relative.startswith('_CodeSignature/') or relative=='Info.plist':continue
    with file.open('rb') as source:magic=source.read(4)
    if magic in (b'\xcf\xfa\xed\xfe',b'\xca\xfe\xba\xbe'):
        cds=verify_macho(file,relative=='MacOS/Rovunet')
        if relative!='MacOS/Rovunet':require(resources.get(relative,{}).get('cdhash') in [cd['cdhash'] for cd in cds],'Nested code seal mismatch: '+relative)
        rows.append({'file':relative,'codeDirectories':[{k:v for k,v in cd.items() if k!='cdhash'} for cd in cds]})
    else:require(resources.get(relative,{}).get('hash2')==hashlib.sha256(file.read_bytes()).digest(),'Unsealed or modified resource: '+relative)
engine_root=contents/'MacOS/engines';manifest=json.loads((engine_root/'engine-manifest.json').read_text(encoding='utf-8-sig'))
paths={'tor':'tor/tor','lyrebird':'tor/pluggable_transports/lyrebird'}
for name,expected in manifest['executables'].items():require(hashlib.sha256((engine_root/paths.get(name,name)).read_bytes()).hexdigest()==expected,'Engine hash mismatch: '+name)
for name,expected in manifest['files'].items():require(hashlib.sha256((engine_root/name).read_bytes()).hexdigest()==expected,'Dependency hash mismatch: '+name)
report={'rid':manifest['rid'],'machOFiles':len(rows),'sealedResources':len(resources),'developmentAdhocSigned':True,'jitEntitlementsValidated':True,'appResourceBindingsValidated':True,'appleCodesignVerified':False,'macRuntimeValidated':False,'checks':'CodeDirectory pages/special blobs, JIT entitlements, nested cdhash, Info.plist, CodeResources and engine SHA-256','nativeFiles':rows}
if args.report:args.report.write_text(json.dumps(report,indent=2),encoding='utf8')
print(json.dumps({k:v for k,v in report.items() if k!='nativeFiles'}))
