#!/usr/bin/env python3
"""Publish only the owner-authorized alpha21 signed bytes, without a signing key in CI.

Restore public ZIP/signature metadata over the pinned passing unsigned CI artifact.
Keep the original source identity, seven exact Room results and prerelease channel.
"""
import argparse, base64, hashlib, io, json, os, re, shutil, struct, subprocess, sys, tarfile, tempfile, zipfile, zlib
from pathlib import Path, PurePosixPath

REPO = 'msVPNco/rovunet-app'
TAG = 'v0.3.0-alpha21'
SOURCE = '36e5f13e6aa1aa78ba7ea164d95f9f426445930f'
TREE = 'f7a07c83f1712822cea9c8e74013ad4b01d4e1a8'
CHECKOUT = 'd2130fed20445e355b584914f302dc7476a03c84'
RUN = 43504139712
ARTIFACT = 14077985965
CI_ZIP_SHA = '4fa8e5c5589ba9147058f3558ec0ef540f06dc02fe36051fc88667ddfd3a39fc'
ROOM_RUN = 43504141963
ROOM_ARTIFACT = 14074839305
ROOM_ZIP_SHA = '8c55500814bfc70cbe78309646d1b88cdb3e19a6045584de3ca18a13bb4a7690'
UNSIGNED_SHA = '938effab2bc354d746eb0539df2c7fb67f59d6b933a74fe5f8b6f997868ba05f'
APK_SHA = '04ba1d0569ca6c994f11b17bfa79eb26ae70d48f2535f944c8ad3feed5635428'
APK_SIZE = 158821929
CERT_SHA = '1e40bd201489a8209043e346255772823d9d52ee21b468bdef2c152d239bc416'
APK_NAME = 'Rovunet-v0.3.0-alpha21-Full.apk'
ASSET_NAMES = ('Rovunet-v0.3.0-alpha21-Full.apk', 'APK-SHA256.txt', 'APK-SIGNATURE-VERIFICATION.txt', 'Rovunet-production-certificate.pem', 'SIGNER-SHA256.txt', 'Rovunet-0.3.0-alpha21-source.zip', 'Rovunet-0.3.0-alpha21-source-light.zip', 'Rovunet-0.3.0-alpha21-Guide-FA.txt', 'Rovunet-alpha21-public-r8-mapping.zip', 'Rovunet-alpha21-CI-evidence.json', 'Rovunet-alpha21-CI-logs.zip', 'Rovunet-alpha21-Room-evidence.zip', 'NATIVE-INVENTORY.json', 'SOURCE-MANIFEST.json', 'VERIFICATION.json', 'SHA256SUMS.txt')
OLD_ASSET = 721633409
OLD_SHA = 'fd126cc5b293b2f12ce60a7045f9b6158f9c21e645b0dee7c316ba70d0363b27'
ROOT = Path(__file__).resolve().parents[2]
DATE = (2026, 11, 14, 0, 0, 0)

def sha(data):
    return hashlib.sha256(data).hexdigest()

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)

