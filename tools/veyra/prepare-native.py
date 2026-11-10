#!/usr/bin/env python3
"""Build pinned Xray, reviewed Hev/JNI and lifecycle helpers; reuse other pinned 2.5.6 Full transports.
Requires Go (auto toolchain enabled), JDK21, Android SDK36 and NDK29.
Uses upstream's checked-in full Java bindings with the exact released JNI library.
"""
import hashlib,os,pathlib,subprocess,tempfile,time,zipfile
from fetch_verified import download_verified
ROOT=pathlib.Path(__file__).resolve().parents[2]
PIN='63ca2e0c1d31b89f67700c33842431f541fedd25'
APK_URL='https://github.com/anonvector/SlipNet/releases/download/v2.5.6/SlipNet-v2.5.6-full-release-universal.apk'
APK_SHA='5eccaabf112eb9dce7d438bf9262e20afd35c02699dec8aa5ec7cb7bd06aa965'
SDK=pathlib.Path(os.environ['ANDROID_HOME']);NDK=pathlib.Path(os.environ.get('ANDROID_NDK_HOME',str(SDK/'ndk/29.0.14206865')))
JAVA=pathlib.Path(os.environ['JAVA_HOME'])/'bin';GO=os.environ.get('GO','go')
def run(args,**kw):subprocess.run([str(x) for x in args],check=True,**kw)
with tempfile.TemporaryDirectory(prefix='veyra-native-') as tmp:
 temp=pathlib.Path(tmp);apk=temp/'slipnet.apk'
 download_verified(apk,APK_SHA,[APK_URL,'https://api.github.com/repos/anonvector/SlipNet/releases/assets/704535849?download=1&t='+str(int(time.time()))],200*1024*1024)
 if hashlib.sha256(apk.read_bytes()).hexdigest()!=APK_SHA:raise SystemExit('Upstream APK hash mismatch')
 with zipfile.ZipFile(apk) as z:
  for name in z.namelist():
   if name.startswith('lib/') and name.endswith('.so') and not any(x in name for x in ['libandroidx.graphics.path','libdatastore_shared_counter']):
    out=ROOT/'app/src/main/jniLibs'/name.removeprefix('lib/');out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(z.read(name))
 source=temp/'bindings';classes=temp/'classes';source.mkdir();classes.mkdir()
 with zipfile.ZipFile(ROOT/'app/libs/golibs-full-sources.jar') as z:z.extractall(source)
 # Upstream sources.jar omits this binding; its exact signature exists in the
 # pinned release's JNI exports and soar-mobile/soar/mobile.go.
 binding=source/'soar/SoarClient.java'
 code=binding.read_text()
 if 'setDiscoveryCandidates' not in code:
  binding.write_text(code.replace('public native void setDiscovery(boolean enabled, String country);','public native void setDiscovery(boolean enabled, String country);\n public native void setDiscoveryCandidates(String list);'))
 run([JAVA/'javac','--release','8','-classpath',SDK/'platforms/android-36/android.jar','-d',classes]+list(source.rglob('*.java')))
 jar=temp/'classes.jar'
 with zipfile.ZipFile(jar,'w',zipfile.ZIP_DEFLATED) as z:
  for p in classes.rglob('*.class'):z.write(p,str(p.relative_to(classes)))
 with zipfile.ZipFile(ROOT/'app/libs/golibs-full.aar','w',zipfile.ZIP_DEFLATED) as z:
  z.write(jar,'classes.jar');z.writestr('AndroidManifest.xml','<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="go.golibs"/>')
  # Reconstructed AARs must retain gomobile's JNI consumer rules. R8 cannot
  # infer the non-native constructors/fields/callbacks referenced from C.
  z.writestr('proguard.txt',(ROOT/'app/gomobile-consumer-rules.pro').read_bytes())
 core=temp/'xray';run(['git','clone','https://github.com/XTLS/Xray-core.git',core]);run(['git','checkout',PIN],cwd=core)
 for abi,arch,target in [('arm64-v8a','arm64','aarch64-linux-android24'),('armeabi-v7a','arm','armv7a-linux-androideabi24'),('x86_64','amd64','x86_64-linux-android24')]:
  cc=NDK/'toolchains/llvm/prebuilt/linux-x86_64/bin'/(target+'-clang');dest=ROOT/'app/src/main/jniLibs'/abi
  env=os.environ.copy();env.update(GOOS='android',GOARCH=arch,CGO_ENABLED='1',CC=str(cc),GOARM='7')
  run([GO,'build','-trimpath','-ldflags=-s -w -checklinkname=0','-o',dest/'libveyra_xray.so','./main'],cwd=core,env=env)
  run([cc,'-O2','-fPIE','-pie','-Wl,-z,max-page-size=16384',ROOT/'app/src/main/cpp/veyra-core-launcher.c','-o',dest/'libveyra_core_launcher.so'])
print('Prepared Full native engines. Build: ./gradlew :app:assembleFullDebug -PveyraPrebuiltNative=true')

# Rebuild the packet engine and JNI lifecycle wrapper from the reviewed source.
# Other native transports remain the pinned upstream payloads above.
with tempfile.TemporaryDirectory(prefix='rovunet-hev-') as temp:
 run([NDK/'ndk-build','NDK_PROJECT_PATH=null',f'APP_BUILD_SCRIPT={ROOT}/app/src/main/cpp/Android.mk',
      f'NDK_APPLICATION_MK={ROOT}/app/src/main/cpp/Application.mk',f'NDK_OUT={temp}/obj',
      f'NDK_LIBS_OUT={temp}/libs','APP_ABI=arm64-v8a armeabi-v7a x86_64','APP_MODULES=hev-tunnel-jni','-j2'])
 for abi in ['arm64-v8a','armeabi-v7a','x86_64']:
  for name in ['libhev-socks5-tunnel.so','libhev-tunnel-jni.so']:
   source=pathlib.Path(temp)/'libs'/abi/name
   (ROOT/'app/src/main/jniLibs'/abi/name).write_bytes(source.read_bytes())
print('Rebuilt Hev packet engine and JNI wrapper for all three supported ABIs; preserved other transports.')

# Slipstream Android certificate pinning adds a JNI entry point: a pinned release
# library is not compatible with this bridge. Rebuild it instead of silently reusing.
run(['python3', ROOT/'tools/native/build-slipstream.py'])
