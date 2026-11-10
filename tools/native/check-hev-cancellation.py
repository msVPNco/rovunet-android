#!/usr/bin/env python3
"""Run production yield cancellation checks and prove a removed guard fails them."""
import pathlib, subprocess, tempfile
ROOT=pathlib.Path(__file__).resolve().parents[2]
NATIVE=ROOT/'app/src/main/cpp/hev-socks5-tunnel-src'
source=NATIVE/'src/core/src/hev-socks5-misc.c'
with tempfile.TemporaryDirectory(prefix='rovunet-cancel-check-') as temp:
    temp=pathlib.Path(temp)
    for mutation in (False,True):
        code=source.read_text()
        if mutation:
            code=code.replace('    if (self->terminated)\n        return -1;\n','').replace('return self->terminated ? -1 : 0;','return 0;')
        path=temp/('mutated.c' if mutation else 'production.c');path.write_text(code)
        binary=path.with_suffix('')
        subprocess.run(['gcc','-ffunction-sections','-fdata-sections','-Wl,--gc-sections',
            '-I'+str(NATIVE/'src/core/src'),'-I'+str(NATIVE/'src/core/include'),
            '-I'+str(NATIVE/'third-part/hev-task-system/include'),ROOT/'tools/native/test-hev-cancellation.c',path,'-o',binary],check=True)
        result=subprocess.run([binary],capture_output=True,timeout=5)
        assert (result.returncode!=0) == mutation, result.stderr.decode()
        print('PASS cancellation mutation rejected' if mutation else 'PASS production sticky cancellation before/during wait and yield')
