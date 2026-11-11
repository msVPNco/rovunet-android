#!/usr/bin/env python3
"""Package and publish the exact owner-accepted alpha17 bytes; never build or sign.

The small copy record contains public ZIP headers/padding/signatures only. Compressed
entry payloads come from a hash-pinned passing CI artifact. No private key is used.
This one-version procedure is authorized by the owner's 2026-11-09 acceptance.
"""
import argparse
import base64
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import struct
import subprocess
import tarfile
import zipfile
import zlib

REPO = 'msVPNco/rovunet-app'
TAG = 'v0.3.0-alpha17'
SOURCE = 'bbb30d7146c3f308f37943a0e3c9456b185167f0'
TREE = 'a76878617ff85453cb372494c389eef719d41a02'
RUN = 42626133278
ARTIFACT = 13679672141
CI_ZIP_SHA = '3d85a69da434cdbf04102402fb7d69b51c68fabc147082deca93b3b31dacb7db'
UNSIGNED_SHA = '4b276f6a3f1cb441e0ee031da6e25afbc3a7f44358a932d7e10c20c7c64c3725'
APK_SHA = 'eb5ac7bd474b37e361e8ca126c8e2f7d445faaf876ddd7b9ecd158548324a871'
APK_SIZE = 97389464
CERT_SHA = '1e40bd201489a8209043e346255772823d9d52ee21b468bdef2c152d239bc416'
APK_NAME = 'Rovunet-v0.3.0-alpha17-Full.apk'
ASSET_NAMES = (
    APK_NAME, 'APK-SHA256.txt', 'APK-SIGNATURE-VERIFICATION.txt',
    'Rovunet-production-certificate.pem', 'SIGNER-SHA256.txt',
    'Rovunet-0.3.0-alpha17-source.zip', 'Rovunet-0.3.0-alpha17-source-light.zip',
    'Rovunet-0.3.0-alpha17-Guide-FA.txt', 'Rovunet-alpha17-public-r8-mapping.zip',
    'Rovunet-alpha17-CI-evidence.json', 'SOURCE-MANIFEST.json',
    'VERIFICATION.json', 'SHA256SUMS.txt',
)
OLD_ASSET = 721633409
OLD_SHA = 'fd126cc5b293b2f12ce60a7045f9b6158f9c21e645b0dee7c316ba70d0363b27'
ROOT = Path(__file__).resolve().parents[2]
DATE = (2026, 11, 9, 0, 0, 0)


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
        choices = [n for n in archive.namelist() if n.endswith('/Rovunet-v0.3.0-alpha17-full-release-universal-unsigned.apk')]
        if len(choices) != 1:
            raise ValueError('Expected exactly one unsigned source APK')
        unsigned = archive.read(choices[0])
    if sha(unsigned) != UNSIGNED_SHA:
        raise ValueError('Unsigned source APK mismatch')
    encoded = (ROOT / 'docs/releases/alpha17-approved-apk-copy.b64').read_bytes()
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
        raise ValueError('Owner-accepted APK identity mismatch')
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
        if not (p == 'AGENTS.md' or p.startswith('docs/') or p.startswith('README') or p.startswith('tools/release/') or p == '.github/workflows/publish-approved-alpha17.yml'):
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
    xray = old_path.parent / 'alpha17-xray.tar.gz'
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
    for name in entries:
        lower = name.lower()
        if any(part in {'.git', '.gradle', '__pycache__'} for part in PurePosixPath(name).parts) or name.startswith(('app/build/', 'build/')) or lower.endswith(('.p12', '.pfx', '.jks', '.keystore')) or 'recovery-private' in lower or lower.endswith('keystore.properties') or lower.endswith('local.properties'):
            raise ValueError('Private/generated material in source archive: ' + name)
    manifest = {
        'schema': 2, 'application_version': '0.3.0-alpha17', 'version_code': 36,
        'application_id': 'app.veyra.client', 'application_build_commit': SOURCE,
        'application_tree': TREE, 'source_archive_commit': revision,
        'source_archive_tree': git('rev-parse', revision + '^{tree}').decode().strip(),
        'publication_documentation_commit': revision,
        'apk': {'file': APK_NAME, 'sha256': APK_SHA, 'signer_sha256': CERT_SHA, 'signature_schemes': ['v2', 'v3']},
        'native_rebuild': {'entire_engine_set': False, 'rebuilt': ['pinned Xray', 'project launcher', 'Hev/JNI'], 'reused': 'remaining transports from checksum-pinned SlipNet 2.5.6'},
        'trees': previous['trees'], 'missing_source_trees': previous['missing_source_trees'],
        'additional_xray_source': previous['additional_xray_source'],
        'expanded_tree_source': {'archive': 'Rovunet-0.3.0-alpha15.4-source.zip', 'sha256': OLD_SHA, 'method': 'Only expanded upstream source entries, with every root gitlink compared to current pins'},
        'external_build_inputs': previous['external_build_inputs'], 'signing_keys_included': False,
        'owner_approved_exact_apk': True, 'owner_approval_date': '2026-11-09',
        'ci_application_run': f'https://github.com/{REPO}/actions/runs/{RUN}',
        'publication_commit_ci': 'Publication-only inputs; application evidence belongs to the recorded passing source',
        'light_source_excludes_extensions': ['.so', '.aar'],
    }
    json_write(out / 'SOURCE-MANIFEST.json', manifest)
    entries['SOURCE-MANIFEST.json'] = ((out / 'SOURCE-MANIFEST.json').read_bytes(), 0o644)
    for lite in (False, True):
        stem = 'Rovunet-0.3.0-alpha17-source' + ('-light' if lite else '')
        with zipfile.ZipFile(out / (stem + '.zip'), 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as target:
            for name, (data, mode) in sorted(entries.items()):
                if lite and Path(name).suffix in ('.so', '.aar'):
                    continue
                info = zipfile.ZipInfo(stem + '/' + name, DATE)
                info.external_attr = ((mode if mode & 0o170000 else 0o100000 | mode)) << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                target.writestr(info, data)
    return manifest


def verify_signature(apk, out):
    apksigner = os.environ.get('APKSIGNER') or shutil.which('apksigner')
    if not apksigner:
        sdk = Path(os.environ.get('ANDROID_HOME', os.environ.get('ANDROID_SDK_ROOT', '/usr/local/lib/android/sdk')))
        candidates = list((sdk / 'build-tools').glob('*/apksigner'))
        if candidates:
            pinned = sdk / 'build-tools/36.0.0/apksigner'
            apksigner = str(pinned if pinned.is_file() else sorted(candidates)[-1])
    if not apksigner:
        raise RuntimeError('apksigner is required for final publication verification')
    # Verify both Android API ranges explicitly. apksigner versions can change
    # their human-readable signer labels; the certificate DER is authoritative.
    logs = []
    for lower, upper in ((24, 27), (28, 35)):
        command = [apksigner, 'verify', '--min-sdk-version', str(lower), '--max-sdk-version', str(upper),
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
    subprocess.run(['python3', str(ROOT / 'tools/quality/check-apk-native.py'), str(apk)], check=True)
    (out / 'SIGNER-SHA256.txt').write_text(CERT_SHA + '\n')


def prepare(ci_path, old_path, out, online):
    revision = validate_publication_revision()
    out.mkdir(parents=True, exist_ok=True)
    if any(out.iterdir()):
        raise ValueError('Use an empty output directory')
    restore_apk(ci_path, out / APK_NAME)
    verify_signature(out / APK_NAME, out)
    manifest = source_archives(old_path, out, revision)
    record = json.loads((ROOT / 'docs/releases/alpha17-provenance.json').read_text())
    record.update(publication_commit=revision, publication_tree=manifest['source_archive_tree'], publication='Published exact owner-accepted alpha17', approved_apk_rebuilt=False, public_apk_restored_without_signing_key=True)
    with zipfile.ZipFile(ci_path) as source:
        names = [n for n in source.namelist() if n.endswith('/outputs/mapping/fullRelease/mapping.txt')]
        if len(names) != 1:
            raise ValueError('Expected exact CI R8 mapping')
        with zipfile.ZipFile(out / 'Rovunet-alpha17-public-r8-mapping.zip', 'w', zipfile.ZIP_DEFLATED) as target:
            info = zipfile.ZipInfo('mapping.txt', DATE)
            info.compress_type = zipfile.ZIP_DEFLATED
            target.writestr(info, source.read(names[0]))
    record.update(r8_mapping_file='Rovunet-alpha17-public-r8-mapping.zip', r8_mapping_sha256=sha((out / 'Rovunet-alpha17-public-r8-mapping.zip').read_bytes()), merged=True)
    json_write(out / 'VERIFICATION.json', record)
    guide = (ROOT / 'docs/USER_GUIDE.md').read_text().split('\n## فارسی\n', 1)[1]
    (out / 'Rovunet-0.3.0-alpha17-Guide-FA.txt').write_text('Rovunet 0.3.0-alpha17 Full · کد ۳۶\n\n' + guide)
    if online:
        run = api(f'actions/runs/{RUN}')
        jobs = api(f'actions/runs/{RUN}/jobs?per_page=20')
        if run['head_sha'] != SOURCE or run['conclusion'] != 'success':
            raise ValueError('Recorded application CI is not successful')
        required = {j['name']: j['conclusion'] for j in jobs['jobs']}
        if required.get('verify') != 'success' or not any('vpn-device' in n and c == 'success' for n, c in required.items()):
            raise ValueError('Required application CI jobs are not green')
        evidence = {'application_source': SOURCE, 'run': RUN, 'conclusion': run['conclusion'], 'jobs': required, 'additional_pr_run': record.get('additional_pr_ci', {'run': 42626133278})}
    else:
        evidence = {'application_source': SOURCE, 'run': RUN, 'source_evidence': record, 'local_packaging_validation_only': True}
    json_write(out / 'Rovunet-alpha17-CI-evidence.json', evidence)
    (out / 'APK-SHA256.txt').write_text(f'{APK_SHA}  {APK_NAME}\n')
    lines = [f'{sha((out / name).read_bytes())}  {name}'
             for name in sorted(ASSET_NAMES) if name != 'SHA256SUMS.txt']
    (out / 'SHA256SUMS.txt').write_text('\n'.join(lines) + '\n')
    print('Prepared', len(ASSET_NAMES), 'allowlisted assets; approved APK SHA-256', APK_SHA, flush=True)


def publish(out, revision):
    verified_file(out / APK_NAME, APK_SHA)
    expected = {line.split('  ', 1)[1]: line.split('  ', 1)[0] for line in (out / 'SHA256SUMS.txt').read_text().splitlines()}
    expected['SHA256SUMS.txt'] = sha((out / 'SHA256SUMS.txt').read_bytes())
    if set(expected) != set(ASSET_NAMES):
        raise ValueError('Unexpected publication file')
    for name, digest in expected.items():
        verified_file(out / name, digest)
    # Refuse to overwrite/relabel a public release or an unrelated existing tag.
    releases = api('releases?per_page=100')
    matches = [r for r in releases if r['tag_name'] == TAG]
    if matches:
        release = matches[0]
        if not release['draft'] or release['target_commitish'] != revision:
            raise ValueError('Existing public or different release must not be overwritten')
    else:
        release = api('releases', 'POST', {'tag_name': TAG, 'target_commitish': revision, 'name': 'Rovunet 0.3.0-alpha17 — reviewed public subscriptions and broader imports', 'body': (ROOT / 'docs/releases/alpha17.md').read_text(), 'draft': True, 'prerelease': False, 'make_latest': 'false'})
    before = api(f"releases/{release['id']}/assets?per_page=100")
    if any(a['name'] not in expected for a in before):
        raise ValueError('Unexpected pre-existing draft asset')
    existing = {a['name']: a for a in before}
    for name, digest in expected.items():
        if name in existing:
            if existing[name]['size'] != (out / name).stat().st_size or existing[name].get('digest') != 'sha256:' + digest:
                raise ValueError('Existing draft asset differs; refusing replacement')
        else:
            subprocess.run(['gh', 'release', 'upload', TAG, str(out / name), '--repo', REPO], check=True)
    assets = api(f"releases/{release['id']}/assets?per_page=100")
    if len(assets) != len(expected) or {a['name'] for a in assets} != set(expected):
        raise ValueError('Draft assets incomplete')
    for asset in assets:
        if asset['size'] != (out / asset['name']).stat().st_size or asset.get('digest') != 'sha256:' + expected[asset['name']]:
            raise ValueError('Server asset digest mismatch')
    final = api(f"releases/{release['id']}", 'PATCH', {'draft': False, 'prerelease': False, 'make_latest': 'true'})
    latest = api('releases/latest')
    if final['draft'] or final['prerelease'] or latest['id'] != final['id']:
        raise ValueError('Latest channel verification failed')
    print('Published exact owner-accepted alpha17:', final['html_url'], flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ci-zip', type=Path, required=True)
    parser.add_argument('--previous-source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--publish', action='store_true')
    options = parser.parse_args()
    revision = validate_publication_revision()
    if options.publish:
        if os.environ.get('GITHUB_REPOSITORY') != REPO or re.fullmatch(r'refs/heads/publish/alpha17-approved(?:-retry[0-9]+)?', os.environ.get('GITHUB_REF', '')) is None or os.environ.get('GITHUB_SHA') != revision:
            raise ValueError('Not the explicitly approved alpha17 publication trigger')
        if not options.ci_zip.is_file():
            api(f'actions/artifacts/{ARTIFACT}/zip', binary_path=options.ci_zip)
        if not options.previous_source.is_file():
            api(f'releases/assets/{OLD_ASSET}', binary_path=options.previous_source)
    prepare(options.ci_zip, options.previous_source, options.output, online=options.publish)
    if options.publish:
        publish(options.output, revision)


if __name__ == '__main__':
    main()
