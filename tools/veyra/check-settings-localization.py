#!/usr/bin/env python3
"""Fail on missing translations, changed format arguments or new raw UI sentences."""
from pathlib import Path
import re,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
res=root/'app/src/main/res'
folders=['values','values-fa','values-zh','values-ru']
resources={}
for folder in folders:
 entries={}
 for file in (res/folder).glob('*.xml'):
  for node in ET.parse(file).getroot():
   if node.tag in ('string','plurals'):
    assert node.attrib['name'] not in entries,(folder,node.attrib['name'])
    entries[node.attrib['name']]=node
 resources[folder]=entries
files=list((root/'app/src/main/java/app/slipnet/presentation').rglob('*.kt'))
files.append(root/'app/src/main/java/app/slipnet/SlipNetApp.kt')
used=set()
for file in files:
 source=file.read_text()
 used.update(re.findall(r'(?<!android\.)R\.(?:string|plurals)\.([a-z0-9_]+)',source))
 for match in re.finditer(r'(?:Text\s*\(\s*(?:text\s*=\s*)?|(?:title|description|subtitle|supportingText|contentDescription)\s*=\s*)"((?:[^"\\]|\\.)*)"',source):
  text=match[1]
  visible=re.sub(r'\$\{[^}]*\}|\$[A-Za-z_][A-Za-z_0-9]*','',text)
  visible=re.sub(r'\\[nrt]', ' ', visible)
  visible=re.sub(r'\\u[0-9A-Fa-f]{4}', '', visible)
  if not re.search('[A-Za-z]',visible):continue
  allowed=('Rovunet VPN v','SOCKS5:','HTTP:','HTTPS ', '• ', 'rovunet://', 'USDT •', '@RovunetVPN', 'obfs4 ', '-----BEGIN ', 'GET / HTTP/1.1', '↑ ', '↓ ')
  assert text.startswith(allowed) or text in ('DNS','AES-128-GCM','ChaCha20-Poly1305','Prism','E2E','IP','UDP','TCP','google.com','http://www.gstatic.com/generate_204','Language · زبان · 语言 · Язык','REALITY','XHTTP','UUID','CDN IP','TLS SNI','xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx','proxy.example.com','your-domain.example.com','www.google.com','example.com','cdn.example.com','https://example.com/dns-query') or not re.search('[A-Za-z]', re.sub(r'%[,0-9$]*[ds]|ms|/s', '', visible)),(file.name,text)
def args(value):return sorted(re.findall(r'%(\d+)\$([sd])',value or ''))
required = {key for key,node in resources['values'].items() if node.attrib.get('translatable') != 'false'}
for key in required:
 original=resources['values'][key]
 signature=args(original.text) if original.tag=='string' else args(original.find('item').text)
 for folder in folders:
  node=resources[folder].get(key)
  assert node is not None,(folder,key)
  texts=[node.text] if node.tag=='string' else [child.text for child in node]
  for text in texts:
   assert text and text.strip(),(folder,key,'empty')
   assert args(text)==signature,(folder,key,'format arguments')
  if folder=='values-ru' and node.tag=='plurals':assert {'one','few','many','other'}<=set(c.attrib['quantity'] for c in node),(key,'Russian plural categories')
main=(root/'app/src/main/java/app/slipnet/presentation/main/MainScreen.kt').read_text()
assert 'activity?.recreate()' not in main,'Language changes must not recreate Activity'
print(f'PASS all {len(required)} translatable resource keys in four languages, format parity and Russian plurals; all presentation raw-text and Activity checks ({len(used)} used keys)')
