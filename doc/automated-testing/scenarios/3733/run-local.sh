#!/usr/bin/env bash
set -Eeuo pipefail
# Execute through the existing Local live lease producer after review/adoption.
source_root=$(git -C "$(dirname "$(readlink -f "$0")")" rev-parse --show-toplevel)
pipeline="$source_root/artifacts/IssueToPr/pipeline-state.json"
pins="$source_root/artifacts/IssueToPr/helper-pins.json"
skill=$(jq -r .skillScripts "$pins")
result=$(readlink -m "${1:?usage: run-local.sh FRESH_RESULT_DIRECTORY}")
[[ ! -e "$result" ]] || { echo 'Result directory already exists' >&2; exit 2; }
# Read-only admission precedes every bench/build/runtime operation.
python3 - "$skill" "$pipeline" "$pins" "$source_root" <<'PY'
import hashlib, json, os, pathlib, subprocess, sys
sys.path.insert(0, sys.argv[1])
import test_queue, source_receipt
p=json.load(open(sys.argv[2])); pins=json.load(open(sys.argv[3])); root=sys.argv[4]
receipt=source_receipt.validate(json.load(open(pathlib.Path(root)/'artifacts/IssueToPr/source-receipt.json')))
archive=pathlib.Path(root)/'artifacts/IssueToPr/source.tar'
assert receipt['source'] == source_receipt.identity(p['currentHead'],p['currentTree'],hashlib.sha256(archive.read_bytes()).hexdigest())
active=json.load(open(test_queue.global_lane_owner_path('local')))
assert os.environ.get('CODEX_THREAD_ID') == p['operatorThreadId']
assert active['owner'] == p['operatorThreadId'] and active['kind'] == 'Live test'
assert active['token'] == os.environ.get('ISSUE_TO_PR_TEST_RELEASE_TOKEN')
assert p['queueStrategy']['strategy'] == 'local-only' and p['currentStage'] == 7
assert active['source_head'] == p['currentHead'] and active['source_tree'] == p['currentTree']
for ref, expected in [('HEAD',p['currentHead']),('HEAD^{tree}',p['currentTree'])]:
 assert subprocess.check_output(['git','-C',root,'rev-parse',ref],text=True).strip() == expected
assert not subprocess.check_output(['git','-C',root,'status','--porcelain','--untracked-files=no'],text=True).strip()
for f in pins['files']:
 assert hashlib.sha256(pathlib.Path(f['path']).read_bytes()).hexdigest() == f['sha256'], f['path']
PY
head=$(jq -r .currentHead "$pipeline")
tree=$(jq -r .currentTree "$pipeline")
live_root=$(jq -r .local.liveRoot "$pins")
dedicated_root=$(jq -r .dedicated.root "$pins")
dedicated_head=$(jq -r .dedicated.head "$pins")
dedicated_tree=$(jq -r .dedicated.tree "$pins")
inputs=$(jq -r .local.inputsRoot "$pins")
integration=$(jq -r .local.integrationScript "$pins")
hidden=$(jq -r .local.hiddenWindows "$pins")
powershell=$(jq -r .local.powershell "$pins")
rotate=$(jq -r .local.rotateScript "$pins")
native_temp=$(jq -r .local.nativeTempRoot "$pins")
run_token="issue3733-$(date -u +%Y%m%dT%H%M%S)-${RANDOM}"
mkdir -p "$result"/{logs,metadata,cleanup}
stage=$(mktemp -d "$native_temp/$run_token.XXXXXX")
cp "$source_root/doc/automated-testing/scenarios/3733/stance-link-handles.ps1" "$stage/scenario.ps1"
cp "$skill/live_test_client.ps1" "$stage/live_test_client.ps1"
cp "$skill/remote_live_runtime.ps1" "$stage/remote_live_runtime.ps1"
cp "$pins" "$result/metadata/helper-pins.json"
cp "$pipeline" "$result/metadata/pipeline-state.json"
ps=("$hidden" "$powershell" -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "$(wslpath -w "$stage/scenario.ps1")"
    -RunToken "$run_token" -ArtifactDirectory "$(wslpath -w "$stage/result")"
    -LiveTestClientScript "$(wslpath -w "$stage/live_test_client.ps1")"
    -RuntimeScript "$(wslpath -w "$stage/remote_live_runtime.ps1")" -ExpectedHead "$head")
