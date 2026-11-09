#!/usr/bin/env python3
"""Loopback integration: actual Kotlin-generated client JSON and pinned Xray server.
Not an Android, public-network censorship, or independent-implementation test.
"""
import base64,concurrent.futures,hashlib,http.server,json,os,pathlib,socket,struct,subprocess,tempfile,threading,time,urllib.parse
from cryptography.hazmat.primitives.asymmetric.x25519 import X25519PrivateKey
from cryptography.hazmat.primitives.serialization import Encoding,PrivateFormat,PublicFormat,NoEncryption
XRAY=os.environ['XRAY'];JARS=os.environ['KOTLIN_JARS'];TEST_JAR=os.environ['CORE_TEST_JAR']
results=[]
def port(kind=socket.SOCK_STREAM):
 with socket.socket(type=kind) as s:s.bind(('127.0.0.1',0));return s.getsockname()[1]
def pair():
 k=X25519PrivateKey.generate();return (base64.b64encode(k.private_bytes(Encoding.Raw,PrivateFormat.Raw,NoEncryption())).decode(),base64.b64encode(k.public_key().public_bytes(Encoding.Raw,PublicFormat.Raw)).decode())
def recv(s,n):
 b=b''
 while len(b)<n:
  x=s.recv(n-len(b))
  if not x:raise RuntimeError('unexpected EOF')
  b+=x
 return b
def socks(p,cmd,target):
 s=socket.create_connection(('127.0.0.1',p),timeout=8);s.settimeout(8);s.sendall(b'\x05\x01\x00');assert recv(s,2)==b'\x05\x00'
 s.sendall(b'\x05'+bytes([cmd])+b'\x00\x01'+socket.inet_aton(target[0])+struct.pack('!H',target[1]))
 h=recv(s,4);assert h[1]==0,h
 if h[3]==1:host=socket.inet_ntoa(recv(s,4))
 elif h[3]==4:host=socket.inet_ntop(socket.AF_INET6,recv(s,16))
 else:host=recv(s,recv(s,1)[0]).decode()
 return s,(host,struct.unpack('!H',recv(s,2))[0])
class Handler(http.server.BaseHTTPRequestHandler):
 def do_GET(self):
  body=b'veyra-real-tunnel-test\n'*200
  self.send_response(200);self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
 def log_message(self,*a):pass
http=http.server.ThreadingHTTPServer(('127.0.0.1',0),Handler);threading.Thread(target=http.serve_forever,daemon=True).start()
httpport=http.server_address[1]
udp=socket.socket(type=socket.SOCK_DGRAM);udp.bind(('127.0.0.1',0));udpport=udp.getsockname()[1]
def echo():
 while True:
  try:b,a=udp.recvfrom(65535);udp.sendto(b,a)
  except OSError:return
