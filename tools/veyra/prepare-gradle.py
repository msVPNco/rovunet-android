#!/usr/bin/env python3
"""Warm the wrapper cache from official sources; require its versioned SHA-256."""
import hashlib
import os
from pathlib import Path
import tempfile
import time
import urllib.request

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
    raise SystemExit(0)
if archive.is_file() and hashlib.sha256(archive.read_bytes()).hexdigest() == expected:
    print('Verified cached Gradle 8.13 archive')
    raise SystemExit(0)

# The ordinary release redirect can fail with a stale CDN response. The second
# URL is the same official release asset through GitHub's public API, not a mirror.
sources = [url, 'https://api.github.com/repos/gradle/gradle-distributions/releases/assets/232373824?download=1&t=' + str(int(time.time()))]
for source in sources:
    temporary = None
    try:
        request = urllib.request.Request(source, headers={'Accept': 'application/octet-stream', 'User-Agent': 'Rovunet-build'})
        with urllib.request.urlopen(request, timeout=45) as response, tempfile.NamedTemporaryFile(dir=directory, delete=False) as output:
            temporary = Path(output.name)
            digest = hashlib.sha256()
            size = 0
            while block := response.read(1024*1024):
                size += len(block)
                if size > 200*1024*1024:
                    raise ValueError('Gradle archive exceeds size bound')
                digest.update(block)
                output.write(block)
        if digest.hexdigest() != expected:
            raise ValueError('Gradle archive checksum mismatch')
        os.replace(temporary, archive)
        print('Prepared checksum-verified Gradle 8.13 for the wrapper')
        break
    except Exception as error:
        print('Gradle source unavailable: ' + type(error).__name__)
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)
else:
    raise SystemExit('No checksum-verified Gradle archive available')
