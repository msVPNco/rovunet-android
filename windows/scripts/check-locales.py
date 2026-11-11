#!/usr/bin/env python3
import json,pathlib,re
root=pathlib.Path(__file__).resolve().parents[1]
locales={p.stem:json.loads(p.read_text()) for p in (root/'src/Rovunet.Desktop/Locales').glob('*.json')}
expected=set(locales['en'])
assert set(locales)=={'fa','en','zh-CN','ru'}
for name,values in locales.items():
    assert set(values)==expected,name
    assert all(isinstance(s,str) and s.strip() for s in values.values()),name
used=set()
for p in (root/'src/Rovunet.Desktop').glob('*.cs'):
    used|=set(re.findall(r'(?:L.Text|Button|Dialog|MenuAction|IconButton|Field|Meta|Nav|Metric)\("([A-Z_]+)"',p.read_text()))
assert used<=expected,used-expected
print(f'Four locales: {len(expected)} nonempty keys each; {len(used)} literal UI keys checked')
