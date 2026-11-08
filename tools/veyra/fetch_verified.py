"""Download exact build inputs without accepting a mirror or an unchecked retry."""
import hashlib
import os
from pathlib import Path
import tempfile
import urllib.request


def download_verified(target: Path, expected: str, sources: list[str], max_bytes: int):
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.is_file() and hashlib.sha256(target.read_bytes()).hexdigest() == expected:
        return
    for source in sources:
        temporary = None
        try:
            request = urllib.request.Request(source, headers={'Accept': 'application/octet-stream', 'User-Agent': 'Rovunet-build'})
            with urllib.request.urlopen(request, timeout=45) as response, tempfile.NamedTemporaryFile(dir=target.parent, delete=False) as output:
                temporary = Path(output.name)
                digest = hashlib.sha256()
                size = 0
                while block := response.read(1024*1024):
                    size += len(block)
                    if size > max_bytes:
                        raise ValueError('Build input exceeds size bound')
                    digest.update(block)
                    output.write(block)
            if digest.hexdigest() != expected:
                raise ValueError('Build input checksum mismatch')
            os.replace(temporary, target)
            return
        except Exception as error:
            # A redirect can contain credentials. Keep the URL and exception text private.
            print('Build source unavailable: ' + type(error).__name__, flush=True)
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
    raise RuntimeError('No checksum-verified input is available from official sources')
