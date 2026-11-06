#!/usr/bin/env python3
"""Check every shipped 64-bit ELF's LOAD alignment; does not claim native reproducibility."""
import argparse
import hashlib
from pathlib import Path
import struct
import zipfile

parser = argparse.ArgumentParser()
parser.add_argument("apks", nargs="+", type=Path)
parser.add_argument("--compare", type=Path, help="also require byte-identical native libraries to this APK")
args = parser.parse_args()


def native(archive):
    return {name: archive.read(name) for name in archive.namelist() if name.startswith("lib/") and name.endswith(".so")}


for apk in args.apks:
    with zipfile.ZipFile(apk) as archive:
        assert archive.testzip() is None, (apk.name, "CRC failure")
        libraries = native(archive)
        assert libraries, (apk.name, "no native libraries")
        count = 0
        for name, data in libraries.items():
            assert data[:4] == b"\x7fELF", name
            if data[4] != 2:
                continue
            endian = {1: "<", 2: ">"}[data[5]]
            table = struct.unpack_from(endian + "Q", data, 32)[0]
            size, number = struct.unpack_from(endian + "HH", data, 54)
            assert size >= 56 and number > 0, name
            loads = 0
            for index in range(number):
                position = table + size * index
                kind = struct.unpack_from(endian + "I", data, position)[0]
                if kind != 1:
                    continue
                offset, virtual = struct.unpack_from(endian + "QQ", data, position + 8)
                align = struct.unpack_from(endian + "Q", data, position + 48)[0]
                assert align >= 16_384 and align & (align - 1) == 0, (name, "LOAD p_align", align)
                assert offset % align == virtual % align, (name, "LOAD congruence")
                loads += 1
            assert loads > 0, (name, "no LOAD segments")
            count += 1
        assert count > 0, (apk.name, "no 64-bit native libraries")
        if args.compare:
            with zipfile.ZipFile(args.compare) as previous:
                old = native(previous)
            assert libraries.keys() == old.keys(), "Native file list changed"
            assert all(hashlib.sha256(value).digest() == hashlib.sha256(old[name]).digest()
                       for name, value in libraries.items()), "Native bytes changed"
        print(f"PASS {apk.name}: {count} 64-bit ELF files have 16KB LOAD alignment; {len(libraries)} libraries total")
