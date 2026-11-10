#!/usr/bin/env python3
"""Actual .NET-generated config + official Xray, local HTTPS.
Linux evidence only. No production endpoints, TLS bypass or direct fallback.
REALITY fixture design adapted from Rovunet Android tools/veyra/integration.py
(AGPL-3.0, based on SlipNet by Anonvector). All keys here are ephemeral fixtures.
"""
import base64,hashlib,http.server,json,os,pathlib,socket,ssl,subprocess,tempfile,threading,time
from cryptography.hazmat.primitives.asymmetric.x25519 import X25519PrivateKey
from cryptography.hazmat.primitives.serialization import Encoding,PublicFormat,PrivateFormat,NoEncryption
root=pathlib.Path(__file__).resolve().parents[1]
xray=root/'engines/linux-x64/xray'
dotnet=os.environ.get('DOTNET','dotnet')
dll=root/'tests/Rovunet.Tests/bin/Release/net10.0/Rovunet.Tests.dll'
output=root/'artifacts/evidence';output.mkdir(parents=True,exist_ok=True)
results=[]
def port():
    with socket.socket() as s:s.bind(('127.0.0.1',0));return s.getsockname()[1]
class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        b=b'rovunet-real-https-fixture\n'*256
        self.send_response(200);self.send_header('Content-Length',str(len(b)));self.end_headers();self.wfile.write(b)
    def log_message(self,*args):pass
def wait(p,process):
    end=time.monotonic()+8
    while time.monotonic()<end:
        if process.poll() is not None:raise RuntimeError('CORE_EXITED')
        try:
            with socket.create_connection(('127.0.0.1',p),timeout=.2):return
        except OSError:time.sleep(.05)
    raise RuntimeError('LISTENER_TIMEOUT')
def request(proxy,target,cert):
    # The destination address is unreachable directly. Only the server redirects
    # this test destination to its local HTTPS service.
    context=ssl.create_default_context(cafile=str(cert))
    with socket.create_connection(('127.0.0.1',proxy),timeout=8) as raw:
        raw.settimeout(8);raw.sendall(f'CONNECT 10.77.0.1:{target} HTTP/1.1\r\nHost: 10.77.0.1:{target}\r\n\r\n'.encode())
        header=b''
        while not header.endswith(b'\r\n\r\n'):
            chunk=raw.recv(1)
            if not chunk:raise RuntimeError('PROXY_EOF')
            header+=chunk
            if len(header)>8192:raise RuntimeError('HEADER_LIMIT')
        if header.split(b'\r\n',1)[0].split()[1]!=b'200':raise RuntimeError('CONNECT_FAILED')
        with context.wrap_socket(raw,server_hostname='localhost') as tls:
            tls.sendall(b'GET / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n');data=b''
            while b:=tls.recv(8192):data+=b
    body=data.split(b'\r\n\r\n',1)[1]
    if body!=b'rovunet-real-https-fixture\n'*256:raise RuntimeError('DATA_MISMATCH')
    return len(body)
