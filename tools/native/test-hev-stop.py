#!/usr/bin/env python3
"""Real packet-engine cancellation against silent and half-handshaken local SOCKS peers.
No root, TUN device, external destination or production credentials required.
"""
import ctypes, os, pathlib, socket, struct, subprocess, sys, threading, time
ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT/'app/src/main/cpp/hev-socks5-tunnel-src'
def checksum(data):
    if len(data)%2: data+=b'\0'
    n=sum(struct.unpack('!%dH'%(len(data)//2), data))
    while n>>16: n=(n&65535)+(n>>16)
    return (~n)&65535
def packet(udp=False, seq=1, ack=0, flags=2, data=b''):
    src=socket.inet_aton('10.255.255.2');dst=socket.inet_aton('198.18.0.13')
    proto=17 if udp else 6
    payload=(struct.pack('!HHHH',40100,53,12,0)+b'test') if udp else struct.pack('!HHIIBBHHH',40100,80,seq,ack,80,flags,65535,0,0)+data
    if not udp:
        c=checksum(src+dst+struct.pack('!BBH',0,proto,len(payload))+payload)
        payload=payload[:16]+struct.pack('!H',c)+payload[18:]
    ip=struct.pack('!BBHHHBBH',69,0,20+len(payload),1,0,64,proto,0)+src+dst
    return ip[:10]+struct.pack('!H',checksum(ip))+ip[12:]+payload

def child(mode):
    # lwIP's standalone host library exports weak allocator fallbacks. Load
    # the real task allocator first, matching Android's static link resolution.
    ctypes.CDLL(str(SOURCE/'third-part/hev-task-system/bin/libhev-task-system.so'), mode=ctypes.RTLD_GLOBAL)
    lib=ctypes.CDLL(str(SOURCE/'bin/libhev-socks5-tunnel.so'))
    lib.hev_socks5_tunnel_main_from_str.argtypes=[ctypes.c_char_p,ctypes.c_size_t,ctypes.c_int]
    with socket.socket() as server:
        server.bind(('127.0.0.1',0));server.listen(16);server.settimeout(4)
        ready=threading.Event(); finished=threading.Event()
        clients=[]
        def peer():
            try:
                conn,_=server.accept();clients.append(conn);conn.settimeout(4)
                if mode in ('request','active'):
                    assert conn.recv(1024)[0]==5
                    conn.sendall(b'\x05\x00')
                    assert conn.recv(1024)[0]==5
                    if mode=='active':
                        conn.sendall(b'\x05\x00\x00\x01\x7f\x00\x00\x01\x00\x50')
                        assert conn.recv(1024)==b'synthetic-data'
                ready.set();finished.wait(5)
            except Exception:
                ready.set()
        threading.Thread(target=peer,daemon=True).start()
        a,b=socket.socketpair(socket.AF_UNIX,socket.SOCK_DGRAM)
        a.setblocking(False)
        config=f'''tunnel:
  mtu: 1500
  ipv4: 10.255.255.1
  ipv6: fd00::1
socks5:
  address: 127.0.0.1
  port: {server.getsockname()[1]}
  udp: tcp
misc:
  task-stack-size: 32768
  connect-timeout: 8000
  tcp-read-write-timeout: 120000
  udp-read-write-timeout: 120000
  log-level: error
'''.encode()
        result=[]
        lib.hev_socks5_tunnel_prepare()
        def native():
            result.append(lib.hev_socks5_tunnel_main_from_str(config,len(config),a.fileno()))
        engine=threading.Thread(target=native,daemon=True)
        engine.start();time.sleep(.1)
        if mode!='idle':
            b.send(packet(udp=mode=='udp'))
            if mode!='udp':
                b.settimeout(3)
                response=b.recv(65535)
                assert response[9]==6 and response[33]&18==18, 'No TCP SYN ACK'
                peer_seq=struct.unpack('!I',response[24:28])[0]
                b.send(packet(seq=2,ack=peer_seq+1,flags=16))
                if mode=='active': b.send(packet(seq=2,ack=peer_seq+1,flags=24,data=b'synthetic-data'))
            assert ready.wait(3), 'SOCKS peer never reached'
            assert clients, 'No actual pending SOCKS socket'
            time.sleep(.05)
        started=time.monotonic();lib.hev_socks5_tunnel_quit();engine.join(2)
        elapsed=time.monotonic()-started
        assert not engine.is_alive(), f'{mode}: native stop blocked beyond 2s'
        assert result==[0],result
        finished.set()
        for c in clients:c.close()
        a.close();b.close()
        print(f'PASS {mode}: native stop {elapsed:.3f}s')
if __name__=='__main__':
    if len(sys.argv)>1:child(sys.argv[1])
    else:
        for mode in ('idle','greeting','request','active','udp'):
            for _ in range(5):
                env=os.environ.copy()
                env['LD_LIBRARY_PATH']=':'.join(str(SOURCE/'third-part'/p/'bin') for p in ('yaml','lwip','hev-task-system'))
                subprocess.run([sys.executable,__file__,mode],check=True,timeout=10,env=env)
