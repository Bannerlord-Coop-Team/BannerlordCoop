#!/usr/bin/env bash
# Full cycle: (optional) stop previous run, (optional) rotate, restage fixture, build, start dedicated server, launch clients.
# usage: cycle.sh <source-worktree> <base-head> <issue> <new-out-dir> [previous-out-dir|-] [rotate] [server-save]
set -euo pipefail
export HOME=/home/pwisorlowska
# Windows PowerShell 5.1 must build its own module path; one inherited from pwsh 7 hides Get-FileHash and friends.
unset PSModulePath
root=$1; base=$2; issue=$3; out=$4; prev=${5:--}; rot=${6:-}; server_save=${7:-${LOCAL_SERVER_SAVE:-}}
here=/mnt/c/Users/Andrew/CodexWindows/coop-live-orchestration
if [[ $prev != - ]]; then bash "$here/local_down.sh" "$prev" >/dev/null 2>&1 || bash "$here/local_down.sh" "$prev" | tail -n 5; fi
# The Local bench is single-occupancy from rotate/build until local_down.sh releases it (exit 75 = busy).
bash "$here/bench_lock.sh" acquire "$issue" "$out" || exit $?
# A failed cycle tears down whatever it started (local_down.sh also releases the lock), so no game runs unlocked;
# a successful run keeps the lock until local_down.sh.
trap 'rc=$?; if [[ $rc -ne 0 ]]; then if [[ -f "$out/identity.env" ]]; then bash "$here/local_down.sh" "$out" >/dev/null 2>&1 || true; fi; bash "$here/bench_lock.sh" release "$issue" >/dev/null; fi' EXIT
bash "$here/make_fixture.sh" "$root" "$out"
steps="build prepare record start readiness"
[[ $rot == rotate ]] && steps="rotate $steps"
LOCAL_SERVER_SAVE="$server_save" bash "$here/local_up.sh" "$root" "$base" "issue$issue-$(date -u +%Y%m%dT%H%M%S)" "$out" $steps
# Free the idle compiler server and MSBuild nodes the build left (~0.6-0.9 GB) before the clients start; the bench lock
# keeps every other build waiting, so nothing else is using them (2026-10-18, runs dropped to <1 GB available).
/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -NoProfile -NonInteractive -Command \
  'Get-CimInstance Win32_Process | Where-Object { $_.Name -eq "VBCSCompiler.exe" -or ($_.CommandLine -match "MSBuild\.(dll|exe)" -and $_.CommandLine -match "nodemode") } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }' || true
# Start the clients outside this process tree: an agent's shell tool kills everything it started when its call returns,
# and that took `nohup ... &` with it, leaving runs with a server and no clients (2026-10-19, #3979 up1, #3810, #3954).
[[ $out != *[\'\"\ ]* ]] || { echo "out dir must not contain quotes or spaces: $out" >&2; exit 2; }
client_pid=$(/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass \
  -File 'C:\Users\Andrew\CodexWindows\coop-live-orchestration\detach_hidden.ps1' \
  -CommandLine "C:\\Users\\Andrew\\CodexWindows\\cygwin\\bin\\bash.exe -lc 'bash $here/clients_up.sh $out visual >$out/clients.log 2>&1'" | tr -d '\r')
[[ $client_pid =~ ^[0-9]+$ ]] || { echo "client launch failed: $client_pid" >&2; exit 1; }
echo "$client_pid" >"$out/clients.pid"
echo "CLIENTS_LAUNCHING pid=$client_pid"