with tempfile.TemporaryDirectory(prefix='rovunet-fixture-') as temp:
    td=pathlib.Path(temp);cert=td/'cert.pem';key=td/'key.pem'
    subprocess.run(['openssl','req','-x509','-newkey','rsa:2048','-nodes','-days','1','-subj','/CN=localhost','-addext','subjectAltName=DNS:localhost','-keyout',str(key),'-out',str(cert)],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    target=http.server.ThreadingHTTPServer(('127.0.0.1',0),Handler)
    context=ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER);context.minimum_version=ssl.TLSVersion.TLSv1_3;context.set_alpn_protocols(['h2','http/1.1']);context.load_cert_chain(cert,key)
    target.socket=context.wrap_socket(target.socket,server_side=True)
    threading.Thread(target=target.serve_forever,daemon=True).start();tp=target.server_address[1]
    def start(config,name):
        log=(td/(name+'.log')).open('wb')
        p=subprocess.Popen([str(xray),'run','-c','stdin:'],stdin=subprocess.PIPE,stdout=log,stderr=log)
        p.stdin.write(json.dumps(config).encode());p.stdin.close();log.close();return p
    uid='00000000-0000-4000-8000-000000000001'
    for mode in ['ws','reality','xhttp','hysteria2','wireguard','ws-fragment','ws-micro']:
        processes=[];cp=port();sp=port();sock=port();record={'protocol':mode,'host':'Linux','windows_validated':False,'udp_tested':False}
        try:
            stream={'network':'ws' if mode in ['ws','ws-fragment','ws-micro'] else 'xhttp' if mode=='xhttp' else 'raw','security':'none'}
            q='type=ws&security=none&path=%2Frovunet'
            clients=[{'id':uid}]
            if mode in ['ws','ws-fragment','ws-micro']:stream['wsSettings']={'path':'/rovunet'}
            if mode=='xhttp':
                stream.update(security='tls',tlsSettings={'certificates':[{'certificateFile':str(cert),'keyFile':str(key)}],'alpn':['h2','http/1.1']},xhttpSettings={'path':'/rovunet','mode':'auto'})
                q='type=xhttp&security=tls&sni=localhost&host=localhost&path=%2Frovunet&mode=packet-up'
            if mode=='reality':
                k=X25519PrivateKey.generate()
                priv=base64.urlsafe_b64encode(k.private_bytes(Encoding.Raw,PrivateFormat.Raw,NoEncryption())).decode().rstrip('=')
                pub=base64.urlsafe_b64encode(k.public_key().public_bytes(Encoding.Raw,PublicFormat.Raw)).decode().rstrip('=')
                stream.update(security='reality',realitySettings={'target':f'127.0.0.1:{tp}','serverNames':['localhost'],'privateKey':priv,'shortIds':['ab']})
                clients=[{'id':uid,'flow':'xtls-rprx-vision'}]
                q=f'type=tcp&security=reality&sni=localhost&fp=chrome&pbk={pub}&sid=ab&flow=xtls-rprx-vision'
            inbound={'listen':'127.0.0.1','port':sp,'protocol':'vless','settings':{'clients':clients,'decryption':'none'},'streamSettings':stream}
            link=f'vless://{uid}@127.0.0.1:{sp}?{q}'
            negative=lambda l:l.replace(uid,'00000000-0000-4000-8000-000000000002')
            if mode in ['ws-fragment','ws-micro']:
                stream.update(security='tls',tlsSettings={'certificates':[{'certificateFile':str(cert),'keyFile':str(key)}]})
                f=['']*88;f[0]='33';f[1]='vless';f[2]='Fixture';f[3]='localhost';f[31]=f[33]=f[34]=f[36]='0';f[62]=uid;f[63]='tls';f[64]='ws';f[65]='/rovunet';f[66]='127.0.0.1';f[67]=str(sp);f[68]='1';f[69]='sni_split';f[70]='0';f[72]='1' if mode=='ws-micro' else '0';f[73]='1';f[74]='1';f[77]='-1';f[78]='localhost';f[87]=base64.b64encode(json.dumps({'sshUser':'','sshPass':'','socksUser':'','socksPass':''}).encode()).decode()
                link='rovunet://'+base64.b64encode('|'.join(f).encode()).decode()
                def negative(l):
                    fields=base64.b64decode(l.split('://',1)[1]).decode().split('|');fields[62]='00000000-0000-4000-8000-000000000002';return 'rovunet://'+base64.b64encode('|'.join(fields).encode()).decode()
            if mode=='hysteria2':
                inbound.update(protocol='hysteria',settings={'version':2,'users':[{'auth':'test-password'}]},streamSettings={'network':'hysteria','security':'tls','tlsSettings':{'certificates':[{'certificateFile':str(cert),'keyFile':str(key)}],'alpn':['h3']},'hysteriaSettings':{'version':2},'finalmask':{'udp':[{'type':'salamander','settings':{'password':'obfs-test'}}]}})
                link=f'hysteria2://test-password@127.0.0.1:{sp}?sni=localhost&obfs=salamander&obfs-password=obfs-test'
                negative=lambda l:l.replace('test-password','wrong-password')
            if mode=='wireguard':
                def pair():
                    private=X25519PrivateKey.generate();return base64.b64encode(private.private_bytes(Encoding.Raw,PrivateFormat.Raw,NoEncryption())).decode(),base64.b64encode(private.public_key().public_bytes(Encoding.Raw,PublicFormat.Raw)).decode()
                serverPriv,serverPub=pair();clientPriv,clientPub=pair();wrongPriv,_=pair()
                inbound.pop('streamSettings');inbound.update(protocol='wireguard',settings={'secretKey':serverPriv,'address':['10.66.0.1/32'],'peers':[{'publicKey':clientPub,'allowedIPs':['10.66.0.2/32']}],'noKernelTun':True,'mtu':1280})
                link=f'[Interface]\nPrivateKey = {clientPriv}\nAddress = 10.66.0.2/32\n[Peer]\nPublicKey = {serverPub}\nEndpoint = 127.0.0.1:{sp}\nAllowedIPs = 0.0.0.0/0\n'
                negative=lambda l:l.replace(clientPriv,wrongPriv)
            server=start({'log':{'loglevel':'none'},'inbounds':[inbound], 'outbounds':[{'protocol':'freedom','settings':{'redirect':'127.0.0.1:0','finalRules':[{'action':'allow','ip':['127.0.0.1/32']}]}}]},mode+'-server');processes.append(server)
            if mode not in ['hysteria2','wireguard']:wait(sp,server)
            else:time.sleep(.25)
            def config_for(link):
                args=[dotnet,str(dll),'--runtime',str(cp),str(sock)]
                if mode in ['ws-fragment','ws-micro']:
                    hp=port();plan=subprocess.run([dotnet,str(dll),'--helper-config',str(hp)],input=link.encode(),stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,check=True)
                    helper=subprocess.Popen([str(root/'engines/linux-x64/rovunet-transports')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,env=dict(os.environ,SSL_CERT_FILE=str(cert)))
                    helper.stdin.write(plan.stdout+b'\n');helper.stdin.flush();ready=json.loads(helper.stdout.readline());processes.append(helper)
                    if ready.get('state')!='ready':raise RuntimeError('HELPER_NOT_READY')
                    args=[dotnet,str(dll),'--runtime-helper',str(cp),str(sock),str(hp)]
                generated=subprocess.run(args,input=link.encode(),stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,check=True)
                cfg=json.loads(generated.stdout)
                if mode in ['xhttp','hysteria2']:cfg['outbounds'][0]['streamSettings']['tlsSettings']['certificates']=[{'certificateFile':str(cert),'usage':'verify'}]
                return cfg
            client=start(config_for(link),mode+'-client');processes.append(client);wait(cp,client)
            size=request(cp,tp,cert);size+=request(cp,tp,cert)
            client.terminate();client.wait(timeout=4)
            # Same transport, wrong UUID: a working direct retry would make this pass.
            bad=start(config_for(negative(link)),mode+'-negative');processes.append(bad);wait(cp,bad)
            try:request(cp,tp,cert)
            except (OSError,RuntimeError,ssl.SSLError):record['bad_credentials_rejected']=True
            else:raise RuntimeError('DIRECT_FALLBACK_OR_AUTH_FAILURE')
            record.update(https_verified=True,payload_bytes=size,requests=2,default_tls_validation=True)
        except Exception as error:
            record.update(https_verified=False,error_type=type(error).__name__)
            if os.environ.get('FIXTURE_DEBUG'):
                print('Fixture error:',repr(error))
                for f in td.glob(mode+'*.log'): print(f.name,f.read_text()[-3000:])
        finally:
            for p in processes:
                if p.poll() is None:p.terminate()
            for p in processes:
                try:p.wait(timeout=4)
                except subprocess.TimeoutExpired:p.kill();p.wait()
            try:
                with socket.create_connection(('127.0.0.1',cp),timeout=.2):record['listener_closed']=False
            except OSError:record['listener_closed']=True
            results.append(record);print(json.dumps(record),flush=True)
    target.shutdown();target.server_close()
(output/'linux-integration.json').write_text(json.dumps(results,indent=2)+'\n')
raise SystemExit(0 if all(r.get('https_verified') and r.get('listener_closed') and r.get('bad_credentials_rejected') for r in results) else 1)
