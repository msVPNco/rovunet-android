"""Run only the supplied isolated candidate on an explicitly selected authorized device."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import subprocess
import time

parser = argparse.ArgumentParser()
parser.add_argument('--serial', required=True, help='Explicit authorized isolated Android target')
parser.add_argument('--adb', default='adb')
parser.add_argument('--kit', type=Path, required=True)
args = parser.parse_args()
root = args.kit.resolve()
manifest = json.loads((root / 'KIT-MANIFEST.json').read_text())
assert manifest['target_package'] == 'app.veyra.client.candidate'
assert manifest['test_package'] == 'app.veyra.client.candidate.test'
for name, expected in manifest['files'].items():
    path = root / name
    assert path.is_file() and hashlib.sha256(path.read_bytes()).hexdigest() == expected, name
out = root / ('device-result-' + time.strftime('%Y%m%d-%H%M%S'))
out.mkdir()
result = {'target_serial_sha256': hashlib.sha256(args.serial.encode()).hexdigest(),
    'source': manifest['source'], 'kit_manifest_sha256': hashlib.sha256((root / 'KIT-MANIFEST.json').read_bytes()).hexdigest(),
    'required_methods': manifest['methods'], 'phases': [], 'instrumentation_started': False}

def run(label, command, timeout):
    call = [args.adb, '-s', args.serial, *command]
    try:
        process = subprocess.run(call, capture_output=True, text=True, timeout=timeout)
        code, stdout, text = process.returncode, process.stdout, process.stdout + process.stderr
    except subprocess.TimeoutExpired:
        code, stdout, text = 124, '', 'Device command timed out; stop and inspect target health.'
    text = text.replace(args.serial, '<selected-device>')
    (out / (label + '.log')).write_text(text + '\nexit=' + str(code) + '\n')
    result['phases'].append({'label': label, 'exit': code})
    if code:
        raise RuntimeError(label + ' failed; no automatic reinstall, reset, reboot or retry')
    return stdout.strip()

try:
    assert run('health-state', ['get-state'], 30) == 'device'
    assert run('health-boot', ['shell', 'getprop', 'sys.boot_completed'], 30) == '1'
    assert 'package:' in run('health-package-service', ['shell', 'cmd', 'package', 'path', 'android'], 30)
    run('sdk', ['shell', 'getprop', 'ro.build.version.sdk'], 30)
    run('abi', ['shell', 'getprop', 'ro.product.cpu.abi'], 30)
    assert 'Success' in run('install-app', ['install', '-t', '-r', str(root / manifest['app'])], 300)
    assert 'Success' in run('install-tests', ['install', '-t', '-r', str(root / manifest['tests'])], 300)
    assert 'package:' in run('installed-candidate', ['shell', 'cmd', 'package', 'path', manifest['target_package']], 30)
    result['instrumentation_started'] = True
    text = run('instrumentation', ['shell', 'am', 'instrument', '-w', '-r', '-e', 'class', ','.join(manifest['methods']), manifest['runner']], 600)
    result['observed_start_records'] = len(re.findall(r'INSTRUMENTATION_STATUS_CODE:\s*1\b', text))
    result['reported_success_count'] = (re.search(r'OK \((\d+) tests?\)', text) or [None, None])[1]
    if not re.search(r'OK \(' + str(len(manifest['methods'])) + r' tests?\)', text) or any(x in text for x in ['FAILURES!!!', 'INSTRUMENTATION_FAILED', 'Process crashed']):
        raise RuntimeError('Runner did not report the exact required successful method count')
    import runpy
    gate = runpy.run_path(str(Path(__file__).with_name('run-device-gate.py')))
    required = {tuple(m.split('#', 1)) for m in manifest['methods']}
    passed = gate['verified_tests'](text, required=required)
    if passed != required: raise RuntimeError('Unexpected per-method status set')
    result['method_results'] = [{'method': c + '#' + m, 'status': 'passed'} for c, m in sorted(passed)]
    result['status'] = 'passed'
except Exception as error:
    result['status'] = 'blocked-or-failed'
    result['reason'] = str(error)
finally:
    result['log_sha256'] = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in out.glob('*.log')}
    (out / 'RESULT.json').write_text(json.dumps(result, indent=2) + '\n')
print('Result:', out / 'RESULT.json')
if result['status'] != 'passed':
    raise SystemExit(1)