threading.Thread(target=echo,daemon=True).start()
with tempfile.TemporaryDirectory(prefix='veyra-integration-') as tmp:
 root=pathlib.Path(tmp);cert=root/'cert.pem';keyfile=root/'key.pem'
 subprocess.run(['openssl','req','-x509','-newkey','rsa:2048','-nodes','-days','1','-subj','/CN=localhost','-addext','subjectAltName=DNS:localhost','-keyout',str(keyfile),'-out',str(cert)],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
 env=os.environ.copy();env['SSL_CERT_FILE']=str(cert)
 def start(cfg,name):
  path=root/(name+'.json');path.write_text(json.dumps(cfg));log=open(root/(name+'.log'),'wb')
  p=subprocess.Popen([XRAY,'run','-c',str(path)],stdout=log,stderr=log,env=env);return p
 def request(p):
  with socks(p,1,('10.77.0.1',httpport))[0] as s:
   s.sendall(b'GET / HTTP/1.0\r\nHost: localhost\r\n\r\n');buf=b''
   while True:
    x=s.recv(8192)
    if not x:break
    buf+=x
   assert buf.split(b'\r\n\r\n',1)[1]==b'veyra-real-tunnel-test\n'*200
 def request_udp(p):
  control,relay=socks(p,3,('0.0.0.0',0))
  try:
   with socket.socket(type=socket.SOCK_DGRAM) as u:
    u.settimeout(8);msg=b'udp-veyra-check';frame=b'\0\0\0\1'+socket.inet_aton('10.77.0.1')+struct.pack('!H',udpport)+msg
    u.sendto(frame,relay);out,_=u.recvfrom(65535);assert out.endswith(msg)
  finally:control.close()
 uid='00000000-0000-4000-8000-000000000001'
 for mode in os.environ.get('PROTOCOLS','reality,xhttp,hysteria2,wireguard,shadowsocks').split(','):
  sp=port(socket.SOCK_DGRAM if mode in ['hysteria2','wireguard'] else socket.SOCK_STREAM);cp=port();extra=[];additionalLinks=[]
  inbound={'listen':'127.0.0.1','port':sp}
  tls={'certificates':[{'certificateFile':str(cert),'keyFile':str(keyfile)}],'alpn':['h3'] if mode=='hysteria2' else ['h2','http/1.1']}
  if mode=='reality':
   priv,pub=pair();dest=port()
   target=subprocess.Popen([os.environ['TLS_TARGET'],f'127.0.0.1:{dest}',str(cert),str(keyfile)],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL);extra.append(target)
   inbound.update(protocol='vless',settings={'clients':[{'id':uid,'flow':'xtls-rprx-vision'}],'decryption':'none'},streamSettings={'network':'raw','security':'reality','realitySettings':{'target':f'127.0.0.1:{dest}','serverNames':['localhost'],'privateKey':priv.rstrip('=').replace('+','-').replace('/','_'),'shortIds':['ab']}})
   link=f'vless://{uid}@127.0.0.1:{sp}?security=reality&type=tcp&sni=localhost&fp=chrome&pbk={pub.rstrip("=").replace("+","-").replace("/","_")}&sid=ab&flow=xtls-rprx-vision'
  elif mode=='xhttp':
   inbound.update(protocol='vless',settings={'clients':[{'id':uid}],'decryption':'none'},streamSettings={'network':'xhttp','security':'tls','tlsSettings':tls,'xhttpSettings':{'path':'/veyra','mode':'auto'}})
   link=f'vless://{uid}@127.0.0.1:{sp}?security=tls&type=xhttp&sni=localhost&host=localhost&path=%2Fveyra&mode=packet-up'
  elif mode=='shadowsocks':
   inbound.update(protocol='shadowsocks',settings={'method':'aes-256-gcm','password':'synthetic-aead-password','network':'tcp,udp'})
   credentials=base64.urlsafe_b64encode(b'aes-256-gcm:synthetic-aead-password').decode().rstrip('=')
   link=f'ss://{credentials}@127.0.0.1:{sp}#Synthetic-AEAD'
  elif mode=='hysteria2':
   inbound.update(protocol='hysteria',settings={'version':2,'users':[{'auth':'test-password'}]},streamSettings={'network':'hysteria','security':'tls','tlsSettings':tls,'hysteriaSettings':{'version':2},'finalmask':{'udp':[{'type':'salamander','settings':{'password':'obfs-test'}}]}})
   link=f'hysteria2://test-password@127.0.0.1:{sp}?sni=localhost&obfs=salamander&obfs-password=obfs-test'
  else:
   serverPriv,serverPub=pair();clientPriv,clientPub=pair()
   inbound.update(protocol='wireguard',settings={'secretKey':serverPriv,'address':['10.66.0.1/32'],'peers':[{'publicKey':clientPub,'allowedIPs':['10.66.0.2/32']}],'noKernelTun':True,'mtu':1280})
   link=f'[Interface]\nPrivateKey = {clientPriv}\nAddress = 10.66.0.2/32\n[Peer]\nPublicKey = {serverPub}\nEndpoint = 127.0.0.1:{sp}\nAllowedIPs = 0.0.0.0/0\n'
   for n in range(1,int(os.environ.get('WG_DEVICES','1'))):
    devicePrivate,devicePublic=pair();address=f'10.66.0.{n+2}'
    inbound['settings']['peers'].append({'publicKey':devicePublic,'allowedIPs':[address+'/32']})
    additionalLinks.append(f'[Interface]\nPrivateKey = {devicePrivate}\nAddress = {address}/32\n[Peer]\nPublicKey = {serverPub}\nEndpoint = 127.0.0.1:{sp}\nAllowedIPs = 0.0.0.0/0\n')
  source=root/(mode+'.link');source.write_text(link);clientFile=root/(mode+'-client.json')
  subprocess.run(['java','-cp',f'{TEST_JAR}:{JARS}/kotlin-stdlib-2.2.21.jar:{JARS}/json.jar','CoreFixtureKt',str(source),str(clientFile),str(cp)],check=True)
  server=start({'log':{'loglevel':'debug'},'inbounds':[inbound],'outbounds':[{'protocol':'freedom','settings':{'redirect':'127.0.0.1:0','finalRules':[{'action':'allow','ip':['127.0.0.1/32']}]}}]},mode+'-server')
  clientCfg=json.loads(clientFile.read_text())
  clientCfg['log']={'loglevel':'debug'}
  if mode in ['xhttp','hysteria2']:
   clientCfg['outbounds'][0]['streamSettings']['tlsSettings']['certificates']=[{'certificateFile':str(cert),'usage':'verify'}]
  client=start(clientCfg,mode+'-client');processes=[client,server]+extra
  clientPorts=[cp]
  for n,extraLink in enumerate(additionalLinks):
   p=port();clientPorts.append(p);linkFile=root/f'device-{n}.conf';linkFile.write_text(extraLink);configFile=root/f'device-{n}.json'
   subprocess.run(['java','-cp',f'{TEST_JAR}:{JARS}/kotlin-stdlib-2.2.21.jar:{JARS}/json.jar','CoreFixtureKt',str(linkFile),str(configFile),str(p)],check=True)
   processes.append(start(json.loads(configFile.read_text()),f'device-{n}'))
  try:
   time.sleep(.6)
   for p in clientPorts:request(p);request_udp(p)
   t=time.monotonic()
   with concurrent.futures.ThreadPoolExecutor(max_workers=40) as pool:list(pool.map(lambda n:request(clientPorts[n%len(clientPorts)]),range(200)))
   results.append({'protocol':mode,'tcp':True,'udp':True,'requests':200,'concurrency':40,'client_processes':len(clientPorts),'seconds':round(time.monotonic()-t,3)})
   print(json.dumps(results[-1]),flush=True)
  except Exception as e:
   results.append({'protocol':mode,'error':type(e).__name__+': '+str(e)});print(json.dumps(results[-1]),flush=True)
   for f in root.glob(mode+'*.log'):print(f.name,f.read_text()[-5000:],flush=True)
  finally:
   for p in processes:
    p.terminate()
   for p in processes:
    try:p.wait(timeout=3)
    except subprocess.TimeoutExpired:p.kill();p.wait()
http.shutdown();udp.close()
pathlib.Path(os.environ.get('TEST_REPORT','/tmp/veyra-integration.json')).write_text(json.dumps(results,indent=2))
if any('error' in r for r in results):raise SystemExit(1)
