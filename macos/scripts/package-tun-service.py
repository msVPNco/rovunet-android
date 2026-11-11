"""Create a no-payload, scripts-only macOS Installer component (XAR/CPIO).

Only the fixed native helper, fixed launchd job and its client code hashes are
installed. This cross-host structural check is not an Apple Installer test.
"""
import argparse, gzip, hashlib, io, json, pathlib, struct, xml.etree.ElementTree as ET, zlib

def cdhashes(data):
    if data[:4]!=b'\xcf\xfa\xed\xfe':raise ValueError('Expected thin signed Mach-O')
    cursor=32;signature=None
    for _ in range(struct.unpack_from('<I',data,16)[0]):
        command,size=struct.unpack_from('<II',data,cursor)
        if command==0x1d:
            start,length=struct.unpack_from('<II',data,cursor+8);signature=data[start:start+length]
        cursor+=size
    if signature is None:raise ValueError('Unsigned TUN client')
    found=[]
    for i in range(struct.unpack_from('>I',signature,8)[0]):
        slot,offset=struct.unpack_from('>II',signature,12+i*8)
        if struct.unpack_from('>I',signature,offset)[0]!=0xfade0c02:continue
        size,version,flags=struct.unpack_from('>III',signature,offset+4)
        if not flags&0x10000:raise ValueError('TUN client requires hardened runtime')
        cd=signature[offset:offset+size];kind=cd[37]
        algorithm={1:hashlib.sha1,2:hashlib.sha256,3:hashlib.sha256,4:hashlib.sha384}[kind]
        found.append(algorithm(cd).digest()[:20].hex())
    if not found:raise ValueError('Missing TUN CodeDirectory')
    return sorted(set(found))

def cpio(files):
    out=io.BytesIO()
    for i,(name,data,mode) in enumerate(files+[('TRAILER!!!',b'',0)],1):
        # pkgbuild uses portable ASCII (odc), not the Linux newc format.
        name=name.encode()+b'\0'
        header=('070707'+'%06o'*7+'%011o%06o%011o')%(0,i,mode,0,0,1,0,1794355200,len(name),len(data))
        out.write(header.encode());out.write(name);out.write(data)
    out.write(b'\0'*((-out.tell())%512))
    return out.getvalue()

def xar(files):
    root=ET.Element('xar');toc=ET.SubElement(root,'toc');checksum=ET.SubElement(toc,'checksum',style='sha1');ET.SubElement(checksum,'offset').text='0';ET.SubElement(checksum,'size').text='20'
    ET.SubElement(toc,'creation-time').text='2026-11-11T00:00:00Z';heap=bytearray(20)
    for i,(name,raw) in enumerate(files,1):
        encoded=raw if name=='Scripts' else zlib.compress(raw,9);entry=ET.SubElement(toc,'file',id=str(i));ET.SubElement(entry,'name').text=name;ET.SubElement(entry,'type').text='file';ET.SubElement(entry,'mode').text='0644';ET.SubElement(entry,'uid').text='0';ET.SubElement(entry,'gid').text='0';ET.SubElement(entry,'user').text='root';ET.SubElement(entry,'group').text='wheel';body=ET.SubElement(entry,'data')
        for key,value in [('length',len(encoded)),('offset',len(heap)),('size',len(raw))]:ET.SubElement(body,key).text=str(value)
        ET.SubElement(body,'encoding',style='application/octet-stream' if name=='Scripts' else 'application/x-gzip');ET.SubElement(body,'archived-checksum',style='sha1').text=hashlib.sha1(encoded).hexdigest();ET.SubElement(body,'extracted-checksum',style='sha1').text=hashlib.sha1(raw).hexdigest();heap.extend(encoded)
    xml=ET.tostring(root,encoding='utf-8',xml_declaration=True);compressed=zlib.compress(xml,9);heap[:20]=hashlib.sha1(compressed).digest()
    return struct.pack('>4sHHQQI',b'xar!',28,1,len(compressed),len(xml),1)+compressed+heap

def main():
    parser=argparse.ArgumentParser();parser.add_argument('client',type=pathlib.Path);parser.add_argument('output',type=pathlib.Path);args=parser.parse_args()
    helper=args.client.read_bytes();policy=json.dumps({'Version':1,'ClientCDHashes':cdhashes(helper)},indent=2).encode();packaging=pathlib.Path(__file__).resolve().parent.parent/'packaging'
    files=[('rovunet-tun',helper,0o100755),('policy.json',policy,0o100644),('org.rovunet.tun.helper.plist',(packaging/'org.rovunet.tun.helper.plist').read_bytes(),0o100644)]
    sums=''.join(hashlib.sha256(data).hexdigest()+'  '+name+'\n' for name,data,_ in files).encode();files.extend([('SHA256SUMS',sums,0o100644),('postinstall',(packaging/'tun-postinstall.sh').read_bytes().replace(b'\r\n',b'\n'),0o100755)])
    info=b'<?xml version="1.0" encoding="UTF-8"?><pkg-info format-version="2" identifier="org.rovunet.tun.helper.pkg" version="0.1.0.3" install-location="/" auth="root"><payload installKBytes="0" numberOfFiles="0"/><scripts><postinstall file="./postinstall"/></scripts></pkg-info>'
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_bytes(xar([('Scripts',gzip.compress(cpio(files),mtime=0)),('PackageInfo',info)]))
    print(json.dumps({'file':args.output.name,'helperSHA256':hashlib.sha256(helper).hexdigest(),'clientCDHashes':cdhashes(helper),'appleInstallerValidated':False}))
if __name__=='__main__':main()
