"""Disposable real WG/HY2/TUIC peers that reject all external data destinations."""
import base64
import json
import os
from pathlib import Path
import select
import subprocess
from contextlib import contextmanager

@contextmanager
def udp_protocol_fixture(root):
    binary = Path(root) / 'app/build/rovunet-test-fixtures/udp-protocols'
    process = subprocess.Popen([str(binary)], env=dict(os.environ, ROVUNET_UDP_DEVICE_FIXTURE='1'),
                               stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
    try:
        if not select.select([process.stdout], [], [], 20)[0]:
            raise RuntimeError('UDP handshake fixture did not become ready')
        fixture = json.loads(process.stdout.readline())
        if len(fixture['links']) != 3 or not fixture['wrongWireguard']:
            raise RuntimeError('Incomplete UDP handshake fixture')
        yield base64.b64encode(json.dumps(fixture).encode()).decode()
    finally:
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
