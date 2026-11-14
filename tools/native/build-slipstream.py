#!/usr/bin/env python3
"""Build the pinned Slipstream source plus the tracked Android pinning patch, all Full ABIs."""
import os, pathlib, subprocess
ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'app/src/main/rust/slipstream-rust'
PIN = '60a5e38c7414b35e4c2fddf8d4d89a4341470c6a'
def run(args, **kw): subprocess.run([str(x) for x in args], check=True, **kw)
run(['git', 'submodule', 'update', '--init', 'app/src/main/rust/slipstream-rust'], cwd=ROOT)
assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=SOURCE, text=True).strip() == PIN
run(['git', 'submodule', 'update', '--init', '--recursive'], cwd=SOURCE)
patch = ROOT / 'tools/native/slipstream-android.patch'
if subprocess.run(['git', 'apply', '--reverse', '--check', str(patch)], cwd=SOURCE, capture_output=True).returncode:
    run(['git', 'apply', '--check', patch], cwd=SOURCE)
    run(['git', 'apply', patch], cwd=SOURCE)
ndk = pathlib.Path(os.environ['ANDROID_NDK_HOME'])
bin_dir = ndk / 'toolchains/llvm/prebuilt/linux-x86_64/bin'
for abi, target, clang in [('arm64-v8a','aarch64-linux-android','aarch64-linux-android24'), ('armeabi-v7a','armv7-linux-androideabi','armv7a-linux-androideabi24'), ('x86_64','x86_64-linux-android','x86_64-linux-android24')]:
    run(['rustup', 'target', 'add', target])
    env = os.environ.copy()
    env.update(ANDROID_ABI=abi, ANDROID_PLATFORM='android-24', PICOQUIC_AUTO_BUILD='1',
               PICOQUIC_BUILD_DIR=str(SOURCE/'.picoquic-build'/abi), BUILD_TYPE='Release',
               CC=str(bin_dir/(clang+'-clang')), AR=str(bin_dir/'llvm-ar'),
               RANLIB=str(bin_dir/'llvm-ranlib'),
               RUSTFLAGS='-C link-arg=-Wl,-z,max-page-size=16384 -C link-arg=-Wl,-soname,librovunet_slipstream.so')
    env['CARGO_TARGET_'+target.upper().replace('-','_')+'_LINKER'] = str(bin_dir/(clang+'-clang'))
    run(['cargo','build','--locked','--release','--target',target,'-p','slipstream-client','--lib','--features','openssl-vendored,picoquic-minimal-build','-j2'], cwd=SOURCE, env=env)
    dest = ROOT / 'app/src/main/jniLibs'/abi/'librovunet_slipstream.so'
    dest.write_bytes((SOURCE/'target'/target/'release/libslipstream.so').read_bytes())
print('Rebuilt patched pinned Slipstream JNI library for all three Full ABIs.')

# A matching synthetic server is used by emulator tests, never shipped in the APK.
host_env = os.environ.copy()
host_env.update(PICOQUIC_AUTO_BUILD='1', PICOQUIC_BUILD_DIR=str(SOURCE/'.picoquic-build/host'), BUILD_TYPE='Release',
                CARGO_BUILD_JOBS='2', CARGO_PROFILE_DEV_DEBUG='0', CARGO_PROFILE_TEST_DEBUG='0')
format_result = subprocess.run(['cargo','fmt','--all','--','--check'], cwd=SOURCE)
if format_result.returncode:
    # The pinned upstream/previous Android overlay has formatting drift outside
    # this change. Keep those sources intact, as requested by the owner.
    print('Existing upstream formatting drift retained; checking the edited verifier separately.', flush=True)
run(['rustfmt','--check','--edition','2021','crates/slipstream-client/src/pinning.rs'], cwd=SOURCE)
run(['python3', ROOT/'tools/native/check-slipstream-clippy.py', SOURCE], cwd=SOURCE, env=host_env)
run(['cargo','test','--locked','-p','slipstream-client','--features','openssl-vendored,picoquic-minimal-build','pinning::tests'], cwd=SOURCE, env=host_env)
run(['cargo','build','--locked','--release','-p','slipstream-server','--features','openssl-vendored,picoquic-minimal-build','-j2'], cwd=SOURCE, env=host_env)
