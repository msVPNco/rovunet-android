#!/usr/bin/env python3
"""Build the constrained AWG child from pinned, GPL-3.0 Mihomo sources."""
import os,pathlib,shutil,subprocess,tempfile
ROOT=pathlib.Path(__file__).resolve().parents[2]
PIN='3f27ef39f8c96bbfbc8b0b9bb10f5c9ef60149d8'
def run(args,**kw):subprocess.run([str(x) for x in args],check=True,**kw)
with tempfile.TemporaryDirectory(prefix='rovunet-awg-build-') as tmp:
 source=pathlib.Path(tmp)/'mihomo'
 run(['git','clone','--no-checkout','https://github.com/MetaCubeX/mihomo.git',source]);run(['git','checkout',PIN],cwd=source)
 # Backport upstream c65570ed0924a7e48a48160463c68015b9a84ca8 / PR3359.
 # Preserve the reviewed core pin; domain-length byte arithmetic must not crash the child.
 patch=ROOT/'tools/native/mihomo-socks-domain.patch'
 run(['git','apply','--check',patch],cwd=source)
 run(['git','apply',patch],cwd=source)
 for name,target in [('outbound.go','adapter/outbound/rovunet_handshake.go'),('certificates.go','component/ca/rovunet.go'),('publish_unix.go','component/ca/rovunet_publish_unix.go'),('publish_other.go','component/ca/rovunet_publish_other.go')]:
  shutil.copy2(ROOT/'tools/native/handshake'/name,source/target)
 ca_config=source/'component/ca/config.go'
 ca_text=ca_config.read_text()
 needle='\treturn tlsConfig, nil\n'
 assert ca_text.count(needle)==1
 ca_config.write_text(ca_text.replace(needle,'\trovunetAutomaticCertificate(tlsConfig, opt)\n'+needle))
 dest=source/'cmd/rovunet-awg';shutil.copytree(ROOT/'tools/native/awg',dest)
 go=os.environ.get('GO','go')
 run([go,'test','./adapter/inbound','-run','TestParseSocksAddr(DomainLengths|FQDN)$','-count=1'],cwd=source)
 if os.environ.get('ROVUNET_AWG_HOST'):
  run([go,'test','./cmd/rovunet-awg'],cwd=source)
  run([go,'build','-trimpath','-ldflags=-s -w -checklinkname=0','-o',os.environ['ROVUNET_AWG_HOST'],'./cmd/rovunet-awg'],cwd=source)
 else:
  ndk=pathlib.Path(os.environ['ANDROID_NDK_HOME'])
  for abi,arch,target in [('arm64-v8a','arm64','aarch64-linux-android24'),('armeabi-v7a','arm','armv7a-linux-androideabi24'),('x86_64','amd64','x86_64-linux-android24')]:
   env=os.environ.copy();env.update(GOOS='android',GOARCH=arch,GOARM='7',CGO_ENABLED='1',CC=str(ndk/'toolchains/llvm/prebuilt/linux-x86_64/bin'/(target+'-clang')))
   out=ROOT/'app/src/main/jniLibs'/abi/'librovunet_awg.so';out.parent.mkdir(parents=True,exist_ok=True)
   run([go,'build','-trimpath','-buildmode=pie','-ldflags=-s -w -checklinkname=0','-o',out,'./cmd/rovunet-awg'],cwd=source,env=env)
  fixture=ROOT/'app/build/rovunet-test-fixtures/udp-protocols'
  fixture.parent.mkdir(parents=True,exist_ok=True)
  run([go,'test','-c','-o',fixture,'./cmd/rovunet-awg'],cwd=source)
