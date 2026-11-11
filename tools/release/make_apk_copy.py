#!/usr/bin/env python3
"""Copy every compressed APK payload byte from CI; record public ZIP/signature metadata only."""
import argparse,base64,hashlib,json,struct,zipfile,zlib
from pathlib import Path

def sha(data):return hashlib.sha256(data).hexdigest()
def make_record(unsigned_path,signed_path,destination):
    unsigned,signed=unsigned_path.read_bytes(),signed_path.read_bytes()
    end=signed.rfind(b'PK\x05\x06')
    if end<0:raise ValueError('Invalid ZIP end record')
    central=struct.unpack_from('<I',signed,end+16)[0]
    if signed[central-16:central]!=b'APK Sig Block 42':raise ValueError('APK signing block absent')
    ops=[];cursor=0;metadata=0
    def span(data,item):
        start=item.header_offset
        if data[start:start+4]!=b'PK\x03\x04':raise ValueError('Invalid local header')
        name,extra=struct.unpack_from('<HH',data,start+26)
        begin=start+30+name+extra;finish=begin+item.compress_size
        if finish>len(data):raise ValueError('Invalid payload range')
        return begin,finish
    def literal(data):
        nonlocal metadata
        if data:
            metadata+=len(data)
            if metadata>1024*1024:raise ValueError('Metadata bound exceeded')
            ops.append({'literal':base64.b64encode(data).decode()})
    with zipfile.ZipFile(unsigned_path) as a,zipfile.ZipFile(signed_path) as b:
        if set(a.namelist())!=set(b.namelist()) or len(set(a.namelist()))!=len(a.namelist()) or b.testzip() is not None:raise ValueError('ZIP entries changed')
        for item in sorted(b.infolist(),key=lambda item:item.header_offset):
            original=a.getinfo(item.filename);begin,finish=span(signed,item);old_begin,old_finish=span(unsigned,original)
            if begin<cursor or item.compress_type!=original.compress_type:raise ValueError('Invalid ZIP layout')
            if signed[begin:finish]!=unsigned[old_begin:old_finish] or a.read(item.filename)!=b.read(item.filename):raise ValueError('APK payload changed')
            literal(signed[cursor:begin])
            if finish>begin:ops.append({'copy':[old_begin,old_finish-old_begin]})
            cursor=finish
        literal(signed[cursor:])
    if len(ops)>2000:raise ValueError('Copy operation bound exceeded')
    restored=bytearray()
    for op in ops:
        if 'copy' in op:
            start,size=op['copy'];restored+=unsigned[start:start+size]
        else:restored+=base64.b64decode(op['literal'],validate=True)
    if restored!=signed:raise ValueError('Exact restoration failed')
    record={'schema':1,'unsigned_sha256':sha(unsigned),'approved_sha256':sha(signed),'approved_size':len(signed),'ops':ops}
    destination.write_bytes(base64.b64encode(zlib.compress(json.dumps(record,separators=(',',':')).encode(),9))+b'\n')
    print('Verified exact public copy record:',len(ops),'operations;',len(signed),'APK bytes')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('unsigned',type=Path);p.add_argument('signed',type=Path);p.add_argument('output',type=Path);a=p.parse_args();make_record(a.unsigned,a.signed,a.output)
