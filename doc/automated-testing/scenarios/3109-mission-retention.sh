#!/usr/bin/env bash
# Called once through live_test_lease.sh; all bench changes occur under its lease.
set -Eeuo pipefail
source_root=$(git rev-parse --show-toplevel)
carrier_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
attempt_root=${ISSUE3109_ATTEMPT_ROOT:?fresh attempt directory required}
token=${ISSUE3109_RUN_TOKEN:?fresh token required}
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
    "$hidden" --env "ISSUE3109_CLEANUP_TOKEN=$token" --env "ISSUE3109_CLEANUP_PATH=$(wslpath -w "$attempt_root/process-cleanup.json")" "$powershell" -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command '
$token=$env:ISSUE3109_CLEANUP_TOKEN
$path=$env:ISSUE3109_CLEANUP_PATH
$ErrorActionPreference="Stop"
. "\\wsl.localhost\Ubuntu\home\pwisorlowska\.codex\skills\issue-to-pr\scripts\live_test_client.ps1"
. "\\wsl.localhost\Ubuntu\home\pwisorlowska\.codex\skills\issue-to-pr\scripts\remote_live_runtime.ps1"
$pidReceiptPath="\\wsl.localhost\Ubuntu\home\pwisorlowska\.codex\runtime\issue-to-pr\local-dedicated-server\live-server.pid"
$pidReceipt=$null
if(Test-Path -LiteralPath $pidReceiptPath){$candidate=Get-Content -LiteralPath $pidReceiptPath -Raw | ConvertFrom-Json;if($candidate.runToken -ceq $token){$pidReceipt=$candidate}}
$receipt=Stop-RemoteLiveRunScopedProcessesVerified -RunToken $token -TimeoutSeconds 30
ConvertTo-Json -InputObject $receipt -Depth 64 | Set-Content -LiteralPath $path
if($null -ne $pidReceipt){
    $root=Split-Path -Parent $path
    $pidReceipt | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $root "dedicated-server-pid-receipt.json")
    Copy-Item -LiteralPath $pidReceipt.standardOutputPath -Destination (Join-Path $root "dedicated-server.stdout.log") -ErrorAction Stop
    Copy-Item -LiteralPath $pidReceipt.standardErrorPath -Destination (Join-Path $root "dedicated-server.stderr.log") -ErrorAction Stop
}
$ports=@(Get-NetUDPEndpoint -LocalPort 4200 -ErrorAction SilentlyContinue)
$ports | Select-Object LocalAddress,LocalPort,OwningProcess | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path (Split-Path -Parent $path) "udp-cleanup.json")
if ($ports.Count -ne 0) { throw "UDP4200 remains occupied" }
if (-not $receipt.verifiedAbsent) { throw "token process tree remains" }
' >"$attempt_root/cleanup.log" 2>&1 || status=1
    if [[ -n $launcher_pid ]]; then
        local deadline=$((SECONDS+60))
        while kill -0 "$launcher_pid" 2>/dev/null && (( SECONDS < deadline )); do sleep 1; done
        if kill -0 "$launcher_pid" 2>/dev/null; then status=1; else wait "$launcher_pid" || status=1; fi
        jq -e --arg token "$token" ' .runToken==$token and .exitCode==0 and .crashDialogs==0 and .crashCloseFailures==0 ' "$attempt_root/crash-dialogs/launcher-crash-verdict.json" >"$attempt_root/launcher-verdict-check.log" 2>&1 || status=1
        jq -e --arg token "$token" '.runToken==$token and .state=="restored" and .exactOriginalRestored==true' "$attempt_root/crash-dialogs/engine-profile.json" >"$attempt_root/engine-restoration.log" 2>&1 || status=1
    fi
    if [[ ! -f $attempt_root/result-manifest.json ]] || ! jq -e '.accepted==true' "$attempt_root/result-manifest.json" >/dev/null; then status=1; fi
    python3 - "$attempt_root" "$head" "$tree" "$status" <<'PY'
