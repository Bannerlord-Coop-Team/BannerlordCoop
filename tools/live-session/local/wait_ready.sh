#!/usr/bin/env bash
# Poll endpoint status until the server shows both clients connected and both clients are campaign-ready.
unset PSModulePath
token=$1
cd "$(dirname "$(readlink -f "$0")")"
declare -A attempted=()
for i in $(seq 1 60); do
  s=$(LT_TOKEN=$token powershell.exe -NoProfile -File status.ps1 2>&1)
  for pid in $(echo "$s" | sed -n 's/^\([0-9]*\) client .*state=InitialState.*campaign=False.*/\1/p'); do
    [[ -v attempted[$pid] ]] && continue
    attempted[$pid]=1
    powershell.exe -NoProfile -File 'C:\Users\Andrew\CodexWindows\.codex\skills\issue-to-pr\scripts\live_test_client.ps1' -Action Join -ProcessId "$pid" -ExpectedRunToken "$token"
  done
  n=$(echo "$s" | grep -c "client.*readyCampaign=True")
  if [ "$n" -ge 2 ] && echo "$s" | grep -Eq "testclient:True,testclient2:True|testclient2:True,testclient:True"; then echo "$s"; exit 0; fi
  sleep 15
done
echo "$s"; exit 1
