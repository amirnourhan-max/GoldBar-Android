#!/usr/bin/env bash
set -euxo pipefail

mkdir -p build
adb logcat -c || true
adb install -r app/build/outputs/apk/debug/app-debug.apk
sleep 3

# Diagnose widget discovery at PackageManager level first.
adb shell dumpsys package com.amirnourhan.goldbar > build/package-widget.txt
adb shell cmd package query-receivers --brief --components --user 0 \
  -a android.appwidget.action.APPWIDGET_UPDATE > build/query-receivers.txt
cat build/query-receivers.txt
grep -q 'com.amirnourhan.goldbar/.QuickCalcWidget' build/query-receivers.txt

# Check AppWidgetService before and after launching the app.
adb shell dumpsys appwidget > build/appwidget-before.txt
adb shell am force-stop com.amirnourhan.goldbar
adb shell am start -W -n com.amirnourhan.goldbar/.MainActivityV112
sleep 5
adb shell dumpsys appwidget > build/appwidget-after.txt

# Capture diagnostics without failing the smoke test on transient logcat transport errors.
adb logcat -d > build/logcat-initial.txt || true
adb shell dumpsys package com.amirnourhan.goldbar > build/package.txt
adb shell pidof com.amirnourhan.goldbar | tr -d '\r' > build/pid-start.txt
test -s build/pid-start.txt

grep -q 'com.amirnourhan.goldbar' build/appwidget-after.txt
grep -q 'QuickCalcWidget' build/appwidget-after.txt

adb shell uiautomator dump /sdcard/start.xml
adb pull /sdcard/start.xml build/start.xml
grep -q 'content-desc="gold-bar-title"' build/start.xml
grep -q 'content-desc="summary-after-alloy"' build/start.xml
grep -q 'content-desc="draggable-section-summary"' build/start.xml

# Confirm the compact clear-all shortcut exists inside Quick Entry.
clear_found=0
for i in 1 2 3 4; do
  adb shell uiautomator dump /sdcard/clear.xml >/dev/null
  adb pull /sdcard/clear.xml build/clear.xml >/dev/null
  if grep -q 'content-desc="clear-all-quick-button"' build/clear.xml; then
    clear_found=1
    break
  fi
  adb shell input swipe 520 1550 520 650 350
  sleep 1
done
test "$clear_found" = "1"

# Scroll through the lower-assay area and ensure the two removed metrics are not rendered.
adb shell input swipe 520 1750 520 500 450
sleep 1
adb shell uiautomator dump /sdcard/lower.xml >/dev/null
adb pull /sdcard/lower.xml build/lower.xml >/dev/null
! grep -q '۰.۴٪ کل وزن (g)' build/lower.xml
! grep -q 'بار نهایی دیگر (g)' build/lower.xml

# Scroll until the in-app quick calculator is visible.
found=0
for i in 1 2 3 4 5 6 7 8; do
  adb shell uiautomator dump /sdcard/tools.xml >/dev/null
  adb pull /sdcard/tools.xml build/tools.xml >/dev/null
  if grep -q 'content-desc="quick-split-base"' build/tools.xml; then
    found=1
    break
  fi
  adb shell input swipe 520 1750 520 350 450
  sleep 1
done
test "$found" = "1"
grep -q 'محاسبه سریع' build/tools.xml

# Tap the quick calculator field and verify the IME does not hide the active field.
python3 - <<'PY'
import re
import xml.etree.ElementTree as ET
root = ET.parse('build/tools.xml').getroot()
node = next((n for n in root.iter('node') if n.attrib.get('content-desc') == 'quick-split-base'), None)
if node is None:
    raise SystemExit('quick-split-base field not visible')
nums = list(map(int, re.findall(r'\d+', node.attrib['bounds'])))
x = (nums[0] + nums[2]) // 2
y = (nums[1] + nums[3]) // 2
with open('build/tap.sh', 'w') as f:
    f.write(f'adb shell input tap {x} {y}\n')
PY

bash build/tap.sh
sleep 2
adb shell dumpsys input_method > build/input-method.txt
grep -Eq 'mInputShown=true|mIsInputViewShown=true|inputShown=true|mShowRequested=true' build/input-method.txt
adb shell uiautomator dump /sdcard/keyboard.xml
adb pull /sdcard/keyboard.xml build/keyboard.xml
grep -q 'content-desc="quick-split-base"' build/keyboard.xml
adb exec-out screencap -p > build/keyboard.png

# Close IME and locate the report-export button at the bottom.
adb shell input keyevent 4
sleep 1
report_found=0
for i in 1 2 3 4 5 6 7 8; do
  adb shell uiautomator dump /sdcard/report.xml >/dev/null
  adb pull /sdcard/report.xml build/report.xml >/dev/null
  if grep -q 'content-desc="save-report-button"' build/report.xml; then
    report_found=1
    break
  fi
  adb shell input swipe 520 1750 520 350 450
  sleep 1
done
test "$report_found" = "1"

adb logcat -d > build/logcat.txt || true
adb shell pidof com.amirnourhan.goldbar | tr -d '\r' > build/pid.txt
test -s build/pid.txt
if test -s build/logcat.txt; then
  ! grep -E 'FATAL EXCEPTION: main|Process: com\.amirnourhan\.goldbar' build/logcat.txt
fi
cat build/pid.txt
