#!/usr/bin/env python3
"""Prevent new untranslated prose in the configuration editor's direct UI strings.

Protocol names, wire syntax and example values are intentionally left intact.
The resource parity/format/plural contract is checked by check-settings-localization.py.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
sources = root / "app/src/main/java/app/slipnet/presentation/profiles"
technical = {"REALITY", "XHTTP", "UUID", "CDN IP", "TLS SNI",
             "obfs4 IP:PORT FINGERPRINT cert=... iat-mode=0",
             "-----BEGIN OPENSSH PRIVATE KEY-----",
             "GET / HTTP/1.1[crlf]Host: [host][crlf][crlf]",
             "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"}
pattern = re.compile(r'(?:\bText\s*\(\s*(?:text\s*=\s*)?|\bcontentDescription\s*=\s*)"((?:[^"\\]|\\.)*)"')
checked = 0
for name in ("EditProfileScreen.kt", "CoreProfileEditor.kt"):
    source = (sources / name).read_text()
    assert 'context.getString(' not in source, "Use the current composition resources after an in-place language switch"
    for match in pattern.finditer(source):
        text = match[1]
        visible = re.sub(r'\$\{[^}]*\}|\$[A-Za-z_][A-Za-z_0-9]*', '', text)
        if not re.search(r'[A-Za-z]', visible):
            continue
        example = re.fullmatch(r'(?:https?://)?[A-Za-z0-9.-]+\.(?:com|invalid)(?:/dns-query)?', text)
        assert text in technical or example, (name, source.count('\n', 0, match.start()) + 1, text)
        checked += 1
vm = (sources / "EditProfileViewModel.kt").read_text()
assert not re.search(r'\b(?:error|\w+Error)\s*=\s*"[A-Za-z]', vm), "Localize field errors through resource IDs"
print(f"PASS editor direct UI text contract ({checked} technical/example literals retained)")
