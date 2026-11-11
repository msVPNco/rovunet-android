"""Build the private auxiliary adapter against the pinned Mihomo source via Go overlay."""
from pathlib import Path
import argparse,json,subprocess

parser=argparse.ArgumentParser();parser.add_argument('--source',required=True,type=Path);parser.add_argument('--output',required=True,type=Path);parser.add_argument('--go',default='go');args=parser.parse_args()
source=args.source.resolve();mac=Path(__file__).resolve().parents[1]
commit=subprocess.check_output(['git','-C',str(source),'rev-parse','HEAD'],text=True).strip()
if commit!='3f27ef39f8c96bbfbc8b0b9bb10f5c9ef60149d8':raise RuntimeError('Mihomo source pin mismatch')
overlay={'Replace':{str(source/'cmd/rovunet-awg/main.go'):str(mac/'native/extra/main.go'),str(source/'cmd/rovunet-awg/socks.go'):str(mac/'native/extra/socks.go'),str(source/'cmd/rovunet-awg/runtime_test.go'):''}}
temporary=mac/'artifacts/extra-overlay.json';temporary.parent.mkdir(parents=True,exist_ok=True);temporary.write_text(json.dumps(overlay),encoding='utf-8')
subprocess.run([args.go,'build','-C',str(source),'-mod=vendor','-overlay',str(temporary),'-buildvcs=false','-trimpath','-ldflags=-s -w -checklinkname=0','-o',str(args.output),'./cmd/rovunet-awg'],check=True)
