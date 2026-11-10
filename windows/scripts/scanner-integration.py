#!/usr/bin/env python3
"""Exercise the shipped scanner's JSON pipe and UDP/TCP/DoT/DoH with loopback fixtures."""
import base64
import hashlib
import hmac
import http.server
import json
import os
from pathlib import Path
import socket
import socketserver
import ssl
import struct
import subprocess
import tempfile
import threading
import time

WINDOWS = Path(__file__).resolve().parents[1]
EXE = WINDOWS / "engines/linux-x64/rovunet-transports"
ZONE = "scan.fixture.test"
KEY = bytes(range(32))
BAD = False

def respond(packet):
    offset = 12
    labels = []
    while packet[offset]:
        length = packet[offset]
        offset += 1
        labels.append(packet[offset:offset+length].decode("ascii"))
        offset += length
    qend = offset + 5
    typ = int.from_bytes(packet[offset+1:offset+3], "big")
    answer = b""
    if typ == 16 and len(labels[0]) == 52:
        nonce_proof = base64.b32decode(labels[0].upper() + "====")
        nonce = nonce_proof[:16]
        if hmac.compare_digest(hmac.digest(KEY, nonce, "sha256")[:16], nonce_proof[16:]):
            proof = hmac.digest(KEY, nonce+b"\x01", "sha256")
            if BAD:
                proof = bytes(32)
            data = bytes([len(proof)]) + proof
            answer = b"\xc0\x0c"+struct.pack("!HHIH",16,1,0,len(data))+data
    return packet[:2] + struct.pack("!HHHHH",0x8100,1,bool(answer),0,0) + packet[12:qend]+answer

class UDP(socketserver.BaseRequestHandler):
    def handle(self):
        packet, sock = self.request
        sock.sendto(respond(packet), self.client_address)

class Stream(socketserver.StreamRequestHandler):
    def handle(self):
        prefix = self.rfile.read(2)
        if len(prefix) == 2:
            answer = respond(self.rfile.read(int.from_bytes(prefix, "big")))
            self.wfile.write(struct.pack("!H",len(answer))+answer)

class HTTP(http.server.BaseHTTPRequestHandler):
    def do_POST(self):
        assert self.headers["Content-Type"] == "application/dns-message"
        data = respond(self.rfile.read(int(self.headers["Content-Length"])))
        self.send_response(200)
        self.send_header("Content-Type","application/dns-message")
        self.send_header("Content-Length",str(len(data)))
        self.end_headers()
        self.wfile.write(data)
    def log_message(self, *args):
        pass

def invoke(mode, address, env):
    req = dict(Mode="prism",Family="dnstt",Zone=ZONE,PublicKey=KEY.hex(),Candidates=[address],Transports=[mode],
               Workers=2,TimeoutMS=2500, Count=4, Threshold=2, ResponseSize=512)
    with subprocess.Popen([str(EXE),"--scan"],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,env=env) as proc:
        try:
            proc.stdin.write(json.dumps(req).encode()+b"\n")
            proc.stdin.flush()
            proc.wait(timeout=6)
            assert proc.returncode==0
            row=json.loads(proc.stdout.read())
            assert "Index" in row and row["Index"]==0 and row["Transport"]==mode and address not in str(row) and KEY.hex() not in str(row)
            return row
        finally:
            if proc.poll() is None:
                proc.kill()

result = []
with tempfile.TemporaryDirectory(prefix="rovunet-scan-test-") as temp:
    cert, key = Path(temp)/"cert.pem",Path(temp)/"key.pem"
    subprocess.run(["openssl","req","-x509","-newkey","rsa:2048","-nodes","-days","1","-subj","/CN=localhost",
                    "-addext","subjectAltName=IP:127.0.0.1","-keyout",str(key),"-out",str(cert)], stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL, check=True)
    tls=ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    tls.load_cert_chain(cert,key)
    trusted=dict(os.environ,SSL_CERT_FILE=str(cert))
    servers=[]
    try:
        for mode in ["udp","tcp","tls","https"]:
            server = (socketserver.ThreadingUDPServer(("127.0.0.1",0),UDP) if mode=="udp" else
                      http.server.ThreadingHTTPServer(("127.0.0.1",0),HTTP) if mode=="https" else
                      socketserver.ThreadingTCPServer(("127.0.0.1",0),Stream))
            if mode in ("tls","https"):
                server.socket=tls.wrap_socket(server.socket,server_side=True)
            servers.append(server)
            threading.Thread(target=server.serve_forever,daemon=True).start()
            addr="127.0.0.1:"+str(server.server_address[1])
            if mode=="https":
                addr="https://"+addr+"/dns-query"
            BAD=False
            row=invoke(mode,addr,trusted)
            assert row["Pass"] and row["Passed"]==row["Attempted"]==2,(mode,row)
            BAD=True
            row=invoke(mode,addr,trusted)
            assert not row["Pass"] and row["Attempted"]==3,(mode,row)
            BAD=False
            if mode in ("tls","https"):
                assert not invoke(mode,addr,os.environ)["Pass"],"Untrusted certificate accepted"
            result.append(dict(transport=mode,valid_prism=True,wrong_hmac_rejected=True,threshold=2,
                               untrusted_certificate_rejected=mode in ("tls","https")))
    finally:
        for server in servers:
            server.shutdown()
            server.server_close()

with socket.socket(type=socket.SOCK_DGRAM) as silent:
    silent.bind(("127.0.0.1",0))
    req=dict(Mode="advanced",Family="dnstt",Zone=ZONE,Candidates=["127.0.0.1:"+str(silent.getsockname()[1])],Transports=["udp"],
             Workers=1,TimeoutMS=90000,Count=4,Threshold=2)
    with subprocess.Popen([str(EXE),"--scan"],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL) as proc:
        proc.stdin.write(json.dumps(req).encode()+b"\n");proc.stdin.flush();silent.settimeout(3);silent.recvfrom(4096)
        clock=time.monotonic();proc.stdin.close();proc.wait(timeout=3);elapsed=round((time.monotonic()-clock)*1000)
        assert proc.returncode==0
print(json.dumps(dict(host="Linux",scanner_transports=result,stdin_eof_cancel_ms=elapsed,windows_executed=False)))
