#!/usr/bin/env python3
"""Offline Linux fixtures: Xray counters over HTTP/2 and DNS UDP/TCP framing."""
import argparse,json,pathlib,socket,socketserver,http.server,http.client,subprocess,threading,select,struct,time
p=argparse.ArgumentParser();p.add_argument('--dotnet',required=True);a=p.parse_args();w=pathlib.Path(__file__).resolve().parents[1];dll=w/'tests/Rovunet.Tests/bin/Release/net10.0/Rovunet.Tests.dll'
def invoke(*args,input=None,ok=True):
 r=subprocess.run([a.dotnet,str(dll),*map(str,args)],input=input,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,timeout=12)
 if ok:assert r.returncode==0
 return r,json.loads(r.stdout)if r.stdout else None

def free():
 with socket.socket()as s:s.bind(('127.0.0.1',0));return s.getsockname()[1]
def exact(s,n):
 b=b''
 while len(b)<n:
  part=s.recv(n-len(b))
  if not part:raise EOFError()
  b+=part
 return b
class TCP(socketserver.ThreadingTCPServer):daemon_threads=True;allow_reuse_address=True
class Socks(socketserver.BaseRequestHandler):
 def handle(self):
  s=self.request;s.settimeout(4)
  try:
   v,n=exact(s,2);exact(s,n);s.sendall(b'\x05\x00');v,cmd,z,kind=exact(s,4)
   host=socket.inet_ntop(socket.AF_INET,exact(s,4))if kind==1 else exact(s,exact(s,1)[0]).decode();port=int.from_bytes(exact(s,2),'big');assert host=='127.0.0.1' and cmd==1
   with socket.create_connection((host,port),4)as remote:
    s.sendall(b'\x05\x00\x00\x01\x7f\x00\x00\x01\x00\x00')
    while True:
     ready,_,_=select.select([s,remote],[],[],4)
     if not ready:return
     for src in ready:
      data=src.recv(8192)
      if not data:return
      (remote if src is s else s).sendall(data)
  except (OSError,EOFError):pass
payload=b'rovunet-counter-fixture'*300
class Target(http.server.BaseHTTPRequestHandler):
 def do_GET(self):self.send_response(200);self.send_header('Content-Length',str(len(payload)));self.end_headers();self.wfile.write(payload)
 def log_message(self,*args):pass
backend=TCP(('127.0.0.1',0),Socks);target=http.server.ThreadingHTTPServer(('127.0.0.1',0),Target)
for server in [backend,target]:threading.Thread(target=server.serve_forever,daemon=True).start()
hp,sp,mp=free(),free(),free();link=f'socks5://127.0.0.1:{backend.server_address[1]}'
config=subprocess.run([a.dotnet,str(dll),'--runtime-stats',str(hp),str(sp),str(mp)],input=link.encode(),stdout=subprocess.PIPE,check=True).stdout
child=subprocess.Popen([str(w/'engines/linux-x64/xray'),'run','-c','stdin:'],stdin=subprocess.PIPE,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
child.stdin.write(config);child.stdin.close();evidence={}
try:
 for _ in range(80):
  try:
   with socket.create_connection(('127.0.0.1',hp),.1):break
  except OSError:time.sleep(.05)
 counts=[]
 for _ in range(2):
  c=http.client.HTTPConnection('127.0.0.1',hp,timeout=4);c.request('GET',f'http://127.0.0.1:{target.server_address[1]}/');r=c.getresponse();assert r.status==200 and r.read()==payload;c.close();_,stats=invoke('--stats',mp);counts.append(stats)
 assert counts[1]['Uploaded']>counts[0]['Uploaded']>0 and counts[1]['Downloaded']>counts[0]['Downloaded']>=len(payload)
 evidence['xray_stats']={'passed':True,'transport':'HTTP/2 gRPC','payload_bytes':2*len(payload),'snapshots':counts,'loopback_only':True,'windows_executed':False}
finally:
 child.terminate();child.wait(timeout=5);backend.shutdown();backend.server_close();target.shutdown();target.server_close()
 for port in [hp,sp,mp]:
  try:socket.create_connection(('127.0.0.1',port),.1)
  except OSError:continue
  raise AssertionError('LISTENER_LEFT_OPEN')
evidence['xray_stats']['listeners_closed']=True
results=[]
for mode in ['udp','tcp']:
 for kind in ['reply','wrong_id','truncated','nxdomain','timeout']:
  sock=socket.socket(socket.AF_INET,socket.SOCK_DGRAM if mode=='udp' else socket.SOCK_STREAM);sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
  if mode=='tcp':sock.listen()
  def serve(sock=sock,mode=mode,kind=kind):
   try:
    if mode=='udp':query,addr=sock.recvfrom(4096);stream=None
    else:stream,addr=sock.accept();query=exact(stream,int.from_bytes(exact(stream,2),'big'))
    reply=bytearray(query);reply[2]|=128
    if kind=='wrong_id':reply[0]^=1
    if kind=='truncated':reply[2]|=2
    if kind=='nxdomain':reply[3]|=3
    if kind!='timeout':
     if mode=='udp':sock.sendto(reply,addr)
     else:stream.sendall(len(reply).to_bytes(2,'big')+reply)
    else:time.sleep(.85)
    if stream:stream.close()
   finally:sock.close()
  t=threading.Thread(target=serve);t.start();r,data=invoke('--dns-probe',f'127.0.0.1:{port}',mode,ok=False);t.join(timeout=2);assert data['Replied']==(kind=='reply');results.append({'mode':mode,'case':kind,'passed':True})
evidence['dns_protocol_checks']=results
out=w/'artifacts/evidence/stats-dns.json';out.write_text(json.dumps(evidence,indent=2)+'\n');print(json.dumps(evidence))
