#!/usr/bin/env python3
"""Reject mixed old/new client JNI payloads and accidental legacy codec replacement."""
import hashlib, pathlib, subprocess, sys, tempfile, zipfile
with zipfile.ZipFile(sys.argv[1]) as apk, tempfile.TemporaryDirectory() as temp:
    for abi in ('arm64-v8a','armeabi-v7a','x86_64'):
        old=apk.read('lib/'+abi+'/libslipstream.so')
        new=apk.read('lib/'+abi+'/librovunet_slipstream.so')
        path=pathlib.Path(temp)/'client.so';path.write_bytes(new)
        symbols=subprocess.check_output(['readelf','--dyn-syms','--wide',path],text=True)
        for suffix in ('nativeStartRovunetClient','nativeStopRovunetClient','nativeIsRovunetRunning','nativeIsRovunetQuicReady','nativeGetRovunetError','nativeSetRovunetCertificatePath'):
            assert 'Java_app_slipnet_tunnel_SlipstreamBridge_'+suffix in symbols, (abi,suffix)
        assert 'LockPasswordUtil_nativeGetConfigKey' not in symbols
        dynamic=subprocess.check_output(['readelf','--dynamic',path],text=True)
        assert 'librovunet_slipstream.so' in dynamic
        # prepare-native's source extraction is pinned by APK SHA-256; compare
        # the legacy bytes again after all client rebuilding/packaging steps.
        expected=(pathlib.Path(__file__).resolve().parents[2]/'app/src/main/jniLibs'/abi/'libslipstream.so').read_bytes()
        assert old==expected, 'Legacy codec bytes changed during packaging'
        print('PASS',abi,'separate client JNI/SONAME and preserved legacy codec',hashlib.sha256(old).hexdigest())
