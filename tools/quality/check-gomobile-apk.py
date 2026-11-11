#!/usr/bin/env python3
"""Check the JNI ABI in actual DEX class definitions, not mappings or references."""
from __future__ import annotations
import argparse
import struct
import zipfile
from pathlib import Path


class Dex:
    def __init__(self, data: bytes):
        self.data = data
        if len(data) < 112 or not data.startswith(b'dex\n') or data[7] != 0:
            raise ValueError('Unsupported or truncated DEX')
        if self.u32(32) != len(data) or self.u32(36) != 112 or self.u32(40) != 0x12345678:
            raise ValueError('Invalid DEX header')
        strings, string_offset = self.table(56, 4)
        self.strings = [self.string(self.u32(string_offset + 4*i)) for i in range(strings)]
        types, type_offset = self.table(64, 4)
        self.types = [self.strings[self.u32(type_offset + 4*i)] for i in range(types)]
        protos, proto_offset = self.table(72, 12)
        self.protos = []
        for i in range(protos):
            at = proto_offset + 12*i
            result = self.types[self.u32(at+4)]
            params_at = self.u32(at+8)
            params = ''
            if params_at:
                count = self.u32(params_at)
                self.bounds(params_at+4, count*2)
                params = ''.join(self.types[self.u16(params_at+4+2*j)] for j in range(count))
            self.protos.append('('+params+')'+result)
        fields, field_offset = self.table(80, 8)
        self.fields = []
        for i in range(fields):
            at=field_offset+8*i
            self.fields.append((self.types[self.u16(at)], self.strings[self.u32(at+4)], self.types[self.u16(at+2)]))
        methods, method_offset = self.table(88, 8)
        self.methods = []
        for i in range(methods):
            at=method_offset+8*i
            self.methods.append((self.types[self.u16(at)], self.strings[self.u32(at+4)], self.protos[self.u16(at+2)]))
        classes, class_offset = self.table(96, 32)
        self.classes = set()
        self.defined_methods = set()
        self.defined_fields = set()
        for i in range(classes):
            at=class_offset+32*i
            owner=self.types[self.u32(at)]
            if owner in self.classes:
                raise ValueError('Duplicate DEX class definition')
            self.classes.add(owner)
            cursor=self.u32(at+24)
            if not cursor:
                continue
            counts=[]
            for _ in range(4):
                count,cursor=self.uleb(cursor);counts.append(count)
            for count in counts[:2]:
                index=0
                for _ in range(count):
                    delta,cursor=self.uleb(cursor);index+=delta
                    _,cursor=self.uleb(cursor)
                    field=self.fields[index]
                    if field[0]!=owner:raise ValueError('Invalid field owner')
                    self.defined_fields.add(field)
            for count in counts[2:]:
                index=0
                for _ in range(count):
                    delta,cursor=self.uleb(cursor);index+=delta
                    _,cursor=self.uleb(cursor)
                    _,cursor=self.uleb(cursor)
                    method=self.methods[index]
                    if method[0]!=owner:raise ValueError('Invalid method owner')
                    self.defined_methods.add(method)

    def bounds(self, offset, size):
        if offset<0 or size<0 or offset>len(self.data)-size:
            raise ValueError('DEX range exceeds its input')

    def u32(self, offset):
        self.bounds(offset,4)
        return struct.unpack_from('<I', self.data, offset)[0]

    def u16(self, offset):
        self.bounds(offset,2)
        return struct.unpack_from('<H', self.data, offset)[0]

    def table(self, offset, width):
        size, start=self.u32(offset),self.u32(offset+4)
        self.bounds(start,size*width)
        return size,start

    def uleb(self, offset):
        result=0
        for shift in range(0,35,7):
            self.bounds(offset,1)
            byte=self.data[offset];offset+=1
            if shift==28 and byte>15:raise ValueError('Invalid DEX ULEB128')
            result|=(byte&127)<<shift
            if not byte&128:return result,offset
        raise ValueError('Invalid DEX ULEB128')

    def string(self, offset):
        _,start=self.uleb(offset)
        end=self.data.index(b'\0',start)
        return self.data[start:end].decode('utf-8', errors='replace')


def missing_abi(apk: Path, lite=False):
    classes=set();methods=set();fields=set();total=0
    with zipfile.ZipFile(apk) as archive:
        entries=[i for i in archive.infolist() if i.filename.endswith('.dex')]
        if not entries:raise ValueError('No DEX in APK')
        for entry in entries:
            total+=entry.file_size
            if entry.file_size>64*1024*1024 or total>256*1024*1024:
                raise ValueError('DEX input exceeds verification budget')
            dex=Dex(archive.read(entry))
            if classes & dex.classes:raise ValueError('Duplicate class across DEX files')
            classes|=dex.classes;methods|=dex.defined_methods;fields|=dex.defined_fields
    required_classes={'Lgo/Seq;', 'Lgo/Seq$Ref;', 'Lgo/Seq$GoObject;', 'Lgo/Seq$Proxy;', 'Lgo/Universe;', 'Lgo/Universe$proxyerror;'}
    required_methods={
        ('Lgo/Seq;', 'getRef', '(I)Lgo/Seq$Ref;'),
        ('Lgo/Seq;', 'incRef', '(Ljava/lang/Object;)I'),
        ('Lgo/Seq;', 'incRefnum', '(I)V'),
        ('Lgo/Seq;', 'incGoObjectRef', '(Lgo/Seq$GoObject;)I'),
        ('Lgo/Seq;', 'decRef', '(I)V'),
        ('Lgo/Seq$Ref;', '<init>', '(ILjava/lang/Object;)V'),
        ('Lgo/Seq$GoObject;', 'incRefnum', '()I'),
        ('Lgo/Universe$proxyerror;', '<init>', '(I)V'),
        ('Lgo/Universe$proxyerror;', 'getMessage', '()Ljava/lang/String;'),
    }
    required_fields={('Lgo/Seq$Ref;', 'obj', 'Ljava/lang/Object;'), ('Lgo/Seq$Ref;', 'refnum', 'I')}
    clients=['mobile/DnsttClient','vaydns/VaydnsClient','soar/SoarClient']
    if not lite:clients.append('snowflake/SnowflakeClient')
    for name in clients:
        owner='L'+name+';';required_classes.add(owner)
        required_methods|={(owner,'<init>','(I)V'),(owner,'incRefnum','()I')}
        required_fields.add((owner,'refnum','I'))
    return ([f'class {c}'for c in sorted(required_classes-classes)]+
            [f'method {c}.{n}{s}'for c,n,s in sorted(required_methods-methods)]+
            [f'field {c}.{n}:{s}'for c,n,s in sorted(required_fields-fields)])


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('apk',type=Path)
    parser.add_argument('--lite',action='store_true')
    args=parser.parse_args()
    try:missing=missing_abi(args.apk,args.lite)
    except (ValueError,IndexError,struct.error,zipfile.BadZipFile) as failure:
        raise SystemExit('FAIL gomobile APK: '+str(failure))
    if missing:raise SystemExit('FAIL gomobile JNI ABI:\n'+'\n'.join(missing))
    print('PASS gomobile JNI class definitions, constructors, fields and callbacks: '+args.apk.name)


if __name__=='__main__':main()
