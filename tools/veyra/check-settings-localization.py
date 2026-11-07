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
files.extend((root/'app/src/main/java/app/slipnet/widget').glob('*.kt'))
files.append(root/'app/src/main/java/app/slipnet/SlipNetApp.kt')
def drop_conditions(text):
 text=' if'+text if text.lstrip().startswith('(') else text
 while True:
  found=re.search(r'\bif\s*\(',text)
  if not found:return text
  depth=0
  for index in range(found.end()-1,len(text)):
   depth+=(text[index]=='(')-(text[index]==')')
   if depth==0:
    text=text[:found.start()]+' '+text[index+1:]
    break
  else:return text
used=set()
for file in files:
 source=file.read_text()
 assert "ProfileGuidance.GUIDE" not in source,(file.name,"raw shared guide bypasses resource translation")
 used.update(re.findall(r'(?<!android\.)R\.(?:string|plurals)\.([a-z0-9_]+)',source))
 for match in re.finditer(r'(?:Toast\.makeText\s*\(\s*[^,\n]+,\s*|set(?:TextViewText|ContentDescription)\s*\(\s*[^,\n]+,\s*|showSnackbar\s*\(\s*|set(?:Title|Message)\s*\(\s*)"((?:[^"\\]|\\.)*)"',source):
  assert not re.search('[A-Za-z]',match[1]),(file.name,'raw transient UI text',match[1])
 # Text(if (...) "English" else "English") has no resource id; Text(rv(if ...)) does.
 for match in re.finditer(r'\bText\s*\(\s*if\b([^\n]*)',source):
  rest=drop_conditions(match[1])
  rest=re.sub(r'stringResource\([^)]*\)','',rest)
  for literal in re.findall(r'"((?:[^"\\]|\\.)*)"',rest):
   if re.search('[A-Za-z]',re.sub(r'\$\{[^}]*\}|\$[A-Za-z_][A-Za-z_0-9]*','',literal)) and literal not in ('google.com',) and not re.fullmatch(r'e\.g\. [0-9.:/]+',literal):
    raise AssertionError((file.name,'conditional raw UI text; wrap it in rv(...) or use a string resource',literal))
 for match in re.finditer(r'(?:Text\s*\(\s*(?:text\s*=\s*)?|(?:title|description|subtitle|supportingText|contentDescription)\s*=\s*)"((?:[^"\\]|\\.)*)"',source):
  text=match[1]
  visible=re.sub(r'\$\{[^}]*\}|\$[A-Za-z_][A-Za-z_0-9]*','',text)
  visible=re.sub(r'\\[nrt]', ' ', visible)
  visible=re.sub(r'\\u[0-9A-Fa-f]{4}', '', visible)
  if not re.search('[A-Za-z]',visible):continue
  allowed=('Rovunet VPN v','SOCKS5:','HTTP:','HTTPS ', '• ', 'rovunet://', 'USDT •', '@RovunetVPN', 'obfs4 ', '-----BEGIN ', 'GET / HTTP/1.1', '↑ ', '↓ ')
  assert text.startswith(allowed) or text in ('DNS','AES-128-GCM','ChaCha20-Poly1305','Prism','E2E','IP','UDP','TCP','google.com','http://www.gstatic.com/generate_204','Language · زبان · 语言 · Язык','REALITY','XHTTP','UUID','CDN IP','TLS SNI','xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx','proxy.example.com','your-domain.example.com','www.google.com','example.com','cdn.example.com','https://example.com/dns-query') or not re.search('[A-Za-z]', re.sub(r'%[,0-9$]*[ds]|ms|/s', '', visible)),(file.name,text)
mapped=set(re.findall(r'"((?:[^"\\]|\\.)*)"\s+to\s+',(root/'app/src/main/java/app/slipnet/presentation/theme/RovunetStrings.kt').read_text()))
for model in ('main/MainViewModel.kt','chain/EditChainViewModel.kt'):
 path=root/'app/src/main/java/app/slipnet/presentation'/model
 if not path.exists():continue
 for match in re.finditer(r'\berror\s*=\s*"([A-Za-z][^"$\\]*)"',path.read_text()):
  text=match[1]
  # Codes such as DATABASE_UNAVAILABLE are mapped by UiErrors; prose must have a translation,
  # or the user only sees the generic error text.
  assert text==text.upper() or text in mapped,(path.name,'error message without translation',text)
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
for file in (res/'layout').glob('*.xml'):
 for node in ET.parse(file).iter():
  for attribute in ('text','hint','contentDescription'):
   value=node.attrib.get('{http://schemas.android.com/apk/res/android}'+attribute,'')
   assert not value or value.startswith(('@','?')) or not re.search('[A-Za-z]',value),(file.name,attribute,value)
assert 'activity?.recreate()' not in main,'Language changes must not recreate Activity'
print(f'PASS all {len(required)} translatable resource keys in four languages, format parity and Russian plurals; all presentation raw-text and Activity checks ({len(used)} used keys)')
