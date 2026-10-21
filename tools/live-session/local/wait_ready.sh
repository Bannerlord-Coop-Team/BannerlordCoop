#!/usr/bin/env bash
# Poll endpoint status until the server shows both clients connected and both clients are campaign-ready.
unset PSModulePath
token=$1
source "$(dirname "${BASH_SOURCE[0]}")/local_env.sh"
cd "$here"
declare -A attempted=()
for i in $(seq 1 60); do
  s=$("$hidden" "$powershell" -NoProfile -NonInteractive -WindowStyle Hidden -File "$(cygpath -w "$here/status.ps1")" -Token "$token" 2>&1)
  for pid in $(echo "$s" | sed -n 's/^\([0-9]*\) client .*state=InitialState.*campaign=False.*/\1/p'); do
    [[ -v attempted[$pid] ]] && continue
    response=$("$hidden" "$powershell" -NoProfile -NonInteractive -WindowStyle Hidden -File "$(cygpath -w "$skill/live_test_client.ps1")" -Action Join -ProcessId "$pid" -ExpectedRunToken "$token")
    echo "$response"
    if echo "$response" | grep -Eq '"ok"[[:space:]]*:[[:space:]]*true'; then attempted[$pid]=1; fi
  done
  n=$(echo "$s" | grep -c "client.*readyCampaign=True")
  if [ "$n" -ge 2 ] && echo "$s" | grep -Eq "testclient:True,testclient2:True|testclient2:True,testclient:True"; then echo "$s"; exit 0; fi
  sleep 15
done
echo "$s"; exit 1
