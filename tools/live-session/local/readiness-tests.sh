#!/usr/bin/env bash
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/codex/runtime/hidden-windows-exec" "$tmp/run" "$tmp/bin"
cat > "$tmp/codex/runtime/hidden-windows-exec/hidden_windows_exec.sh" <<'HOST'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$TEST_ROOT/calls"
[[ " $* " == *' -NonInteractive '* && " $* " == *' -WindowStyle Hidden '* ]]
if [[ "$*" == *status.ps1* ]]; then
  if [[ ! -f $TEST_ROOT/join101-ok || ! -f $TEST_ROOT/join102-ok ]]; then
    printf '101 client testclient state=InitialState campaign=False readyCampaign=False\n102 client testclient2 state=InitialState campaign=False readyCampaign=False\n'
  else
    printf '101 client testclient readyCampaign=True\n102 client testclient2 readyCampaign=True\n103 server roster=testclient:True,testclient2:True\n'
  fi
elif [[ "$*" == *'-ProcessId 101 '* ]]; then
  if [[ ! -f $TEST_ROOT/join101-refused ]]; then
    touch "$TEST_ROOT/join101-refused"
    printf '{"ok":false,"error":{"code":"client_start_refused"}}\n'
  else
    touch "$TEST_ROOT/join101-ok"
    printf '{"ok":true,"result":{"started":true}}\n'
  fi
elif [[ "$*" == *'-ProcessId 102 '* ]]; then
  touch "$TEST_ROOT/join102-ok"
  printf '{"ok":true,"result":{"started":true}}\n'
fi
HOST
printf '#!/usr/bin/env bash\nexit 0\n' > "$tmp/bin/sleep"
chmod +x "$tmp/bin/sleep" "$tmp/codex/runtime/hidden-windows-exec/hidden_windows_exec.sh"
TEST_ROOT=$tmp COOP_LIVE_ORCHESTRATION_ROOT=$tmp/run COOP_LIVE_CODEX_ROOT=$tmp/codex PATH="$tmp/bin:$PATH" bash "$here/wait_ready.sh" owned-run > "$tmp/ready"
[[ $(grep -c -- '-Action Join' "$tmp/calls") -eq 3 ]]
[[ $(grep -- '-Action Join' "$tmp/calls" | grep -c -- '-ProcessId 101 ') -eq 2 ]]
[[ $(grep -- '-Action Join' "$tmp/calls" | grep -c -- '-ProcessId 102 ') -eq 1 ]]
grep -q 'testclient:True,testclient2:True' "$tmp/ready"
echo 'PASS readiness joins both clients through the hidden host and requires their connected roster'
