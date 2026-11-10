"""Synthetic DNS-encapsulated QUIC peer with local TCP echo and two public pins."""
import base64, json, os, pathlib, socket, subprocess, tempfile, threading, time
from contextlib import contextmanager

@contextmanager
def slipstream_fixture(root):
    binary = root/'app/src/main/rust/slipstream-rust/target/release/slipstream-server'
    with tempfile.TemporaryDirectory(prefix='rovunet-slipstream-fixture-') as tmp:
        directory=pathlib.Path(tmp)
        for name in ('server','other'):
            subprocess.run(['openssl','req','-x509','-newkey','rsa:2048','-nodes','-subj','/CN=synthetic-slipstream','-days','1',
                '-keyout',directory/(name+'.key'),'-out',directory/(name+'.pem')],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
        listener=socket.socket();listener.bind(('0.0.0.0',0));listener.listen(16);listener.settimeout(.2)
        stopping=threading.Event();clients=[]
        def echo(client):
            try:
                with client:
                    client.settimeout(10)
                    while not stopping.is_set():
                        data=client.recv(4096)
                        if not data:break
                        client.sendall(data)
            except OSError: pass
        def accept():
            while not stopping.is_set():
                try:client,_=listener.accept()
                except socket.timeout:continue
                except OSError:break
                clients.append(client);threading.Thread(target=echo,args=(client,),daemon=True).start()
        threading.Thread(target=accept,daemon=True).start()
        udp=socket.socket(socket.AF_INET,socket.SOCK_DGRAM);udp.bind(('0.0.0.0',0));port=udp.getsockname()[1];udp.close()
        log=(directory/'server.log').open('w+')
        process=subprocess.Popen([binary,'--dns-listen-host','0.0.0.0','--dns-listen-port',str(port),'--target-address',f'127.0.0.1:{listener.getsockname()[1]}',
            '--domain','tunnel.example.invalid','--cert',directory/'server.pem','--key',directory/'server.key'],stdout=log,stderr=log,env=dict(os.environ,RUST_LOG='warn'))
        try:
            time.sleep(.5)
            if process.poll() is not None:raise RuntimeError('Synthetic Slipstream peer failed to start')
            data=json.dumps({'port':port,'certificate':(directory/'server.pem').read_text(),'wrongCertificate':(directory/'other.pem').read_text()},separators=(',',':'))
            yield base64.b64encode(data.encode()).decode()
        finally:
            stopping.set();listener.close()
            for client in clients:
                try:client.close()
                except OSError:pass
            process.terminate()
            try:process.wait(timeout=5)
            except subprocess.TimeoutExpired:process.kill();process.wait()
            log.close()
