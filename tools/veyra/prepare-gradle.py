#!/usr/bin/env python3
"""Warm the wrapper cache from official sources; require its versioned SHA-256."""
import hashlib
import os
from pathlib import Path
import shutil
import time
from fetch_verified import download_verified

ROOT = Path(__file__).resolve().parents[2]
properties = dict(line.split('=', 1) for line in
    (ROOT/'gradle/wrapper/gradle-wrapper.properties').read_text().splitlines() if '=' in line)
url = properties['distributionUrl'].replace('\\:', ':')
expected = properties['distributionSha256Sum']
assert url.endswith('/gradle-8.13-bin.zip') and len(expected) == 64
value = int.from_bytes(hashlib.md5(url.encode()).digest(), 'big')
key = ''
while value:
    value, digit = divmod(value, 36)
    key = '0123456789abcdefghijklmnopqrstuvwxyz'[digit] + key
directory = Path(os.environ.get('GRADLE_USER_HOME', str(Path.home()/'.gradle')))/'wrapper/dists/gradle-8.13-bin'/key
directory.mkdir(parents=True, exist_ok=True)
archive = directory/'gradle-8.13-bin.zip'
if (directory/'gradle-8.13-bin.zip.ok').is_file() and (directory/'gradle-8.13/lib/gradle-launcher-8.13.jar').is_file():
    print('Using wrapper-verified Gradle 8.13 cache')
elif archive.is_file() and hashlib.sha256(archive.read_bytes()).hexdigest() == expected:
    print('Verified cached Gradle 8.13 archive')
else:
    # The same official release asset through a fresh API redirect, not a mirror.
    sources = [url, 'https://downloads.gradle.org/distributions/gradle-8.13-bin.zip', 'https://api.github.com/repos/gradle/gradle-distributions/releases/assets/232373824?download=1&t=' + str(int(time.time()))]
    download_verified(archive, expected, sources, 200*1024*1024)
    print('Prepared checksum-verified Gradle 8.13 for the wrapper')

# Maven's compiler download can also redirect to the failing release CDN.
# Serve just this exact official artifact from a verified file repository. A
# normal Gradle artifact-cache hit still HEADs the unavailable remote URL.
gradle_home = Path(os.environ.get('GRADLE_USER_HOME', str(Path.home()/'.gradle')))
compiler = gradle_home/'verified-inputs/kotlin-compiler-embeddable-2.2.21.jar'
download_verified(compiler, '9588fa696ac5507af764fd5660df6894185e12704ec1bc3f5cb772af4987f295', [
    'https://repo.maven.apache.org/maven2/org/jetbrains/kotlin/kotlin-compiler-embeddable/2.2.21/kotlin-compiler-embeddable-2.2.21.jar',
    'https://api.github.com/repos/JetBrains/kotlin/releases/assets/587847942?download=1&t=' + str(int(time.time()))], 100*1024*1024)
maven = gradle_home/'verified-maven/org/jetbrains/kotlin/kotlin-compiler-embeddable/2.2.21'
maven.mkdir(parents=True, exist_ok=True)
shutil.copyfile(compiler, maven/compiler.name)
download_verified(maven/'kotlin-compiler-embeddable-2.2.21.pom',
    '0b33c64f2622ba3bc8283378bdb7505f2be330a2275807038ad54359b33ddf9b',
    ['https://repo.maven.apache.org/maven2/org/jetbrains/kotlin/kotlin-compiler-embeddable/2.2.21/kotlin-compiler-embeddable-2.2.21.pom'], 1024*1024)
print('Prepared checksum-verified Kotlin 2.2.21 file repository')
