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
class HopPortRelay:
 """Emulate server-side UDP DNAT: all public ports share one QUIC listener.
 Separate Xray inbounds have separate connection-ID tables and cannot migrate
 an established QUIC session between ports. No root/network rule is required.
 """
 def __init__(self,backend):
  self.closed=False;self.seen=set();self.latest=None;self.lock=threading.Lock();self.front=[]
  self.upstream=socket.socket(type=socket.SOCK_DGRAM);self.upstream.connect(('127.0.0.1',backend));self.upstream.settimeout(.2)
  for _ in range(3):
   sock=socket.socket(type=socket.SOCK_DGRAM);sock.bind(('127.0.0.1',0));sock.settimeout(.2);self.front.append(sock)
   threading.Thread(target=self.forward,args=(sock,),daemon=True).start()
  threading.Thread(target=self.reply,daemon=True).start()
 def forward(self,sock):
  while not self.closed:
   try:
    data,client=sock.recvfrom(65535)
    with self.lock:self.latest=(sock,client);self.seen.add(sock.getsockname()[1])
    self.upstream.send(data)
   except socket.timeout:pass
   except OSError:return
 def reply(self):
  while not self.closed:
   try:
    data=self.upstream.recv(65535)
    with self.lock:target=self.latest
    if target:target[0].sendto(data,target[1])
   except socket.timeout:pass
   except OSError:return
 def close(self):
  self.closed=True
  for sock in self.front+[self.upstream]:sock.close()

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
 for mode in os.environ.get('PROTOCOLS','reality,xhttp,hysteria2,wireguard,shadowsocks,trojan,vmess-ws,vless-grpc,vless-httpupgrade,vless-ws-heartbeat,vless-kcp,vless-encrypted-xhttp,wireguard-uri,shadowsocks2022-aes128,shadowsocks2022-chacha,trojan-fm,vless-xhttp-fm,hysteria2-hop').split(','):
  sp=port(socket.SOCK_DGRAM if mode in ['hysteria2','hysteria2-hop','wireguard','wireguard-uri','vless-kcp'] else socket.SOCK_STREAM);cp=port();extra=[];additionalLinks=[];serverExtra=[];hopRelay=None
  inbound={'listen':'127.0.0.1','port':sp}
  tls={'certificates':[{'certificateFile':str(cert),'keyFile':str(keyfile)}],'alpn':['h3'] if mode in ['hysteria2','hysteria2-hop'] else ['h2','http/1.1']}
  if mode=='reality':
   priv,pub=pair();dest=port()
   target=subprocess.Popen([os.environ['TLS_TARGET'],f'127.0.0.1:{dest}',str(cert),str(keyfile)],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL);extra.append(target)
   inbound.update(protocol='vless',settings={'clients':[{'id':uid,'flow':'xtls-rprx-vision'}],'decryption':'none'},streamSettings={'network':'raw','security':'reality','realitySettings':{'target':f'127.0.0.1:{dest}','serverNames':['localhost'],'privateKey':priv.rstrip('=').replace('+','-').replace('/','_'),'shortIds':['ab']}})
   link=f'vless://{uid}@127.0.0.1:{sp}?security=reality&type=tcp&sni=localhost&fp=chrome&pbk={pub.rstrip("=").replace("+","-").replace("/","_")}&sid=ab&flow=xtls-rprx-vision'
  elif mode in ['xhttp','vless-xhttp-fm']:
   inbound.update(protocol='vless',settings={'clients':[{'id':uid}],'decryption':'none'},streamSettings={'network':'xhttp','security':'tls','tlsSettings':tls,'xhttpSettings':{'path':'/veyra','mode':'auto'}})
   link=f'vless://{uid}@127.0.0.1:{sp}?security=tls&type=xhttp&sni=localhost&host=localhost&path=%2Fveyra&mode=packet-up'
  elif mode=='vless-encrypted-xhttp':
   private,public=pair()
   rawurl=lambda key:key.rstrip('=').replace('+','-').replace('/','_')
   inbound.update(protocol='vless',settings={'clients':[{'id':uid}],'decryption':'mlkem768x25519plus.native.600s.'+rawurl(private)},streamSettings={'network':'xhttp','security':'none','xhttpSettings':{'path':'/encrypted-fixture','mode':'auto'}})
   encryption='mlkem768x25519plus.native.0rtt.'+rawurl(public)
   link=f'vless://{uid}@127.0.0.1:{sp}?security=none&type=xhttp&path=%2Fencrypted-fixture&mode=packet-up&encryption={encryption}'
  elif mode.startswith('shadowsocks2022'):
   method='2022-blake3-aes-128-gcm' if mode.endswith('aes128') else '2022-blake3-chacha20-poly1305'
   password=base64.b64encode(os.urandom(16 if mode.endswith('aes128') else 32)).decode()
   inbound.update(protocol='shadowsocks',settings={'method':method,'password':password,'network':'tcp,udp'})
   credentials=base64.urlsafe_b64encode((method+':'+password).encode()).decode().rstrip('=')
   link=f'ss://{credentials}@127.0.0.1:{sp}#Synthetic-2022'
  elif mode=='shadowsocks':
   inbound.update(protocol='shadowsocks',settings={'method':'aes-256-gcm','password':'synthetic-aead-password','network':'tcp,udp'})
   credentials=base64.urlsafe_b64encode(b'aes-256-gcm:synthetic-aead-password').decode().rstrip('=')
   link=f'ss://{credentials}@127.0.0.1:{sp}#Synthetic-AEAD'
  elif mode in ['trojan','trojan-fm','vmess-ws','vless-grpc','vless-httpupgrade','vless-ws-heartbeat','vless-kcp']:
   protocol='trojan' if mode in ['trojan','trojan-fm'] else 'vmess' if mode=='vmess-ws' else 'vless'
   network='raw' if mode in ['trojan','trojan-fm'] else 'ws' if mode in ['vmess-ws','vless-ws-heartbeat'] else 'grpc' if mode=='vless-grpc' else 'kcp' if mode=='vless-kcp' else 'httpupgrade'
   settings={'clients':[{'password':'synthetic-password'}]} if protocol=='trojan' else {'clients':[{'id':uid}]}
   if protocol=='vless':settings['decryption']='none'
   stream={'network':network,'security':'tls','tlsSettings':tls}
   if network=='ws':stream['wsSettings']={'path':'/fixture','headers':{'Host':'localhost'}}
   if network=='grpc':stream['grpcSettings']={'serviceName':'fixture'}
   if network=='httpupgrade':stream['httpupgradeSettings']={'path':'/fixture','host':'localhost'}
   if network=='kcp':stream['kcpSettings']={'mtu':1200,'tti':20}
   inbound.update(protocol=protocol,settings=settings,streamSettings=stream)
   if protocol=='vmess':
    payload={'v':'2','ps':'Synthetic VMess','add':'127.0.0.1','port':str(sp),'id':uid,'aid':'0','scy':'auto','net':'ws','type':'none','host':'localhost','path':'/fixture','tls':'tls','sni':'localhost'}
    link='vmess://'+base64.b64encode(json.dumps(payload).encode()).decode()
   else:
    auth='synthetic-password' if protocol=='trojan' else uid
    link=f'{protocol}://{auth}@127.0.0.1:{sp}?security=tls&type={network}&sni=localhost&host=localhost&path=%2Ffixture'
    if network=='grpc':link=f'{protocol}://{auth}@127.0.0.1:{sp}?security=tls&type=grpc&sni=localhost&serviceName=fixture'
    if network=='kcp':link=f'{protocol}://{auth}@127.0.0.1:{sp}?security=tls&type=kcp&sni=localhost&mtu=1200&tti=20'
    if mode=='vless-ws-heartbeat':link+='&heartbeatPeriod=30'
    link+='#Synthetic display name'
  elif mode in ['hysteria2','hysteria2-hop']:
   inbound.update(protocol='hysteria',settings={'version':2,'users':[{'auth':'test-password'}]},streamSettings={'network':'hysteria','security':'tls','tlsSettings':tls,'hysteriaSettings':{'version':2},'finalmask':{'udp':[{'type':'salamander','settings':{'password':'obfs-test'}}]}})
   link=f'hysteria2://test-password@127.0.0.1:{sp}?sni=localhost&obfs=salamander&obfs-password=obfs-test'
  else:
   serverPriv,serverPub=pair();clientPriv,clientPub=pair()
   inbound.update(protocol='wireguard',settings={'secretKey':serverPriv,'address':['10.66.0.1/32'],'peers':[{'publicKey':clientPub,'allowedIPs':['10.66.0.2/32']}],'noKernelTun':True,'mtu':1280})
   link=f'[Interface]\nPrivateKey = {clientPriv}\nAddress = 10.66.0.2/32\n[Peer]\nPublicKey = {serverPub}\nEndpoint = 127.0.0.1:{sp}\nAllowedIPs = 0.0.0.0/0\n'
   if mode=='wireguard-uri':
    query=urllib.parse.urlencode({'publickey':serverPub,'address':'10.66.0.2/32','allowedips':'0.0.0.0/0'})
    link=f'wireguard://{urllib.parse.quote(clientPriv,safe="")}@127.0.0.1:{sp}/?{query}#Synthetic-WireGuard'
   for n in range(1,int(os.environ.get('WG_DEVICES','1'))):
    devicePrivate,devicePublic=pair();address=f'10.66.0.{n+2}'
    inbound['settings']['peers'].append({'publicKey':devicePublic,'allowedIPs':[address+'/32']})
    additionalLinks.append(f'[Interface]\nPrivateKey = {devicePrivate}\nAddress = {address}/32\n[Peer]\nPublicKey = {serverPub}\nEndpoint = 127.0.0.1:{sp}\nAllowedIPs = 0.0.0.0/0\n')
  if mode in ['trojan-fm','vless-xhttp-fm']:
   mask={'tcp':[{'type':'fragment','settings':{'packets':'tlshello','length':'50-100','delay':'1-2'}}]}
   link+='&cs='+urllib.parse.quote('TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256',safe='')+'&fm='+urllib.parse.quote(json.dumps(mask),safe='')
  if mode=='hysteria2-hop':
   hopRelay=HopPortRelay(sp)
   link+='&mport='+','.join(str(sock.getsockname()[1]) for sock in hopRelay.front)+'&hop-interval=5s'
  source=root/(mode+'.link');source.write_text(link);clientFile=root/(mode+'-client.json')
  subprocess.run(['java','-cp',f'{TEST_JAR}:{JARS}/kotlin-stdlib-2.2.21.jar:{JARS}/json.jar:{JARS}/bcprov-jdk18on-1.80.jar','CoreFixtureKt',str(source),str(clientFile),str(cp)],check=True)
  server=start({'log':{'loglevel':'debug'},'inbounds':[inbound]+serverExtra,'outbounds':[{'protocol':'freedom','settings':{'redirect':'127.0.0.1:0','finalRules':[{'action':'allow','ip':['127.0.0.1/32']}]}}]},mode+'-server')
  clientCfg=json.loads(clientFile.read_text())
  clientCfg['log']={'loglevel':'debug'}
  if mode in ['xhttp','vless-xhttp-fm','hysteria2','hysteria2-hop','trojan-fm','trojan','vmess-ws','vless-grpc','vless-httpupgrade','vless-ws-heartbeat','vless-kcp']:
   clientCfg['outbounds'][0]['streamSettings']['tlsSettings']['certificates']=[{'certificateFile':str(cert),'usage':'verify'}]
  client=start(clientCfg,mode+'-client');processes=[client,server]+extra
  clientPorts=[cp]
  for n,extraLink in enumerate(additionalLinks):
   p=port();clientPorts.append(p);linkFile=root/f'device-{n}.conf';linkFile.write_text(extraLink);configFile=root/f'device-{n}.json'
   subprocess.run(['java','-cp',f'{TEST_JAR}:{JARS}/kotlin-stdlib-2.2.21.jar:{JARS}/json.jar:{JARS}/bcprov-jdk18on-1.80.jar','CoreFixtureKt',str(linkFile),str(configFile),str(p)],check=True)
   processes.append(start(json.loads(configFile.read_text()),f'device-{n}'))
  try:
   time.sleep(.6)
   for p in clientPorts:request(p);request_udp(p)
   if hopRelay:
    deadline=time.monotonic()+22
    while len(hopRelay.seen)<2 and time.monotonic()<deadline:
     time.sleep(.5);request(cp);request_udp(cp)
    assert len(hopRelay.seen)>=2,'No real remote-port change observed'
    request(cp);request_udp(cp)
   t=time.monotonic()
   with concurrent.futures.ThreadPoolExecutor(max_workers=40) as pool:list(pool.map(lambda n:request(clientPorts[n%len(clientPorts)]),range(200)))
   results.append({'protocol':mode,'tcp':True,'udp':True,'requests':200,'concurrency':40,'client_processes':len(clientPorts),'seconds':round(time.monotonic()-t,3)})
   print(json.dumps(results[-1]),flush=True)
  except Exception as e:
   results.append({'protocol':mode,'error':type(e).__name__+': '+str(e)});print(json.dumps(results[-1]),flush=True)
   for f in root.glob(mode+'*.log'):print(f.name,f.read_text()[-5000:],flush=True)
  finally:
   if hopRelay:hopRelay.close()
   for p in processes:
    p.terminate()
   for p in processes:
    try:p.wait(timeout=3)
    except subprocess.TimeoutExpired:p.kill();p.wait()
http.shutdown();udp.close()
pathlib.Path(os.environ.get('TEST_REPORT','/tmp/veyra-integration.json')).write_text(json.dumps(results,indent=2))
if any('error' in r for r in results):raise SystemExit(1)
