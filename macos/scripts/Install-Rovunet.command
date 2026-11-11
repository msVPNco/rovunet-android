#!/bin/bash
set -euo pipefail
trap 'result=$?; if [[ $result != 0 ]]; then echo "نصب کامل نشد؛ متن خطای بالا را نگه دارید."; read -r -p "برای بستن Enter بزنید..."; fi' EXIT
base=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
echo "Rovunet — local development installer"
echo "نصب نسخهٔ توسعهٔ Rovunet؛ پنجره را تا پایان نبندید."
if [[ $(uname -s) != Darwin ]]; then echo "این فایل باید روی مک اجرا شود."; exit 2; fi
app="$base/Rovunet.app"
if [[ ! -d "$app/Contents/MacOS" ]]; then echo "Rovunet.app باید کنار این فایل باشد. ابتدا ZIP را کامل استخراج کنید."; read -r -p "Enter..."; exit 2; fi
rid=$(/usr/bin/plutil -extract rid raw -o - "$app/Contents/Resources/BUILD-INFO.json")
case "$(uname -m):$rid" in arm64:osx-arm64|x86_64:osx-x64) ;; *) echo "این بسته با پردازندهٔ مک سازگار نیست."; exit 2;; esac
# Prepare inside the extracted folder. The original ZIP remains unchanged.
/bin/bash "$base/prepare-local-test.sh" "$app"
destination=/Applications
if [[ ! -w "$destination" ]]; then destination="$HOME/Applications"; /bin/mkdir -p "$destination"; fi
target="$destination/Rovunet.app"
if [[ -e "$target" ]]; then
  if /usr/bin/pgrep -x Rovunet >/dev/null; then echo "ابتدا Rovunet را با Command-Q ببندید و دوباره نصب را اجرا کنید."; read -r -p "Enter..."; exit 3; fi
  backup="$destination/Rovunet-previous-$(date +%Y%m%d-%H%M%S).app"
  /bin/mv "$target" "$backup"
  echo "نسخهٔ قبلی نگه داشته شد: $backup"
fi
/usr/bin/ditto "$app" "$target"
/usr/bin/codesign --verify --strict "$target"
echo "نصب انجام شد: $target"
echo "اگر مک اجازهٔ اجرا خواست، در Privacy & Security فقط برای Rovunet روی Open Anyway بزنید."
echo "پراکسی سیستم مجوز جداگانهٔ macOS می‌خواهد؛ این خروجی VPN/TUN کامل نیست."
/usr/bin/open "$target"
