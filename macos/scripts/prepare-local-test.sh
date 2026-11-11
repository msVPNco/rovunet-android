#!/bin/bash
set -euo pipefail
# Private development preparation only. This does not notarize or publish.
base=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
app=${1:-"$base/Rovunet.app"}
if [[ $(uname -s) != Darwin || ! -d "$app/Contents/MacOS" ]]; then
  echo "Run on a Mac with the extracted Rovunet.app path as the first argument." >&2; exit 2
fi
mac="$app/Contents/MacOS"
entitlements="$app/Contents/Resources/Rovunet.entitlements"
for executable in Rovunet Rovunet.Agent engines/xray engines/naive engines/rovunet-transports engines/rovunet-supervisor engines/slipstream-client engines/rovunet-awg engines/tor/tor engines/tor/pluggable_transports/lyrebird; do
  chmod u+x "$mac/$executable"
done
while IFS= read -r -d '' library; do
  /usr/bin/codesign --force --sign - "$library"
done < <(/usr/bin/find "$mac" -type f -name '*.dylib' -print0)
for executable in xray naive rovunet-transports rovunet-supervisor slipstream-client rovunet-awg tor/tor tor/pluggable_transports/lyrebird; do
  /usr/bin/codesign --force --sign - "$mac/engines/$executable"
done
rid=$(/usr/bin/plutil -extract rid raw -o - "$app/Contents/Resources/BUILD-INFO.json")
/bin/bash "$base/write-engine-manifest.sh" "$mac/engines" "$rid"
for executable in Rovunet.Agent Rovunet; do
  /usr/bin/codesign --force --sign - --entitlements "$entitlements" "$mac/$executable"
done
/usr/bin/codesign --force --sign - --entitlements "$entitlements" "$app"
/usr/bin/codesign --verify --strict --verbose=2 "$app"
echo "Prepared a locally signed development copy. Public Developer ID signing, notarization and macOS acceptance are still pending."
