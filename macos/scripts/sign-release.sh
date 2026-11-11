#!/bin/bash
set -euo pipefail
# Offline signing/package stage. No Apple upload, GitHub write or publication.
app=${1:?App path required}
identity=${2:?Developer ID Application identity required}
out=${3:?Output directory required}
[[ $(uname -s) == Darwin && -d "$app/Contents/MacOS" ]] || exit 2
mkdir -p "$out"
mac="$app/Contents/MacOS"
entitlements="$app/Contents/Resources/Rovunet.entitlements"
while IFS= read -r -d '' library; do codesign --force --timestamp --options runtime --sign "$identity" "$library"; done < <(find "$mac" -type f -name '*.dylib' -print0)
for name in xray naive rovunet-transports rovunet-supervisor slipstream-client rovunet-awg tor/tor tor/pluggable_transports/lyrebird; do codesign --force --timestamp --options runtime --sign "$identity" "$mac/engines/$name"; done
rid=$(/usr/bin/plutil -extract rid raw -o - "$app/Contents/Resources/BUILD-INFO.json")
/bin/bash "$(dirname "$0")/write-engine-manifest.sh" "$mac/engines" "$rid"
for name in Rovunet.Agent Rovunet; do codesign --force --timestamp --options runtime --entitlements "$entitlements" --sign "$identity" "$mac/$name"; done
codesign --force --timestamp --options runtime --entitlements "$entitlements" --sign "$identity" "$app"
codesign --verify --strict --verbose=2 "$app"
ditto -c -k --sequesterRsrc --keepParent "$app" "$out/Rovunet-signed-review.zip"
(cd "$out" && shasum -a 256 Rovunet-signed-review.zip > SHA256SUMS.txt)
echo "Signed review ZIP created. Notarization/upload/publication are separate pending stages."