def json_write(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n')

def api(path, method='GET', payload=None, binary_path=None):
    command = ['gh', 'api', f'repos/{REPO}/{path}']
    if method != 'GET':
        command += ['--method', method]
    if payload is not None:
        command += ['--input', '-']
    if binary_path is not None:
        # Actions artifact redirects require the JSON media type; release assets
        # negotiate their binary bytes using octet-stream.
        media_type = 'application/octet-stream' if path.startswith('releases/assets/') else 'application/vnd.github+json'
        command += ['-H', 'Accept: ' + media_type]
        with binary_path.open('wb') as out:
            subprocess.run(command, stdout=out, check=True)
        return None
    data = subprocess.check_output(command, input=json.dumps(payload).encode() if payload is not None else None)
    return json.loads(data)

def verified_file(path, expected):
    if sha(path.read_bytes()) != expected:
        raise ValueError(f'Checksum mismatch: {path.name}')

def safe_path(value):
    p = PurePosixPath(value)
    if p.is_absolute() or '..' in p.parts or '\\' in value:
        raise ValueError('Unsafe archive entry')
    return str(p)

def restore_apk(ci_path, destination):
    verified_file(ci_path, CI_ZIP_SHA)
    with zipfile.ZipFile(ci_path) as archive:
        choices = [n for n in archive.namelist() if n.rsplit('/', 1)[-1] == 'Rovunet-v0.3.0-alpha21-full-release-universal-unsigned.apk']
        if len(choices) != 1:
            raise ValueError('Expected exactly one unsigned source APK')
        unsigned = archive.read(choices[0])
    if sha(unsigned) != UNSIGNED_SHA:
        raise ValueError('Unsigned source APK mismatch')
    encoded = (ROOT / 'docs/releases/alpha21-approved-apk-copy.b64').read_bytes()
    record = json.loads(zlib.decompress(base64.b64decode(encoded, validate=False)))
    if (record['schema'], record['unsigned_sha256'], record['approved_sha256'], record['approved_size']) != (1, UNSIGNED_SHA, APK_SHA, APK_SIZE):
        raise ValueError('Copy record identity mismatch')
    if len(record['ops']) > 2000:
        raise ValueError('Copy operation bound exceeded')
    output = bytearray()
    for operation in record['ops']:
        if set(operation) == {'copy'}:
            start, length = operation['copy']
            if not isinstance(start, int) or not isinstance(length, int) or start < 0 or length < 0 or start + length > len(unsigned):
                raise ValueError('Invalid source copy')
            output += unsigned[start:start + length]
        elif set(operation) == {'literal'}:
            output += base64.b64decode(operation['literal'], validate=True)
        else:
            raise ValueError('Invalid copy operation')
        if len(output) > APK_SIZE:
            raise ValueError('Output bound exceeded')
    if len(output) != APK_SIZE or sha(output) != APK_SHA:
        raise ValueError('Authorized APK identity mismatch')
    destination.write_bytes(output)
    # Validate every uncompressed ZIP entry against the pinned CI source as well.
    with zipfile.ZipFile(io.BytesIO(unsigned)) as a, zipfile.ZipFile(destination) as b:
        if not set(a.namelist()).issubset(set(b.namelist())) or b.testzip() is not None:
            raise ValueError('APK payload entry mismatch')
        for name in a.namelist():
            if a.read(name) != b.read(name):
                raise ValueError('APK payload mismatch')
    return len(output)

def validate_publication_revision():
    revision = git('rev-parse', 'HEAD').decode().strip()
    if git('rev-parse', SOURCE + '^{tree}').decode().strip() != TREE:
        raise ValueError('Application source tree mismatch')
    subprocess.run(['git', 'merge-base', '--is-ancestor', SOURCE, revision], cwd=ROOT, check=True)
    paths = git('diff', '--name-only', SOURCE, revision).decode().splitlines()
    for p in paths:
        if not (p == 'AGENTS.md' or p.startswith('docs/') or p.startswith('README') or p.startswith('tools/release/') or p == '.github/workflows/publish-approved-alpha21.yml'):
            raise ValueError(f'Application/build/test input changed after review: {p}')
    if git('status', '--porcelain', '--untracked-files=no').strip():
        raise ValueError('Tracked working tree is dirty')
    return revision

def source_archives(old_path, out, revision):
    verified_file(old_path, OLD_SHA)
    entries = {}
    # Only committed files; no workspace build output, keys, temp files or .git.
    with tarfile.open(fileobj=io.BytesIO(git('archive', '--format=tar', revision))) as archive:
        for entry in archive:
            if entry.isfile():
                name = safe_path(entry.name)
                entries[name] = (archive.extractfile(entry).read(), entry.mode)
            elif entry.issym():
                name = safe_path(entry.name)
                # Preserve Git's relative symlink text; never follow it on disk.
                if PurePosixPath(entry.linkname).is_absolute():
                    raise ValueError('Absolute source symlink')
                entries[name] = (entry.linkname.encode(), 0o120777)
            elif not entry.isdir():
                raise ValueError('Unexpected tracked source archive type')
    links = {}
    for row in git('ls-tree', '-r', revision).decode().splitlines():
        meta, path = row.split('\t', 1)
        mode, kind, oid = meta.split()
        if mode == '160000':
            links[path + '/'] = oid
    with zipfile.ZipFile(old_path) as old:
        manifest_name = next(n for n in old.namelist() if n.endswith('/SOURCE-MANIFEST.json'))
        prefix = manifest_name.removesuffix('SOURCE-MANIFEST.json')
        previous = json.loads(old.read(manifest_name))
        available = {t['path']: t for t in previous['trees']}
        missing = {t['path']: t for t in previous['missing_source_trees']}
        for path, oid in links.items():
            item = available.get(path, missing.get(path))
            if item is None or item['commit'] != oid:
                raise ValueError('Expanded tree pin mismatch: ' + path)
            if path in missing:
                continue
            count = 0
            for info in old.infolist():
                if info.filename.startswith(prefix + path) and not info.is_dir():
                    name = safe_path(info.filename.removeprefix(prefix))
                    if name in entries:
                        raise ValueError('Expanded source shadows tracked file')
                    entries[name] = (old.read(info), (info.external_attr >> 16) & 0o777 or 0o644)
                    count += 1
            if not count:
                raise ValueError('Expanded source tree is empty')
    # Expand the exact Xray source used by the accepted CI, independently hash-pinned.
    xray_pin = '63ca2e0c1d31b89f67700c33842431f541fedd25'
    xray_sha = 'f4a41aff30f63c00e79d2ab23364a1d6122185b62d93fe283d4d28c548888334'
    xray = old_path.parent / 'alpha21-xray.tar.gz'
    if not xray.is_file():
        import urllib.request
        with urllib.request.urlopen('https://codeload.github.com/XTLS/Xray-core/tar.gz/' + xray_pin, timeout=120) as response, xray.open('wb') as output:
            shutil.copyfileobj(response, output)
    verified_file(xray, xray_sha)
    count = 0
    with tarfile.open(xray) as archive:
        for member in archive:
            if member.isdir():
                continue
            if not member.isfile() or not member.name.startswith('Xray-core-' + xray_pin + '/'):
                raise ValueError('Unexpected Xray source entry')
            name = safe_path('external-source/Xray-core/' + member.name.split('/', 1)[1])
            if name in entries:
                raise ValueError('Xray source shadows tracked file')
            entries[name] = (archive.extractfile(member).read(), member.mode)
            count += 1
    if count != 1190:
        raise ValueError('Unexpected Xray source file count')
    previous['additional_xray_source'] = {'commit': xray_pin, 'archive_sha256': xray_sha, 'files': count, 'directory': 'external-source/Xray-core/'}
    # Include the exact GPL-3.0 auxiliary-engine source, never a mutable branch.
    mihomo_pin = '3f27ef39f8c96bbfbc8b0b9bb10f5c9ef60149d8'
    mihomo_sha = 'ecda5f8c8a8a1d60bc088c2640c85d92c8f002c0686234a26bdf671735afe216'
    mihomo = old_path.parent / 'alpha18-mihomo.tar.gz'
    if not mihomo.is_file():
        import urllib.request
        with urllib.request.urlopen('https://codeload.github.com/MetaCubeX/mihomo/tar.gz/' + mihomo_pin, timeout=45) as response, mihomo.open('wb') as output:
            shutil.copyfileobj(response, output)
    verified_file(mihomo, mihomo_sha)
    mihomo_count = 0
    with tarfile.open(mihomo) as archive:
        for member in archive:
            if member.isdir():
                continue
            if not member.isfile() or not member.name.startswith('mihomo-' + mihomo_pin + '/'):
                raise ValueError('Unexpected Mihomo source entry')
            name = safe_path('external-source/Mihomo/' + member.name.split('/', 1)[1])
            if name in entries:
                raise ValueError('Mihomo source shadows tracked file')
            entries[name] = (archive.extractfile(member).read(), member.mode)
            mihomo_count += 1
    if mihomo_count != 1045:
        raise ValueError('Unexpected Mihomo source file count')
    previous['additional_mihomo_source'] = {'commit': mihomo_pin, 'archive_sha256': mihomo_sha, 'files': mihomo_count, 'directory': 'external-source/Mihomo/', 'adapter': 'tools/native/awg/', 'applied_patch': 'tools/native/mihomo-socks-domain.patch', 'patch_sha256': sha((ROOT / 'tools/native/mihomo-socks-domain.patch').read_bytes()), 'upstream_patch_commit': 'c65570ed0924a7e48a48160463c68015b9a84ca8', 'dependencies': 'pinned upstream go.mod/go.sum; fetched by Go during builds'}
    for name in entries:
        lower = name.lower()
        if any(part in {'.git', '.gradle', '__pycache__'} for part in PurePosixPath(name).parts) or name.startswith(('app/build/', 'build/')) or lower.endswith(('.p12', '.pfx', '.jks', '.keystore')) or 'recovery-private' in lower or lower.endswith('keystore.properties') or lower.endswith('local.properties'):
            raise ValueError('Private/generated material in source archive: ' + name)
    manifest = {
        'schema': 2, 'application_version': '0.3.0-alpha21', 'version_code': 46,
        'application_id': 'app.veyra.client', 'application_build_commit': SOURCE,
        'application_tree': TREE, 'source_archive_commit': revision,
        'source_archive_tree': git('rev-parse', revision + '^{tree}').decode().strip(),
        'publication_documentation_commit': revision,
        'apk': {'file': APK_NAME, 'sha256': APK_SHA, 'signer_sha256': CERT_SHA, 'signature_schemes': ['v2', 'v3']},
        'native_rebuild': {'entire_engine_set': False, 'rebuilt': ['pinned Xray', 'project launcher', 'Hev/JNI', 'Slipstream/JNI with certificate patch', 'pinned constrained AWG/AnyTLS/TUIC/Mieru adapter with SOCKS domain-length patch'], 'reused': 'remaining transports from checksum-pinned SlipNet 2.5.6'},
        'trees': previous['trees'], 'missing_source_trees': previous['missing_source_trees'],
        'additional_xray_source': previous['additional_xray_source'],
        'additional_mihomo_source': previous['additional_mihomo_source'],
        'expanded_tree_source': {'archive': 'Rovunet-0.3.0-alpha15.4-source.zip', 'sha256': OLD_SHA, 'method': 'Only expanded upstream source entries, with every root gitlink compared to current pins'},
        'external_build_inputs': previous['external_build_inputs'], 'signing_keys_included': False,
        'owner_authorized_implementation_and_release': True, 'owner_accepted_exact_apk': False, 'new_detailed_physical_measurements': False, 'owner_approval_date': '2026-11-13', 'channel': 'prerelease-not-latest', 'actual_apk_checkout': CHECKOUT, 'actual_room_checkout': SOURCE,
        'ci_application_run': f'https://github.com/{REPO}/actions/runs/{RUN}',
        'publication_commit_ci': 'Publication-only inputs; application evidence belongs to the recorded passing source',
        'light_source_excludes_extensions': ['.so', '.aar'],
    }
    json_write(out / 'SOURCE-MANIFEST.json', manifest)
    entries['SOURCE-MANIFEST.json'] = ((out / 'SOURCE-MANIFEST.json').read_bytes(), 0o644)
    for lite in (False, True):
        stem = 'Rovunet-0.3.0-alpha21-source' + ('-light' if lite else '')
        with zipfile.ZipFile(out / (stem + '.zip'), 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as target:
            for name, (data, mode) in sorted(entries.items()):
                if lite and Path(name).suffix in ('.so', '.aar'):
                    continue
                info = zipfile.ZipInfo(stem + '/' + name, DATE)
                info.external_attr = ((mode if mode & 0o170000 else 0o100000 | mode)) << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                target.writestr(info, data)
    return manifest

def verify_signature(apk, out, signer_jar):
    # Verify both Android API ranges explicitly. apksigner versions can change
    # their human-readable signer labels; the certificate DER is authoritative.
    verified_file(signer_jar, '3716d9311e55d2b0918a2fd9d54ba9e406c5f6abeea700b287f11259bc163dec')
    logs = []
    for lower, upper in ((24, 27), (28, 36)):
        command = ['java', '-jar', str(signer_jar), 'verify', '--min-sdk-version', str(lower), '--max-sdk-version', str(upper),
                   '--verbose', '--print-certs', '--print-certs-pem', str(apk)]
        logs.append(subprocess.check_output(command, stderr=subprocess.STDOUT).decode())
    log = '\n'.join(logs)
    print(log, flush=True)  # Public certificate/signature diagnostics; no key input.
    # Also require the actual v2 and v3 signing-block records in the exact APK.
    data = apk.read_bytes()
    eocd = data.rfind(b'PK\x05\x06')
    central = struct.unpack_from('<I', data, eocd + 16)[0]
    if data[central - 16:central] != b'APK Sig Block 42':
        raise ValueError('APK signing block missing')
    size = struct.unpack_from('<Q', data, central - 24)[0]
    start = central - size - 8
    if start < 0 or struct.unpack_from('<Q', data, start)[0] != size:
        raise ValueError('Invalid signing block boundary')
    ids = set()
    cursor = start + 8
    while cursor < central - 24:
        length = struct.unpack_from('<Q', data, cursor)[0]
        if length < 4 or cursor + 8 + length > central - 24:
            raise ValueError('Invalid signing record')
        ids.add(struct.unpack_from('<I', data, cursor + 8)[0])
        cursor += 8 + length
    if cursor != central - 24 or not {0x7109871a, 0xf05368c0}.issubset(ids):
        raise ValueError('Required v2/v3 signatures missing')
    match = re.search(r'-----BEGIN CERTIFICATE-----.*?-----END CERTIFICATE-----', log, re.S)
    if not match:
        raise ValueError('Public certificate missing')
    pem = match.group() + '\n'
    der = base64.b64decode(''.join(pem.splitlines()[1:-1]))
    if sha(der) != CERT_SHA:
        raise ValueError('Certificate DER mismatch')
    (out / 'Rovunet-production-certificate.pem').write_text(pem)
    (out / 'APK-SIGNATURE-VERIFICATION.txt').write_text(log)
    subprocess.run([sys.executable, str(ROOT / 'tools/quality/check-apk-native.py'), str(apk)], check=True)
    (out / 'SIGNER-SHA256.txt').write_text(CERT_SHA + '\n')

def room_evidence(room_path, out):
    verified_file(room_path, ROOM_ZIP_SHA)
    with zipfile.ZipFile(room_path) as z:
        manifest_name = 'status-room-kit/KIT-MANIFEST.json'
        manifest = json.loads(z.read(manifest_name))
        results = [n for n in z.namelist() if n.endswith('/RESULT.json')]
        if len(results) != 1: raise ValueError('Expected exact Room result')
        result_name = results[0]
        raw_result = z.read(result_name)
        result = json.loads(raw_result)
        prefix = result_name.rsplit('/', 1)[0] + '/'
        required = json.loads((ROOT / 'tools/veyra/status-room-methods.json').read_text())
        if set(required) != set(manifest['methods']) or set(required) != set(result['required_methods']):
            raise ValueError('Room method set mismatch')
        if result['status'] != 'passed' or result['reported_success_count'] != '7' or result['observed_start_records'] != 7:
            raise ValueError('Room run did not pass seven exact methods')
        if sha(z.read(manifest_name)) != result['kit_manifest_sha256']: raise ValueError('Kit manifest digest mismatch')
        for name, digest in manifest['files'].items():
            if sha(z.read('status-room-kit/' + name)) != digest: raise ValueError('Room APK pair digest mismatch')
        for name, digest in result['log_sha256'].items():
            if sha(z.read(prefix + name)) != digest: raise ValueError('Room raw log digest mismatch')
        import runpy, sys
        sys.path.insert(0, str(ROOT / 'tools/veyra'))
        gate = runpy.run_path(str(ROOT / 'tools/veyra/run-device-gate.py'))
        methods = {tuple(m.split('#', 1)) for m in required}
        if gate['verified_tests'](z.read(prefix+'instrumentation.log').decode(), required=methods) != methods:
            raise ValueError('Raw per-method instrumentation results differ')
        names = [manifest_name, result_name] + [prefix+n for n in sorted(result['log_sha256'])] + ['status-emulator.log']
        with zipfile.ZipFile(out / 'Rovunet-alpha21-Room-evidence.zip', 'w', zipfile.ZIP_DEFLATED) as target:
            for name in names: target.writestr(name, z.read(name))
    return {'run': ROOM_RUN, 'artifact': ROOM_ARTIFACT, 'zip_sha256': ROOM_ZIP_SHA,
            'result_json_sha256': sha(raw_result),
            'instrumentation_sha256': result['log_sha256']['instrumentation.log'], 'methods': result['method_results'],
            'actual_checkout': SOURCE, 'metadata_commit': manifest['source']['commit'],
            'metadata_correction': 'GITHUB_SHA records PR merge ref; explicit checkout and git log prove PR head. Raw evidence retained unchanged.',
            'scope': 'Accelerated API30 x86_64 isolated Debug Room; code43 native fixture. No production upgrade/Keystore acceptance.'}


def prepare(ci_path, room_path, old_path, out, log_dir, online):
    revision = validate_publication_revision()
    out.mkdir(parents=True, exist_ok=True)
    if any(out.iterdir()): raise ValueError('Use an empty output directory')
    restore_apk(ci_path, out / APK_NAME)
    with tempfile.TemporaryDirectory(prefix='rovunet-alpha21-verify-') as tmp:
        with zipfile.ZipFile(ci_path) as z:
            names = [n for n in z.namelist() if n.endswith('rovunet-apksigner.jar')]
            if len(names) != 1: raise ValueError('Expected exact SDK verifier')
            signer = Path(tmp) / 'apksigner.jar'; signer.write_bytes(z.read(names[0]))
        verify_signature(out / APK_NAME, out, signer)
    manifest = source_archives(old_path, out, revision)
    room = room_evidence(room_path, out)
    logs = {}
    log_dir.mkdir(parents=True, exist_ok=True)
    for name, job in [('verify-job.log', 132157255021), ('device-job.log', 132157255455), ('room-job.log', 132157262498)]:
        path = log_dir / name
        if not path.is_file() and online: api(f'actions/jobs/{job}/logs', binary_path=path)
        if not path.is_file(): raise ValueError('Missing source checkout/JVM log: '+name)
        logs[name] = path.read_bytes()
    if b'OK (174 tests)' not in logs['verify-job.log']: raise ValueError('Missing 174-test JVM result')
    if SOURCE.encode() not in logs['room-job.log'] or CHECKOUT.encode() not in logs['device-job.log']:
        raise ValueError('CI actual checkout proof absent')
    if online:
        for run_id in (RUN, ROOM_RUN):
            run = api(f'actions/runs/{run_id}')
            jobs = api(f'actions/runs/{run_id}/jobs?per_page=30')['jobs']
            if run['head_sha'] != SOURCE or run['conclusion'] != 'success': raise ValueError('Pinned CI no longer successful')
            statuses = {j['name']:j['conclusion'] for j in jobs}
            if run_id == RUN and (statuses.get('verify') != 'success' or statuses.get('android-device / vpn-device') != 'success'):
                raise ValueError('Required APK/JVM jobs not green')
            if run_id == ROOM_RUN and statuses.get('status-room') != 'success': raise ValueError('Room job not green')
    with zipfile.ZipFile(out / 'Rovunet-alpha21-CI-logs.zip', 'w', zipfile.ZIP_DEFLATED) as target:
        for name,data in logs.items(): target.writestr(name,data)
    evidence = {'application_source': SOURCE, 'tree': TREE, 'apk_jvm_actual_checkout': CHECKOUT,
                'checkout_tree_equals_head_tree': True, 'source_pr': 22, 'apk_run': RUN, 'apk_artifact': ARTIFACT,
                'artifact_zip_sha256': CI_ZIP_SHA, 'jvm': {'count':174,'job':132157255021}, 'room': room,
                'job_log_sha256': {n:sha(b) for n,b in logs.items()},
                'independent_test_counts_not_summed': True, 'production_upgrade_keystore_tested': False,
                'build_job_skipped': True, 'apk_built_by': 'android-device / vpn-device; offline production signing'}
    json_write(out / 'Rovunet-alpha21-CI-evidence.json', evidence)
    record = json.loads((ROOT / 'docs/releases/alpha21-provenance.json').read_text())
    record.update(publication_commit=revision, publication_tree=manifest['source_archive_tree'],
                  publication='Owner-authorized alpha21 prerelease; not Latest', approved_apk_rebuilt=False,
                  public_apk_restored_without_signing_key=True, merged=online)
    with zipfile.ZipFile(ci_path) as z:
        names = [n for n in z.namelist() if n.endswith('/outputs/mapping/fullRelease/mapping.txt')]
        if len(names) != 1: raise ValueError('Expected exact R8 mapping')
        with zipfile.ZipFile(out / 'Rovunet-alpha21-public-r8-mapping.zip', 'w', zipfile.ZIP_DEFLATED) as target:
            info=zipfile.ZipInfo('mapping.txt', DATE); info.compress_type=zipfile.ZIP_DEFLATED
            target.writestr(info,z.read(names[0]))
    record['r8_mapping_sha256'] = sha((out / 'Rovunet-alpha21-public-r8-mapping.zip').read_bytes())
    json_write(out / 'VERIFICATION.json', record)
    shutil.copyfile(ROOT / 'docs/releases/alpha21-native-inventory.json', out / 'NATIVE-INVENTORY.json')
    shutil.copyfile(ROOT / 'docs/releases/alpha21-guide-fa.txt', out / 'Rovunet-0.3.0-alpha21-Guide-FA.txt')
    (out / 'APK-SHA256.txt').write_text(f'{APK_SHA}  {APK_NAME}\n')
    (out / 'SHA256SUMS.txt').write_text('\n'.join(f'{sha((out/name).read_bytes())}  {name}' for name in sorted(ASSET_NAMES) if name!='SHA256SUMS.txt')+'\n')
    if {p.name for p in out.iterdir()} != set(ASSET_NAMES): raise ValueError('Asset allowlist mismatch')
    print('Prepared',len(ASSET_NAMES),'allowlisted assets; APK',APK_SHA,flush=True)


def ensure_merged(revision):
    pr = api('pulls/22')
    if pr['head']['sha'] != revision or pr['base']['ref'] != 'main' or not pr['merged'] or not pr['merge_commit_sha']:
        raise ValueError('Exact publication PR22 has not merged')
    return pr['merge_commit_sha']


def publish(out, revision):
    merged = ensure_merged(revision)
    verified_file(out / APK_NAME, APK_SHA)
    expected = {line.split('  ',1)[1]:line.split('  ',1)[0] for line in (out/'SHA256SUMS.txt').read_text().splitlines()}
    expected['SHA256SUMS.txt']=sha((out/'SHA256SUMS.txt').read_bytes())
    if set(expected)!=set(ASSET_NAMES): raise ValueError('Unexpected asset set')
    for name,digest in expected.items(): verified_file(out/name,digest)
    tags=api('git/matching-refs/tags/'+TAG)
    for tag in tags:
        if tag['ref']=='refs/tags/'+TAG and (tag['object']['type']!='commit' or tag['object']['sha']!=revision):
            raise ValueError('Existing tag points elsewhere')
    releases=api('releases?per_page=100')
    matches=[r for r in releases if r['tag_name']==TAG]
    latest_before=api('releases/latest')['id']
    if matches:
        release=matches[0]
        if not release['draft'] or release['target_commitish']!=revision: raise ValueError('Refuse existing published/different release')
    else:
        release=api('releases','POST',{'tag_name':TAG,'target_commitish':revision,
            'name':'Rovunet 0.3.0-alpha21 — Account trust and independent caches',
            'body':(ROOT/'docs/releases/alpha21.md').read_text(),'draft':True,'prerelease':True,'make_latest':'false'})
    existing={a['name']:a for a in api(f"releases/{release['id']}/assets?per_page=100")}
    if set(existing)-set(expected): raise ValueError('Unexpected existing draft asset')
    for name,digest in expected.items():
        if name in existing:
            if existing[name]['size']!=(out/name).stat().st_size or existing[name].get('digest')!='sha256:'+digest:
                raise ValueError('Refuse to replace different draft asset')
        else: subprocess.run(['gh','release','upload',TAG,str(out/name),'--repo',REPO],check=True)
    assets=api(f"releases/{release['id']}/assets?per_page=100")
    if len(assets)!=len(expected) or {a['name'] for a in assets}!=set(expected): raise ValueError('Incomplete draft assets')
    for a in assets:
        if a['size']!=(out/a['name']).stat().st_size or a.get('digest')!='sha256:'+expected[a['name']]:
            raise ValueError('Server asset digest mismatch')
    final=api(f"releases/{release['id']}",'PATCH',{'draft':False,'prerelease':True,'make_latest':'false'})
    latest_after=api('releases/latest')['id']
    if final['draft'] or not final['prerelease'] or latest_after==final['id']: raise ValueError('Prerelease channel verification failed')
    apk_asset=next(a for a in assets if a['name']==APK_NAME)
    import urllib.request, time
    with tempfile.TemporaryDirectory(prefix='alpha21-release-readback-') as tmp:
        downloaded=Path(tmp)/APK_NAME
        for attempt in range(5):
            try:
                with urllib.request.urlopen(apk_asset['browser_download_url'],timeout=60) as response, downloaded.open('wb') as f:
                    shutil.copyfileobj(response,f)
                break
            except Exception:
                if attempt==4: raise
                time.sleep(3)
        verified_file(downloaded,APK_SHA)
    receipt={'release_url':final['html_url'],'apk_url':apk_asset['browser_download_url'],'sha256':APK_SHA,
        'release_id':final['id'],'asset_id':apk_asset['id'],'prerelease':True,'latest_before':latest_before,
        'latest_after':latest_after,'application_source':SOURCE,'application_tree':TREE,
        'publication_commit':revision,'merge_commit':merged,'release_redownload_verified':True,
        'server_digests':{a['name']:a['digest'] for a in assets}}
    json_write(out.parent/'PUBLICATION-RECEIPT.json',receipt)
    print('Published and redownloaded:',final['html_url'],APK_SHA,flush=True)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ci-zip',type=Path,required=True)
    parser.add_argument('--room-zip',type=Path,required=True)
    parser.add_argument('--previous-source',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--ci-log-dir',type=Path,required=True)
    parser.add_argument('--publish',action='store_true')
    args=parser.parse_args()
    revision=validate_publication_revision()
    if args.publish:
        if os.environ.get('GITHUB_REPOSITORY')!=REPO or re.fullmatch(r'refs/heads/publish/alpha21-approved(?:-retry[0-9]+)?',os.environ.get('GITHUB_REF','')) is None or os.environ.get('GITHUB_SHA')!=revision:
            raise ValueError('Not the one-version owner-authorized trigger')
        ensure_merged(revision)
        for path,endpoint in [(args.ci_zip,f'actions/artifacts/{ARTIFACT}/zip'),(args.room_zip,f'actions/artifacts/{ROOM_ARTIFACT}/zip'),(args.previous_source,f'releases/assets/{OLD_ASSET}')]:
            if not path.is_file(): api(endpoint,binary_path=path)
    prepare(args.ci_zip,args.room_zip,args.previous_source,args.output,args.ci_log_dir,args.publish)
    if args.publish: publish(args.output,revision)


if __name__=='__main__': main()