from pathlib import Path
import json,hashlib,sys
root=Path(sys.argv[1]);status=int(sys.argv[4]);files=[{'path':str(p.relative_to(root)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in root.rglob('*') if p.is_file()]
actions=root/'result-manifest.json'
if not actions.exists() or not json.loads(actions.read_text(encoding='utf-8-sig')).get('accepted'):status=1
manifest={'source':{'head':sys.argv[2],'tree':sys.argv[3]},'status':'passed' if status==0 else 'failed','head':sys.argv[2],'tree':sys.argv[3],'verdict':'passed' if status==0 else 'failed','exitCode':status,'files':files}
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
cp "$carrier_root/3109-mission-retention.ps1" "$attempt_root/scenario.ps1"
# Preserve the disposable bench before the launcher can build or inspect its cache.
env ROTATE_REPO="$live_root" /home/pwisorlowska/.codex/skills/rotate/scripts/rotate.sh --allow-large --committed "$source_root" >"$attempt_root/source-preparation.log" 2>&1
integration=/home/pwisorlowska/.codex/skills/start-integration-test/scripts/start-integration-test.sh
helper=/home/pwisorlowska/.codex/skills/issue-to-pr/scripts
dedicated_source=/home/pwisorlowska/codex-projects/BannerlordCoop.DedicatedServer
mapfile -t dedicated < <(python3 - "$source_root/artifacts/IssueToPr/pipeline-state.json" <<'PYD'
import json,sys
p=json.load(open(sys.argv[1]))['validation']['stage7Carrier']['dedicatedServer']
for key in ('head','tree','ensureSha256','runWindowsSha256','branch'):print(p[key])
PYD
)
server_args=(--dedicated-server-inputs-root /home/pwisorlowska/.codex/runtime/issue-to-pr/local-dedicated-server --dedicated-server-branch "${dedicated[4]}" --expected-dedicated-server-head "${dedicated[0]}" --expected-dedicated-server-tree "${dedicated[1]}")
prepare=("$helper/prepare_local_dedicated_server.sh" --repo-root "$live_root" --source-identity-root "$source_root" --expected-coop-head "$head" --expected-coop-tree "$tree" --dedicated-server-root "$dedicated_source" --run-token "$token" "${server_args[@]}")
"$integration" --repo-root "$live_root" --source-identity-root "$source_root" --expected-tree "$tree" --clients 2 --clients-only --build-only >"$attempt_root/build.log" 2>&1
"${prepare[@]}" >"$attempt_root/server-prepare.log" 2>&1
"$integration" --repo-root "$live_root" --expected-tree "$tree" --clients 2 --clients-only --record-prepared-build --dedicated-server-inputs-root /home/pwisorlowska/.codex/runtime/issue-to-pr/local-dedicated-server --expected-dedicated-server-tree "${dedicated[1]}" --expected-dedicated-server-ensure-script-sha256 "${dedicated[2]}" --expected-dedicated-server-run-windows-script-sha256 "${dedicated[3]}" >"$attempt_root/prepared-build.log" 2>&1
"${prepare[@]}" --start >"$attempt_root/server-start.log" 2>&1
"$integration" --repo-root "$live_root" --expected-tree "$tree" --clients 2 --run-token "$token" --runtime-profile visual --no-focus --keep-alive --clients-only --reuse-verified-build --crash-artifact-dir "$attempt_root/crash-dialogs" >"$attempt_root/launcher.log" 2>&1 &
launcher_pid=$!
scenario_marker="$attempt_root/scenario-started.marker"
CODEX_HIDDEN_WINDOWS_RETRY_PRE_CHILD_MARKER="$scenario_marker" "$hidden" --env "CODEX_HIDDEN_WINDOWS_CHILD_MARKER=$(wslpath -w "$scenario_marker")" "$powershell" -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "$(wslpath -w "$attempt_root/scenario.ps1")" -ArtifactDirectory "$(wslpath -w "$attempt_root")" -RawCaptureRoot "$raw_capture_root" -RunToken "$token" -ExpectedHead "$head" -ExpectedTree "$tree" >"$attempt_root/actions.log" 2>&1
