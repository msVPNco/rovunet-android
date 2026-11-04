#!/usr/bin/env python3
"""Linux validation of the exact Android launcher C source, PID-namespace safe."""
import os,pathlib,subprocess,tempfile,time
root=pathlib.Path(__file__).resolve().parents[2]
def alive(pid):
 try:os.kill(pid,0);return True
 except ProcessLookupError:return False
with tempfile.TemporaryDirectory() as tmp:
 binary=pathlib.Path(tmp)/'launcher';pidfile=pathlib.Path(tmp)/'core.pid'
 subprocess.run(['cc','-O2',str(root/'app/src/main/cpp/veyra-core-launcher.c'),'-o',str(binary)],check=True)
 code=f'''import subprocess,threading,time
 def start():
  p=subprocess.Popen([{str(binary)!r},'/bin/sh','-c',{('echo $$ > '+str(pidfile)+'; exec sleep 60')!r}]);print(p.pid,flush=True)
 thread=threading.Thread(target=start);thread.start();thread.join();time.sleep(60)
'''
 code=code.replace('\n ', '\n')
 parent=subprocess.Popen(['python3','-c',code],stdout=subprocess.PIPE,text=True)
 try:
  launcher=int(parent.stdout.readline())
  for _ in range(30):
   if pidfile.exists():break
   time.sleep(.05)
  child=int(pidfile.read_text());time.sleep(.4)
  assert alive(child), 'core died when spawning worker exited'
  parent.kill();parent.wait()
  for _ in range(40):
   if not alive(child):break
   time.sleep(.05)
  else:raise AssertionError('orphan core survived app death')
 finally:
  if parent.poll() is None:parent.kill();parent.wait()
 print('Worker thread exit preserves core; app process death terminates core: passed')