launcher_pid=
cleanup_rc=255
runtime_started=0
scenario_rc=255
failure_stage=preparation
record() { local name=$1; shift; "$@" >"$result/logs/$name.stdout.log" 2>"$result/logs/$name.stderr.log"; }
finish() {
    local original=$?
    trap - EXIT
    set +e
    if [[ $runtime_started -eq 1 ]]; then
        cp "$inputs/live-server.pid" "$result/metadata/dedicated-server-pid.json" 2>/dev/null
        record cleanup timeout 150s "${ps[@]}" -Mode Cleanup
        cleanup_rc=$?
        if [[ -n "$launcher_pid" ]]; then
            # The launcher exits after its owned clients stop; bound a broken keep-alive.
            timeout 30s tail --pid="$launcher_pid" -f /dev/null
            if kill -0 "$launcher_pid" 2>/dev/null; then kill "$launcher_pid"; fi
            wait "$launcher_pid"
        fi
    else cleanup_rc=0; fi
    python3 - "$stage/result" "$result/metadata/dedicated-server-pid.json" "$run_token" "$result/logs" <<'PY'
import json,pathlib,shutil,subprocess,sys
root=pathlib.Path(sys.argv[1]); pid=pathlib.Path(sys.argv[2]); token=sys.argv[3]; dest=pathlib.Path(sys.argv[4])
for path in root.glob('*status.json'):
 data=json.loads(path.read_text(encoding='utf-8-sig')); response=data.get('Response',data.get('response',data))
 log=(response.get('result') or {}).get('logPath')
 if log:
  src=pathlib.Path(subprocess.check_output(['wslpath','-u',log],text=True).strip())
  if src.is_file(): shutil.copy2(src,dest/(path.stem+'.log'))
if pid.is_file():
 data=json.loads(pid.read_text(encoding='utf-8-sig'))
 if data.get('runToken') == token:
  shutil.copy2(pid,dest/'dedicated-server-pid.json')
  for key in ['standardOutputPath','standardErrorPath']:
   if data.get(key):
    src=pathlib.Path(subprocess.check_output(['wslpath','-u',data[key]],text=True).strip())
    if src.is_file(): shutil.copy2(src,dest/(key+'.log'))
PY
    retention_rc=$?
    # Preserve raw originals left by a failed retention copy as well as the result.
    cp -a "$stage" "$result/windows-local"
    copy_rc=$?
    if [[ $retention_rc -eq 0 && $copy_rc -eq 0 ]]; then
        diff -qr "$stage" "$result/windows-local" >"$result/metadata/retention.diff"
        copy_rc=$?
        if [[ $copy_rc -eq 0 ]]; then rm -rf -- "$stage"; fi
    fi
    python3 - "$result" "$head" "$tree" "$run_token" "$original" "$cleanup_rc" "$retention_rc" "$copy_rc" "$scenario_rc" "$failure_stage" <<'PY'
import hashlib,json,pathlib,sys
root=pathlib.Path(sys.argv[1]); scenario=root/'windows-local/result/scenario-result.json'
s=json.loads(scenario.read_text(encoding='utf-8-sig')) if scenario.is_file() else {}
passed=all(x=='0' for x in sys.argv[5:10]) and s.get('outcome')=='passed'
images=s.get('screenshots',[])
for image in images: image['path']='windows-local/result/'+image['path']
files=[{'path':str(p.relative_to(root)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(root.rglob('*')) if p.is_file()]
(root/'terminal-result-manifest.json').write_text(json.dumps({
 'outcome':'passed' if passed else 'failed','sourceHead':sys.argv[2],'sourceTree':sys.argv[3],'runToken':sys.argv[4],
 'originalExitCode':int(sys.argv[5]),'cleanupExitCode':int(sys.argv[6]),'retentionExitCode':int(sys.argv[7]),'copyExitCode':int(sys.argv[8]),
 'scenarioExitCode':int(sys.argv[9]),'failureStage':sys.argv[10],'nativeStorageRetained':sys.argv[8]!='0','screenshots':images,'files':files},indent=2)+'\n')
PY
    manifest_rc=$?
    if [[ $original -eq 0 && $cleanup_rc -eq 0 && $retention_rc -eq 0 && $copy_rc -eq 0 && $manifest_rc -eq 0 ]]; then exit 0; else exit 1; fi
}
trap finish EXIT
# Committed rotation safety-stashes the shared bench inside this exact lease.
record rotation "$rotate" --committed --allow-large "$source_root"
failure_stage=build
record build "$integration" --repo-root "$live_root" --clients 2 --expected-tree "$tree" --source-identity-root "$source_root" --clients-only --build-only
ensure_sha=$(jq -r .dedicated.ensureScriptSha256 "$pins")
run_sha=$(jq -r .dedicated.runWindowsScriptSha256 "$pins")
prepare=("$skill/prepare_local_dedicated_server.sh" --repo-root "$live_root" --source-identity-root "$source_root"
    --expected-coop-head "$head" --expected-coop-tree "$tree" --dedicated-server-root "$dedicated_root"
    --dedicated-server-inputs-root "$inputs" --dedicated-server-branch main --expected-dedicated-server-head "$dedicated_head"
    --expected-dedicated-server-tree "$dedicated_tree" --run-token "$run_token")
failure_stage=dedicated-prepare
record dedicated-prepare "${prepare[@]}"
record build-cache "$integration" --repo-root "$live_root" --clients 2 --expected-tree "$tree" --clients-only --record-prepared-build \
    --dedicated-server-inputs-root "$inputs" --expected-dedicated-server-tree "$dedicated_tree" \
    --expected-dedicated-server-ensure-script-sha256 "$ensure_sha" --expected-dedicated-server-run-windows-script-sha256 "$run_sha"
failure_stage=dedicated-start
runtime_started=1
record dedicated-start "${prepare[@]}" --start
failure_stage=clients
"$integration" --repo-root "$live_root" --clients 2 --run-token "$run_token" --expected-tree "$tree" --reuse-verified-build \
    --runtime-profile visual --crash-artifact-dir "$result/crash-dialogs" --keep-alive --no-focus --clients-only \
    >"$result/logs/launcher.stdout.log" 2>"$result/logs/launcher.stderr.log" &
launcher_pid=$!
failure_stage=scenario
set +e
record scenario timeout 1200s "${ps[@]}" -Mode Scenario
scenario_rc=$?
set -e
[[ $scenario_rc -eq 0 ]] || exit "$scenario_rc"
failure_stage=none
