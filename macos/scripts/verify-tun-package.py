"""Independently inspect the XAR/CPIO service package; never execute scripts."""
import argparse, gzip, hashlib, json, pathlib, struct, xml.etree.ElementTree as ET, zlib

def require(good,reason):
    if not good:raise ValueError(reason)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('package',type=pathlib.Path);parser.add_argument('client',type=pathlib.Path);parser.add_argument('--report',type=pathlib.Path);args=parser.parse_args();data=args.package.read_bytes()
    magic,header,version,compressed,uncompressed,kind=struct.unpack_from('>4sHHQQI',data);require((magic,header,version,kind)==(b'xar!',28,1,1),'Invalid XAR header');encoded=data[28:28+compressed];toc=zlib.decompress(encoded);require(len(toc)==uncompressed,'Invalid XAR TOC length');root=ET.fromstring(toc);heap=data[28+compressed:];require(hashlib.sha1(encoded).digest()==heap[:20],'XAR TOC checksum mismatch');outer={}
    for entry in root.findall('./toc/file'):
        name=entry.findtext('name');body=entry.find('data');offset=int(body.findtext('offset'));length=int(body.findtext('length'));raw=heap[offset:offset+length];require(offset>=20 and len(raw)==length,'Invalid XAR data extent');require(hashlib.sha1(raw).hexdigest()==body.findtext('archived-checksum'),'XAR archive checksum mismatch');value=raw if body.find('encoding').get('style')=='application/octet-stream' else zlib.decompress(raw);require(len(value)==int(body.findtext('size')) and hashlib.sha1(value).hexdigest()==body.findtext('extracted-checksum'),'XAR extracted checksum mismatch');outer[name]=value
    require(set(outer)=={'PackageInfo','Scripts'},'Unexpected installer components');info=ET.fromstring(outer['PackageInfo']);require(info.get('identifier')=='org.rovunet.tun.helper.pkg' and info.get('auth')=='root','Wrong installer identity');require(info.find('payload').get('numberOfFiles')=='0' and info.find('scripts/postinstall').get('file')=='./postinstall','Wrong no-payload script declaration')
    cpio=gzip.decompress(outer['Scripts']);position=0;files={};modes={}
    while True:
        require(cpio[position:position+6]==b'070707','Invalid odc CPIO magic');offset=position+6;values=[]
        for width in [6,6,6,6,6,6,6,11,6,11]:values.append(int(cpio[offset:offset+width],8));offset+=width
        dev,inode,mode,uid,gid,links,rdev,mtime,namesize,size=values;position=offset
        name=cpio[position:position+namesize-1].decode();position+=namesize;value=cpio[position:position+size];position+=size
        require(uid==gid==0 and links==1 and size==len(value),'Unsafe CPIO ownership');require('/' not in name and name not in files,'Unsafe CPIO path')
        if name=='TRAILER!!!':break
        files[name]=value;modes[name]=mode
    require(set(files)=={'rovunet-tun','policy.json','org.rovunet.tun.helper.plist','SHA256SUMS','postinstall'},'Unexpected root script input');require(files['rovunet-tun']==args.client.read_bytes(),'Installed helper differs from signed client');require(modes['rovunet-tun']==modes['postinstall']==0o100755,'Root executable mode')
    for line in files['SHA256SUMS'].decode().splitlines():
        digest,name=line.split('  ',1);require(hashlib.sha256(files[name]).hexdigest()==digest,'Installer SHA-256 mismatch')
    policy=json.loads(files['policy.json']);require(policy['Version']==1 and len(policy['ClientCDHashes']) in range(1,9),'Invalid client policy')
    client=files['rovunet-tun'];cursor=32;signature=None
    require(client[:4]==b'\xcf\xfa\xed\xfe','Wrong native helper format')
    for _ in range(struct.unpack_from('<I',client,16)[0]):
        command,size=struct.unpack_from('<II',client,cursor)
        if command==0x1d:
            offset,length=struct.unpack_from('<II',client,cursor+8);signature=client[offset:offset+length]
        cursor+=size
    require(signature is not None,'Unsigned helper');hashes=[]
    for i in range(struct.unpack_from('>I',signature,8)[0]):
        slot,offset=struct.unpack_from('>II',signature,12+i*8)
        magic,length=struct.unpack_from('>II',signature,offset)
        if magic!=0xfade0c02:continue
        code=signature[offset:offset+length];flags=struct.unpack_from('>I',code,12)[0];require(flags&0x10000,'Native client lacks hardened runtime')
        algorithm={1:hashlib.sha1,2:hashlib.sha256,3:hashlib.sha256,4:hashlib.sha384}[code[37]];hashes.append(algorithm(code).digest()[:20].hex())
    require(hashes and set(hashes)==set(policy['ClientCDHashes']),'Installed client code-hash policy mismatch')
    report={'package':args.package.name,'version':info.get('version'),'helperBytes':len(files['rovunet-tun']),'helperSHA256':hashlib.sha256(files['rovunet-tun']).hexdigest(),'clientCDHashes':policy['ClientCDHashes'],'rootExecutablePaths':['/Library/PrivilegedHelperTools/org.rovunet.tun.helper'],'xarAndCpioVerified':True,'exactHelperMatch':True,'fixedRootScriptInputs':True,'appleInstallerValidated':False,'macRuntimeValidated':False}
    if args.report:args.report.write_text(json.dumps(report,indent=2))
    print(json.dumps(report))
if __name__=='__main__':main()
