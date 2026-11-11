#!/bin/bash
set -euo pipefail
root=${1:?Engine directory required}
rid=${2:?Runtime ID required}
[[ "$rid" == osx-arm64 || "$rid" == osx-x64 ]] || exit 2
names=(xray naive rovunet-transports rovunet-supervisor slipstream-client rovunet-awg tor lyrebird)
paths=(xray naive rovunet-transports rovunet-supervisor slipstream-client rovunet-awg tor/tor tor/pluggable_transports/lyrebird)
deps=(lib/libssl.3.dylib lib/libcrypto.3.dylib tor/libevent-2.1.7.dylib data/geoip data/geoip6 data/pt_config.json)
temporary=$(mktemp "$root/engine-manifest.XXXXXX")
trap 'rm -f "$temporary"' EXIT
hash() { /usr/bin/shasum -a 256 "$root/$1" | /usr/bin/awk '{print $1}'; }
{
    printf '{"rid":"%s","executables":{' "$rid"
    for ((i=0;i<${#names[@]};i++)); do [[ $i == 0 ]] || printf ','; printf '"%s":"%s"' "${names[$i]}" "$(hash "${paths[$i]}")"; done
    printf '},"files":{'
    for ((i=0;i<${#deps[@]};i++)); do [[ $i == 0 ]] || printf ','; printf '"%s":"%s"' "${deps[$i]}" "$(hash "${deps[$i]}")"; done
    printf '}}\n'
} > "$temporary"
mv -f "$temporary" "$root/engine-manifest.json"
