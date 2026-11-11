"""Independently verify the CRC32 layers of our zlib UDIF images (stdlib only)."""
import argparse, json, pathlib, plistlib, struct, zlib
parser=argparse.ArgumentParser();parser.add_argument('image',type=pathlib.Path);parser.add_argument('--report',type=pathlib.Path);args=parser.parse_args()
def u32(data,offset):return struct.unpack_from('>I',data,offset)[0]
def u64(data,offset):return struct.unpack_from('>Q',data,offset)[0]
def require(condition,message):
    if not condition:raise RuntimeError(message)
with args.image.open('rb') as source:
    source.seek(-512,2);footer=source.read(512)
    require(footer[:4]==b'koly' and u32(footer,4)==4 and u32(footer,8)==512,'Invalid UDIF footer')
    data_offset=u64(footer,24);data_length=u64(footer,32);xml_offset=u64(footer,216);xml_length=u64(footer,224)
    require(data_offset+data_length<=xml_offset and xml_offset+xml_length==args.image.stat().st_size-512,'Invalid UDIF bounds')
    source.seek(data_offset);remaining=data_length;crc=0
    while remaining:
        data=source.read(min(1048576,remaining));require(bool(data),'Truncated data fork');crc=zlib.crc32(data,crc);remaining-=len(data)
    require(u32(footer,80)==2 and u32(footer,88)==crc,'Compressed data-fork CRC mismatch')
    source.seek(xml_offset);plist=plistlib.loads(source.read(xml_length));blocks=[];master=0;chunks=0;uncompressed=0
    for block in plist['resource-fork']['blkx']:
        table=block['Data'];require(table[:4]==b'mish','Invalid block table');base=u64(table,24);block_crc=0;logical_size=0
        for index in range(u32(table,200)):
            p=204+index*40;kind=u32(table,p);sectors=u64(table,p+16);offset=u64(table,p+24);length=u64(table,p+32)
            if kind in (0x7ffffffe,0xffffffff):continue
            require(sectors*512<=1073741824,'Unexpectedly large chunk')
            if kind in (0,2):raw=b'\0'*(sectors*512)
            else:
                require(data_offset+base+offset+length<=data_offset+data_length,'Compressed chunk outside data fork');source.seek(data_offset+base+offset);encoded=source.read(length)
                raw=zlib.decompress(encoded) if kind==0x80000005 else encoded if kind==1 else None
                require(raw is not None,'Unexpected chunk compression')
            require(len(raw)==sectors*512,'Chunk expanded size mismatch');block_crc=zlib.crc32(raw,block_crc);logical_size+=len(raw);chunks+=1
        require(u32(table,64)==2 and u32(table,72)==block_crc,'Uncompressed block CRC mismatch')
        require(logical_size==u64(table,16)*512,'Block size mismatch');master=zlib.crc32(struct.pack('>I',block_crc),master);uncompressed+=logical_size;blocks.append(block['Name'])
    require(u32(footer,352)==2 and u32(footer,360)==master,'UDIF master CRC mismatch')
report={'image':args.image.name,'blocks':blocks,'chunks':chunks,'uncompressedBytes':uncompressed,'compressedBytes':args.image.stat().st_size,'stdlibCRCVerified':True,'macMounted':False}
if args.report:args.report.write_text(json.dumps(report,indent=2),encoding='utf8')
print(json.dumps(report))
