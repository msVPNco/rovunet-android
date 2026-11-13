#!/bin/bash
set -euo pipefail
# Explicit distribution stage, invoked separately by the publisher on a Mac.
# Credentials stay in a preconfigured notarytool keychain profile, never argv.
package=${1:?Signed pkg or dmg required}
profile=${2:?Existing notarytool keychain profile name required}
[[ $(uname -s) == Darwin && -f "$package" ]] || exit 2
case "$package" in
  *.pkg) /usr/sbin/pkgutil --check-signature "$package" ;;
  *.dmg) /usr/bin/codesign --verify --strict "$package" ;;
  *) echo 'Expected a signed PKG or DMG' >&2; exit 2 ;;
esac
/usr/bin/xcrun notarytool submit "$package" --keychain-profile "$profile" --wait
/usr/bin/xcrun stapler staple "$package"
/usr/bin/xcrun stapler validate "$package"
case "$package" in
  *.pkg) /usr/sbin/spctl --assess --type install --verbose=2 "$package" ;;
  *.dmg) /usr/sbin/spctl --assess --type open --context context:primary-signature --verbose=2 "$package" ;;
esac
