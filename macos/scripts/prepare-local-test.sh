#!/bin/bash
set -euo pipefail
# Private development preparation only. This does not notarize or publish.
base=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
app=${1:-"$base/Rovunet.app"}
python=${2:-python3}
if [[ $(uname -s) != Darwin || ! -d "$app/Contents/MacOS" ]]; then
  echo "Run on a Mac with the extracted Rovunet.app path as the first argument." >&2; exit 2
fi
mac="$app/Contents/MacOS"
entitlements="$app/Contents/Resources/Rovunet.entitlements"
for executable in Rovunet Rovunet.Agent engines/xray engines/naive engines/rovunet-transports engines/rovunet-supervisor engines/slipstream-client engines/rovunet-awg engines/rovunet-tun engines/rovunet-extra engines/rovunet-slip-trust engines/tor/tor engines/tor/pluggable_transports/lyrebird; do
  chmod u+x "$mac/$executable"
done
while IFS= read -r -d '' library; do
  /usr/bin/codesign --force --options runtime --sign - "$library"
done < <(/usr/bin/find "$mac" -type f -name '*.dylib' -print0)
for executable in xray naive rovunet-transports rovunet-supervisor slipstream-client rovunet-awg rovunet-tun rovunet-extra rovunet-slip-trust tor/tor tor/pluggable_transports/lyrebird; do
  /usr/bin/codesign --force --options runtime --sign - "$mac/engines/$executable"
done
"$python" "$base/package-tun-service.py" "$mac/engines/rovunet-tun" "$app/Contents/Resources/Rovunet-TUN.pkg"
"$python" "$base/verify-tun-package.py" "$app/Contents/Resources/Rovunet-TUN.pkg" "$mac/engines/rovunet-tun"
rid=$(/usr/bin/plutil -extract rid raw -o - "$app/Contents/Resources/BUILD-INFO.json")
/bin/bash "$base/write-engine-manifest.sh" "$mac/engines" "$rid"
for executable in Rovunet.Agent Rovunet; do
  /usr/bin/codesign --force --options runtime --sign - --entitlements "$entitlements" "$mac/$executable"
done
"$python" - "$app/Contents/Resources/BUILD-INFO.json" <<'PYINFO'
import json,pathlib,sys
p=pathlib.Path(sys.argv[1]);d=json.loads(p.read_text(encoding='utf-8-sig'))
d.update(developerIdSigned=False,notarized=False,developmentAdhocSigned=True,signatureTool='Apple codesign; local development re-sign',tunServiceCompatibility='Re-signed client: install its matching embedded helper package')
p.write_text(json.dumps(d,indent=2)+'\n',encoding='utf-8')
PYINFO
/usr/bin/codesign --force --options runtime --sign - --entitlements "$entitlements" "$app"
/usr/bin/codesign --verify --strict --deep --verbose=2 "$app"
echo "Prepared a locally signed development copy. Public Developer ID signing, notarization and macOS acceptance are still pending."
