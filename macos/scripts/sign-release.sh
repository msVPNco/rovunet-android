#!/bin/bash
set -euo pipefail
# Offline signing/package stage. No Apple upload, GitHub write or publication.
app=${1:?App path required}
identity=${2:?Developer ID Application identity required}
out=${3:?Output directory required}
python=${4:-python3}
installer_identity=${5:?Developer ID Installer identity required for the embedded service package}
[[ $(uname -s) == Darwin && -d "$app/Contents/MacOS" ]] || exit 2
mkdir -p "$out"
mac="$app/Contents/MacOS"
entitlements="$app/Contents/Resources/Rovunet.entitlements"
while IFS= read -r -d '' library; do codesign --force --timestamp --options runtime --sign "$identity" "$library"; done < <(find "$mac" -type f -name '*.dylib' -print0)
for name in xray naive rovunet-transports rovunet-supervisor slipstream-client rovunet-awg rovunet-tun rovunet-extra rovunet-slip-trust tor/tor tor/pluggable_transports/lyrebird; do codesign --force --timestamp --options runtime --sign "$identity" "$mac/engines/$name"; done
codesign --verify --strict -R='anchor apple generic and certificate leaf[field.1.2.840.113635.100.6.1.13] exists' "$mac/engines/rovunet-tun"
# Re-signing changes the client's CDHash: recreate its embedded helper policy
# after signing so upgrading the helper authorizes exactly the shipped client.
"$python" "$(dirname "$0")/package-tun-service.py" "$mac/engines/rovunet-tun" "$app/Contents/Resources/Rovunet-TUN.pkg"
/usr/bin/productsign --sign "$installer_identity" "$app/Contents/Resources/Rovunet-TUN.pkg" "$app/Contents/Resources/Rovunet-TUN-signed.pkg"
/usr/sbin/pkgutil --check-signature "$app/Contents/Resources/Rovunet-TUN-signed.pkg"
/bin/mv "$app/Contents/Resources/Rovunet-TUN-signed.pkg" "$app/Contents/Resources/Rovunet-TUN.pkg"
"$python" "$(dirname "$0")/verify-tun-package.py" "$app/Contents/Resources/Rovunet-TUN.pkg" "$mac/engines/rovunet-tun"
rid=$(/usr/bin/plutil -extract rid raw -o - "$app/Contents/Resources/BUILD-INFO.json")
/bin/bash "$(dirname "$0")/write-engine-manifest.sh" "$mac/engines" "$rid"
for name in Rovunet.Agent Rovunet; do codesign --force --timestamp --options runtime --entitlements "$entitlements" --sign "$identity" "$mac/$name"; done
"$python" - "$app/Contents/Resources/BUILD-INFO.json" <<'PY'
import json, pathlib, sys
p=pathlib.Path(sys.argv[1]); info=json.loads(p.read_text(encoding='utf-8-sig'))
info.update(developerIdSigned=True,notarized=False,developmentAdhocSigned=False,
            tunServiceCompatibility='Signed helper policy regenerated; install the matching embedded service package on upgrade')
p.write_text(json.dumps(info,indent=2)+'\n',encoding='utf-8')
PY
codesign --force --timestamp --options runtime --entitlements "$entitlements" --sign "$identity" "$app"
codesign --verify --strict --deep --verbose=2 "$app"
codesign --verify --strict -R='anchor apple generic and certificate leaf[field.1.2.840.113635.100.6.1.13] exists' "$app"
ditto -c -k --sequesterRsrc --keepParent "$app" "$out/Rovunet-signed-review.zip"
(cd "$out" && shasum -a 256 Rovunet-signed-review.zip > SHA256SUMS.txt)
echo "Signed review ZIP created. Notarization/upload/publication are separate pending stages."
