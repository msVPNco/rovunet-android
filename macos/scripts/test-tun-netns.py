"""Run only inside a fresh Linux network namespace; never changes host routes."""
import argparse, http.server, ipaddress, json, os, pathlib, select, shutil, socket, struct, subprocess, sys, tempfile, threading, time

def run(*args):return subprocess.run(args,check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True).stdout
def serve():
    class Handler(http.server.BaseHTTPRequestHandler):
        def do_GET(self):
            body=b'Rovunet TUN fixture';self.send_response(200);self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
        def log_message(self,*args):pass
    class V6(http.server.ThreadingHTTPServer):address_family=socket.AF_INET6
    for server in [http.server.ThreadingHTTPServer(('0.0.0.0',8080),Handler),V6(('::',8081),Handler)]:threading.Thread(target=server.serve_forever,daemon=True).start()
    def udp(family,port,dns=False):
        s=socket.socket(family,socket.SOCK_DGRAM)
        if family==socket.AF_INET6:s.setsockopt(socket.IPPROTO_IPV6,socket.IPV6_V6ONLY,1)
        s.bind(('2001:db8:100::10' if family==socket.AF_INET6 else '203.0.113.10',port))
        while True:
            data,address=s.recvfrom(8192)
            if dns:
                p=12
                while p<len(data) and data[p]:p+=data[p]+1
                p+=5
                data=data[:2]+struct.pack('>5H',0x8180,1,1,0,0)+data[12:p]+b'\xc0\x0c\x00\x01\x00\x01\x00\x00\x00\x00\x00\x04'+ipaddress.ip_address('203.0.113.10').packed
            s.sendto(data,address)
    for family,port,dns in [(socket.AF_INET,7777,False),(socket.AF_INET6,7777,False),(socket.AF_INET,5353,True)]:threading.Thread(target=udp,args=(family,port,dns),daemon=True).start()
    print('ready',flush=True);time.sleep(150)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--inside',action='store_true');parser.add_argument('--servers',action='store_true');parser.add_argument('--driver');parser.add_argument('--worker');parser.add_argument('--xray');parser.add_argument('--report');parser.add_argument('--host-netns');args=parser.parse_args()
    if args.servers:serve();return
    if not args.inside:
        subprocess.run(['unshare','-n',sys.executable,__file__,'--inside','--driver',args.driver,'--xray',args.xray,'--report',args.report,'--host-netns',os.readlink('/proc/self/ns/net')]+(['--worker',args.worker] if args.worker else []),check=True);return
    if os.geteuid()!=0 or os.readlink('/proc/self/ns/net')==args.host_netns or [item['ifname'] for item in json.loads(run('ip','-j','link'))]!=['lo']:raise RuntimeError('Not a fresh isolated root namespace')
    env=os.environ.copy()
    for key in list(env):
        if key.lower().endswith('_proxy'):env.pop(key)
    work=pathlib.Path(tempfile.mkdtemp(prefix='rovunet-tun-netns-'));processes=[]
    try:
        peer=subprocess.Popen(['unshare','-n','sleep','150']);processes.append(peer);time.sleep(.1)
        run('ip','link','set','lo','up');run('ip','link','add','rvclient','type','veth','peer','name','rvserver');run('ip','link','set','rvserver','netns',str(peer.pid))
        def server(*values):return run('nsenter','-t',str(peer.pid),'-n','--',*values)
        for execute,name,address,address6 in [(run,'rvclient','10.231.0.2/24','fd00:231::2/64'),(server,'rvserver','10.231.0.1/24','fd00:231::1/64')]:
            execute('ip','link','set','lo','up');execute('ip','addr','add',address,'dev',name);execute('ip','-6','addr','add',address6,'dev',name,'nodad');execute('ip','link','set',name,'up')
        run('ip','route','add','default','via','10.231.0.1','dev','rvclient');run('ip','-6','route','add','default','via','fd00:231::1','dev','rvclient')
        server('ip','addr','add','203.0.113.10/32','dev','lo');server('ip','-6','addr','add','2001:db8:100::10/128','dev','lo','nodad')
        backend=subprocess.Popen(['nsenter','-t',str(peer.pid),'-n','--',sys.executable,__file__,'--servers'],stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,env=env);processes.append(backend)
        if not select.select([backend.stdout],[],[],8)[0] or backend.stdout.readline().strip()!='ready':raise RuntimeError('Fixture server failed')
        base=work/'Rovunet.app/Contents/MacOS';engines=base/'engines';engines.mkdir(parents=True);xray=engines/'xray';shutil.copyfile(args.xray,xray);xray.chmod(0o755)
        # Linux's INET_DIAG process lookup needs a concrete local UDP address.
        # Darwin uses the upstream wildcard-UDP PID fallback instead. Keep this
        # fixture difference explicit; no production Mac profile is changed.
        config={'log':{'access':str(work/'access.log'),'loglevel':'warning'},'inbounds':[{'listen':'127.0.0.1','port':10280,'protocol':'socks','settings':{'auth':'password','accounts':[{'user':'a'*64,'pass':'b'*64}],'udp':True}}],'outbounds':[{'tag':'v4','protocol':'freedom','sendThrough':'10.231.0.2'},{'tag':'v6','protocol':'freedom','sendThrough':'fd00:231::2'}],'routing':{'rules':[{'type':'field','ip':['::/0'],'outboundTag':'v6'}]}}
        core=subprocess.Popen([str(xray),'run','-c','stdin:'],stdin=subprocess.PIPE,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,env=env,text=True);processes.append(core);core.stdin.write(json.dumps(config));core.stdin.close()
        for _ in range(50):
            try:s=socket.create_connection(('127.0.0.1',10280),.1);s.close();break
            except OSError:time.sleep(.1)
        packets=[]
        def spy():
            raw=socket.socket(socket.AF_PACKET,socket.SOCK_RAW,socket.htons(3));raw.bind(('rvclient',0))
            while len(packets)<40:
                try:packet=raw.recv(8192)
                except OSError:return
                if len(packet)>42 and packet[12:14]==b'\x08\x00' and packet[23]==17:
                    start=14+(packet[14]&15)*4;packets.append([socket.inet_ntoa(packet[26:30]),socket.inet_ntoa(packet[30:34]),*struct.unpack_from('>HH',packet,start)])
        threading.Thread(target=spy,daemon=True).start()
        env.update(ROVUNET_NETNS_TEST='1',ROVUNET_TEST_BASE=str(base),ROVUNET_TEST_SOCKS='10280')
        os.chmod(args.driver,0o755);result=subprocess.run([args.driver,'-test.v','-test.run=^TestTunNetworkFixture$','-test.timeout=65s'],env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=75)
        access=(work/'access.log').read_text() if (work/'access.log').exists() else ''
        observed={name:needle in access for name,needle in [('socksTcp4','tcp:203.0.113.10:8080'),('socksTcp6','tcp:[2001:db8:100::10]:8081'),('socksUdp4','udp:203.0.113.10:7777'),('socksUdp6','udp:[2001:db8:100::10]:7777'),('socksDns','udp:203.0.113.10:5353')]}
        worker_eof=None
        if result.returncode==0 and args.worker:
            os.chmod(args.worker,0o755);worker=subprocess.Popen([args.worker,'--worker'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,env=env);processes.append(worker)
            body=json.dumps({'Proxy':{'Action':'start','SocksPort':10280,'SocksUser':'a'*64,'SocksPassword':'b'*64},'Base':str(base)}).encode();worker.stdin.write(struct.pack('<I',len(body))+body);worker.stdin.flush()
            if not select.select([worker.stdout],[],[],10)[0]:raise RuntimeError('Production worker did not acknowledge')
            size=struct.unpack('<I',worker.stdout.read(4))[0]
            if size>16384:raise RuntimeError('Worker stdout corrupted the control frame')
            ready=json.loads(worker.stdout.read(size))
            if ready.get('State')!='active':raise RuntimeError('Production worker failed to start')
            worker.stdin.close();worker.wait(timeout=10)
            worker_eof=worker.returncode==0 and ready['Interface'] not in [item['ifname'] for item in json.loads(run('ip','-j','link'))] and 'rvclient' in run('ip','route','get','203.0.113.10')
        report={'scope':'isolated Linux namespaces, real Xray SOCKS with concrete Linux UDP source binding; not macOS validation','exit':result.returncode,'observed':observed,'workerPipeEofRestored':worker_eof,'packets':packets,'accessTail':access.splitlines()[-12:],'output':result.stdout[:120000],'passed':result.returncode==0 and all(observed.values()) and worker_eof is not False};pathlib.Path(args.report).write_text(json.dumps(report,indent=2));print(result.stdout[:12000]);print(json.dumps(observed));print('workerPipeEofRestored:',worker_eof)
        if not report['passed']:raise RuntimeError('TUN data-plane fixture failed')
    finally:
        for process in reversed(processes):
            process.terminate()
            try:process.wait(timeout=3)
            except subprocess.TimeoutExpired:process.kill();process.wait()
        if work.name.startswith('rovunet-tun-netns-') and work.parent==pathlib.Path('/tmp'):shutil.rmtree(work)
if __name__=='__main__':main()
