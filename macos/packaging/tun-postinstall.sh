#!/bin/sh
# Installer runs this fixed script as root. It never executes the GUI or Agent.
set -eu
PATH=/usr/bin:/bin:/usr/sbin:/sbin
export PATH
test "$(id -u)" = 0
test "${3:-/}" = /
script_dir=$(CDPATH= cd -- "$(/usr/bin/dirname "$0")" && pwd -P)
helper=/Library/PrivilegedHelperTools/org.rovunet.tun.helper
policy=/Library/PrivilegedHelperTools/org.rovunet.tun.policy.json
plist=/Library/LaunchDaemons/org.rovunet.tun.helper.plist

# Root-only parents prevent replacement/symlink races at the install targets.
for directory in /Library /Library/PrivilegedHelperTools /Library/LaunchDaemons; do
    test ! -L "$directory"
    if test ! -d "$directory"; then /usr/bin/install -d -o root -g wheel -m 755 "$directory"; fi
    test "$(/usr/bin/stat -f %u "$directory")" = 0
    permissions=$(/usr/bin/stat -f %Lp "$directory")
    test $((0$permissions & 0022)) = 0
done
for file in "$helper" "$policy" "$plist" /var/run/rovunet-tun; do test ! -L "$file"; done
cd "$script_dir"
/usr/bin/shasum -a 256 -c SHA256SUMS >/dev/null
/bin/launchctl bootout system/org.rovunet.tun.helper >/dev/null 2>&1 || true
/usr/bin/install -o root -g wheel -m 755 ./rovunet-tun "$helper"
/usr/bin/install -o root -g wheel -m 644 ./policy.json "$policy"
/usr/bin/install -o root -g wheel -m 644 ./org.rovunet.tun.helper.plist "$plist"
/bin/launchctl bootstrap system "$plist"
exit 0
