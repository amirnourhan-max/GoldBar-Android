#!/usr/bin/env bash
set -euo pipefail

mkdir -p build
adb logcat -c
adb install -r app/build/outputs/apk/debug/app-debug.apk
adb shell am force-stop com.amirnourhan.goldbar
adb shell am start -W -n com.amirnourhan.goldbar/.MainActivity
sleep 4

adb shell pidof com.amirnourhan.goldbar | tr -d '\r' > build/pid-start.txt
test -s build/pid-start.txt
adb shell uiautomator dump /sdcard/start.xml
adb pull /sdcard/start.xml build/start.xml
grep -q 'content-desc="gold-bar-title"' build/start.xml

found=0
for i in 1 2 3 4 5 6; do
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

adb logcat -d > build/logcat.txt
adb shell pidof com.amirnourhan.goldbar | tr -d '\r' > build/pid.txt
test -s build/pid.txt
! grep -E 'FATAL EXCEPTION: main|Process: com\.amirnourhan\.goldbar' build/logcat.txt
cat build/pid.txt
