#!/usr/bin/env python3
"""A bounded ratchet for inherited unsafe Kotlin constructs; does not replace Android lint/detekt."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
BASELINE = Path(__file__).with_name("kotlin-safety-baseline.json")
PATTERNS = {"force_unwrap": r"!!", "global_scope": r"\bGlobalScope\b", "run_blocking": r"\brunBlocking\s*[({]"}


def findings(root):
    found = Counter()
    for path in sorted(root.rglob("*.kt")):
        relative = path.relative_to(ROOT).as_posix()
        # Track the source text of each occurrence, not a whole-file allowance that new calls can consume.
        for line in path.read_text().splitlines():
            code = line.split("//", 1)[0].strip()
            if not code or code.startswith(("*", "/*", "import ")):
                continue
            for rule, pattern in PATTERNS.items():
                count = len(re.findall(pattern, code))
                if count:
                    digest = hashlib.sha256(re.sub(r"\s+", "", code).encode()).hexdigest()
                    found[f"{relative}:{rule}:{digest}"] += count
    return found


current = findings(ROOT / "app/src/main/java")
if sys.argv[1:] == ["--update-baseline"]:
    BASELINE.write_text(json.dumps(dict(sorted(current.items())), indent=2) + "\n")
    print(f"Recorded {sum(current.values())} existing occurrences; review baseline changes explicitly.")
elif sys.argv[1:]:
    raise SystemExit("Usage: check-kotlin-safety.py [--update-baseline]")
else:
    baseline = Counter(json.loads(BASELINE.read_text()))
    added = current - baseline
    if added:
        raise SystemExit("New unsafe constructs require a fix or explicit reviewed baseline change:\n" + "\n".join(added))
    print(f"PASS Kotlin safety ratchet: {sum(current.values())} inherited occurrences, zero additions.")
