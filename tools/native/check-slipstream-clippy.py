#!/usr/bin/env python3
"""Report frozen upstream style debt while rejecting new compiler/Clippy failures."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

def diagnostic_key(message):
    primary = next((s for s in message.get('spans', []) if s.get('is_primary')), None)
    code = message.get('code') or {}
    return (primary.get('file_name'), code.get('code'), primary.get('line_start')) if primary else None

def audit(messages, baseline, source):
    allowed = {(row['file'], row['code'], row['line']): row['sha256'] for row in baseline}
    seen = set()
    unknown = []
    for message in messages:
        if message.get('level') != 'error':
            continue
        key = diagnostic_key(message)
        path = source / key[0] if key else None
        digest = hashlib.sha256(path.read_text(encoding='utf-8').replace('\r\n', '\n').encode()).hexdigest() if path and path.is_file() else None
        if key not in allowed or digest != allowed[key]:
            unknown.append(message.get('rendered') or message.get('message'))
        else:
            seen.add(key)
    return seen, unknown

def main():
    source = Path(sys.argv[1]).resolve()
    baseline = json.loads(Path(__file__).with_name('slipstream-clippy-baseline.json').read_text())
    command = ['cargo', 'clippy', '--locked', '--workspace', '--all-targets', '--features',
               'openssl-vendored,picoquic-minimal-build', '--message-format=json', '--', '-D', 'warnings']
    result = subprocess.run(command, cwd=source, capture_output=True, text=True, encoding='utf-8')
    stderr = re.sub(r'\x1b\[[0-9;]*m', '', result.stderr)
    messages = []
    for line in result.stdout.splitlines():
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        if event.get('reason') == 'compiler-message':
            messages.append(event['message'])
    seen, unknown = audit(messages, baseline, source)
    print(stderr[-16000:], flush=True)
    for message in unknown:
        print(message, flush=True)
    unexpected_cargo = [line for line in stderr.splitlines() if line.startswith('error:') and
                        not re.fullmatch(r'error: could not compile `slipstream-(client|server)`.*due to \d+ previous errors?(; \d+ warnings? emitted)?', line)]
    if unknown or unexpected_cargo or (result.returncode and not seen):
        raise SystemExit('New/unknown Slipstream Clippy or build failure; old-code exceptions cannot cover it')
    if seen:
        print('Retained frozen upstream style diagnostics (owner requested no unrelated old-code edits):', sorted(seen), flush=True)
    else:
        print('PASS workspace Clippy without inherited diagnostics', flush=True)

if __name__ == '__main__':
    main()
