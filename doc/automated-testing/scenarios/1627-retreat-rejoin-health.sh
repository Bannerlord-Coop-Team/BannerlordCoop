#!/usr/bin/env bash
# Called once through live_test_lease.sh; all bench changes occur under its lease.
set -Eeuo pipefail
source_root=$(git rev-parse --show-toplevel)
carrier_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
attempt_root=${ISSUE1627_ATTEMPT_ROOT:?fresh attempt directory required}
token=${ISSUE1627_RUN_TOKEN:?fresh token required}
raw_capture_root='C:\Users\Andrew\AppData\Local\CodexLiveTestRaw\'"$token"
head=${ISSUE_TO_PR_SOURCE_HEAD:?adopted head required}
tree=${ISSUE_TO_PR_SOURCE_TREE:?adopted tree required}
live_root=/mnt/c/Users/Andrew/.codex-runtime/live-testing/BannerlordCoop
hidden=/home/pwisorlowska/.codex/runtime/hidden-windows-exec/hidden_windows_exec.sh
powershell='/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe'
[[ $token =~ ^[A-Za-z0-9_-]{1,64}$ && ! -e $attempt_root ]] || exit 64
[[ $(git rev-parse HEAD) == "$head" && $(git rev-parse HEAD^{tree}) == "$tree" ]] || exit 65
[[ -n ${ISSUE_TO_PR_TEST_RELEASE_TOKEN:-} ]] || { echo 'owned Local lease required'; exit 66; }
mkdir -p "$attempt_root"
launcher_pid=''
cleanup() {
    local status=$?
    trap - EXIT
    # The scenario requests shutdown once; recover only exact-token processes still alive.
    "$hidden" --env "ISSUE1627_CLEANUP_TOKEN=$token" --env "ISSUE1627_CLEANUP_PATH=$(wslpath -w "$attempt_root/process-cleanup.json")" "$powershell" -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command '
$token=$env:ISSUE1627_CLEANUP_TOKEN
$path=$env:ISSUE1627_CLEANUP_PATH
$ErrorActionPreference="Stop"
. "\\wsl.localhost\Ubuntu\home\pwisorlowska\.codex\skills\issue-to-pr\scripts\live_test_client.ps1"
$probe=Get-LiveTestCimProcessIds -ExpectedRunToken $token -TimeoutMilliseconds 10000
if (-not $script:LiveTestLastCimDiscovery.completed) { throw "process cleanup probe incomplete" }
foreach($gamePid in $probe) { Stop-Process -Id $gamePid -Force -ErrorAction Stop }
$deadline=[DateTime]::UtcNow.AddSeconds(20)
do {
    $after=Get-LiveTestCimProcessIds -ExpectedRunToken $token -TimeoutMilliseconds 10000
    if (-not $script:LiveTestLastCimDiscovery.completed) { throw "process cleanup verification incomplete" }
    if (@($after).Count -eq 0) { break }
    Start-Sleep -Milliseconds 500
} while ([DateTime]::UtcNow -lt $deadline)
ConvertTo-Json -InputObject @($after) -Depth 10 | Set-Content -LiteralPath $path
if (-not $script:LiveTestLastCimDiscovery.completed -or @($after).Count -ne 0) { throw "token processes remain" }
' >"$attempt_root/cleanup.log" 2>&1 || status=1
    if [[ -n $launcher_pid ]]; then
        local deadline=$((SECONDS+60))
        while kill -0 "$launcher_pid" 2>/dev/null && (( SECONDS < deadline )); do sleep 1; done
        if kill -0 "$launcher_pid" 2>/dev/null; then status=1; else wait "$launcher_pid" || status=1; fi
        jq -e --arg token "$token" '.runToken==$token and .state=="restored" and .exactOriginalRestored==true' "$attempt_root/crash-dialogs/engine-profile.json" >"$attempt_root/engine-restoration.log" 2>&1 || status=1
    fi
    if [[ ! -f $attempt_root/result-manifest.json ]] || ! jq -e '.accepted==true' "$attempt_root/result-manifest.json" >/dev/null; then status=1; fi
    python3 - "$attempt_root" "$head" "$tree" "$status" <<'PY'
from pathlib import Path
import json,hashlib,sys
root=Path(sys.argv[1]);status=int(sys.argv[4]);files=[{'path':str(p.relative_to(root)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in root.rglob('*') if p.is_file()]
actions=root/'result-manifest.json'
if not actions.exists() or not json.loads(actions.read_text(encoding='utf-8-sig')).get('accepted'):status=1
manifest={'head':sys.argv[2],'tree':sys.argv[3],'verdict':'passed' if status==0 else 'failed','exitCode':status,'files':files}
(root/'terminal-manifest.json').write_text(json.dumps(manifest,sort_keys=True)+'\n')
PY
    echo "STAGE7_LOCAL=$( [[ $status == 0 ]] && echo passed || echo failed ) result=$attempt_root/terminal-manifest.json"
    exit "$status"
}
trap cleanup EXIT
python3 - "$source_root" "$head" "$tree" <<'PYTHON'
import hashlib,json,sys
from pathlib import Path
root=Path(sys.argv[1]);pipeline=json.loads((root/'artifacts/IssueToPr/pipeline-state.json').read_text())
assert (pipeline['currentHead'],pipeline['currentTree'])==tuple(sys.argv[2:]),'pipeline source mismatch'
pins=pipeline['validation']['stage7Carrier']['helperPins']
assert pins,'required carrier helper pins absent'
for path,digest in pins.items():
    assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,f'helper changed: {path}'
PYTHON
/home/pwisorlowska/.codex/skills/issue-to-pr/scripts/source_snapshot.sh "$source_root" "$attempt_root/source.tar" >"$attempt_root/source-identity.txt"
mapfile -t identity <"$attempt_root/source-identity.txt"
[[ ${identity[0]} == "$head" && ${identity[1]} == "$tree" && ${identity[3]} == false ]]
python3 /home/pwisorlowska/.codex/skills/issue-to-pr/scripts/source_receipt.py --archive "$attempt_root/source.tar" --head "$head" --tree "$tree" --output "$attempt_root/source-receipt.json" >"$attempt_root/source-receipt.log"
cp "$carrier_root/1627-retreat-rejoin-health.ps1" "$attempt_root/scenario.ps1"
# Preserve the disposable bench before the launcher can build or inspect its cache.
env ROTATE_REPO="$live_root" /home/pwisorlowska/.codex/skills/rotate/scripts/rotate.sh --allow-large --committed "$source_root" >"$attempt_root/source-preparation.log" 2>&1
/home/pwisorlowska/.codex/skills/start-integration-test/scripts/start-integration-test.sh --repo-root "$live_root" --source-identity-root "$source_root" --expected-tree "$tree" --clients 2 --run-token "$token" --runtime-profile visual --no-focus --keep-alive --crash-artifact-dir "$attempt_root/crash-dialogs" >"$attempt_root/launcher.log" 2>&1 &
launcher_pid=$!
"$hidden" "$powershell" -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "$(wslpath -w "$attempt_root/scenario.ps1")" -ArtifactDirectory "$(wslpath -w "$attempt_root")" -RawCaptureRoot "$raw_capture_root" -RunToken "$token" -ExpectedHead "$head" -ExpectedTree "$tree" >"$attempt_root/actions.log" 2>&1
