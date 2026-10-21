#!/usr/bin/env bash
# Bring up the Local lane for one issue worktree: rotate bench -> DebugAutoConnect build ->
# prepare/start the standalone dedicated server -> readiness. Clients are launched separately.
# usage: local_up.sh <source-worktree> <base-head> <run-token> <out-dir> [steps...]
set -euo pipefail
export HOME=/home/pwisorlowska
# Windows PowerShell 5.1 must build its own module path; one inherited from pwsh 7 hides Get-FileHash and friends.
unset PSModulePath
source_root=$1; base_head=$2; run_token=$3; out=$4; shift 4
steps=${*:-rotate build prepare record start readiness}
server_save=${LOCAL_SERVER_SAVE:-}
skill=/home/pwisorlowska/.codex/skills/issue-to-pr/scripts
integration=/home/pwisorlowska/.codex/skills/start-integration-test/scripts/start-integration-test.sh
rotate=/home/pwisorlowska/.codex/skills/rotate/scripts/rotate.sh
hidden=/home/pwisorlowska/.codex/runtime/hidden-windows-exec/hidden_windows_exec.sh
powershell=/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe
live_root=/mnt/c/Users/Andrew/.codex-runtime/live-testing/BannerlordCoop
dedicated_root=/mnt/c/Users/Andrew/.codex-runtime/live-testing/BannerlordCoop.DedicatedServer
inputs=/home/pwisorlowska/.codex/runtime/issue-to-pr/local-dedicated-server
dedicated_head=$(/usr/bin/git -C "$dedicated_root" rev-parse 'refs/remotes/origin/main^{commit}')
dedicated_tree=$(/usr/bin/git -C "$dedicated_root" rev-parse "$dedicated_head^{tree}")
ensure_sha=$(/usr/bin/git -C "$dedicated_root" cat-file --filters --path=scripts/ensure-local-live-server.ps1 "$dedicated_head:scripts/ensure-local-live-server.ps1" | sha256sum | cut -d' ' -f1)
run_sha=$(/usr/bin/git -C "$dedicated_root" cat-file --filters --path=windows/run-windows.ps1 "$dedicated_head:windows/run-windows.ps1" | sha256sum | cut -d' ' -f1)
head=$(/usr/bin/git -C "$source_root" rev-parse 'HEAD^{commit}')
tree=$(/usr/bin/git -C "$source_root" rev-parse "$head^{tree}")
# Owners often pass an abbreviated base; compare full SHAs so a base equal to HEAD still gets --allow-no-changes
# (2026-10-19: 7fbe1aa6f vs the full HEAD sha stopped rotate with "no changes relative to").
base_head=$(/usr/bin/git -C "$source_root" rev-parse "$base_head^{commit}")
mkdir -p "$out"
fixture_args=()
if [[ -f $out/fixture.env ]]; then source "$out/fixture.env"; fixture_args=(--debug-fixture-manifest "$fixture_manifest" --debug-fixture-manifest-sha256 "$fixture_sha" --debug-fixture-inputs-root "$fixture_inputs"); fi
cat >"$out/identity.env" <<EOF
source_root=$source_root
head=$head
tree=$tree
base_head=$base_head
run_token=$run_token
dedicated_head=$dedicated_head
dedicated_tree=$dedicated_tree
ensure_sha=$ensure_sha
run_sha=$run_sha
inputs=$inputs
live_root=$live_root
EOF
step() { local name=$1; shift; echo "== $name $(date -u +%H:%M:%S)"; if "$@" >"$out/$name.stdout.log" 2>"$out/$name.stderr.log"; then echo "   ok"; else local rc=$?; echo "   FAILED rc=$rc"; tail -20 "$out/$name.stderr.log"; tail -20 "$out/$name.stdout.log"; exit $rc; fi; }
prepare=("$skill/prepare_local_dedicated_server.sh" --repo-root "$live_root" --source-identity-root "$source_root"
  --expected-coop-head "$head" --expected-coop-tree "$tree" --dedicated-server-root "$dedicated_root"
  --dedicated-server-inputs-root "$inputs" --dedicated-server-branch main --expected-dedicated-server-head "$dedicated_head"
  --expected-dedicated-server-tree "$dedicated_tree" --run-token "$run_token")
for s in $steps; do case $s in
  rotate) rargs=(--committed --parent "$base_head"); [[ $head != "$base_head" ]] || rargs+=(--allow-no-changes)
          step rotate env ROTATE_REPO="$live_root" "$rotate" --allow-large "${rargs[@]}" "$source_root" ;;
  build) step build "$integration" --repo-root "$live_root" --clients 2 --expected-tree "$tree" --source-identity-root "$source_root" --clients-only --build-only "${fixture_args[@]}" ;;
  prepare) step dedicated-prepare "${prepare[@]}" ;;
  record) step build-cache "$integration" --repo-root "$live_root" --clients 2 --expected-tree "$tree" --clients-only --record-prepared-build "${fixture_args[@]}" \
            --dedicated-server-inputs-root "$inputs" --expected-dedicated-server-tree "$dedicated_tree" \
            --expected-dedicated-server-ensure-script-sha256 "$ensure_sha" --expected-dedicated-server-run-windows-script-sha256 "$run_sha" ;;
  start) if [[ -n "$server_save" ]]; then
            step select-save "$hidden" "$powershell" -NoProfile -NonInteractive -WindowStyle Hidden -File "$(wslpath -w "$(dirname "$0")/select_server_save.ps1")" -PreparationLog "$(wslpath -w "$out/dedicated-prepare.stdout.log")" -SaveName "$server_save"
          fi
          step dedicated-start "${prepare[@]}" --start ;;
  readiness) step dedicated-readiness timeout 660s "$hidden" "$powershell" -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass \
            -File "$(wslpath -w "$skill/verify_local_dedicated_server_readiness.ps1")" \
            -DedicatedServerRoot "$(wslpath -w "$dedicated_root")" -InputsRoot "$(wslpath -w "$inputs")" \
            -ExpectedRunToken "$run_token" -ExpectedServerHead "$dedicated_head" -ExpectedServerTree "$dedicated_tree" \
            -ExpectedCoopHead "$head" -ExpectedCoopTree "$tree" -ExpectedEnsureScriptSha256 "$ensure_sha" \
            -ExpectedRunWindowsScriptSha256 "$run_sha" -PreparationStartedUtc "$(date -u +%Y-%m-%dT%H:%M:%SZ -d '-30 min')" \
            -ArtifactPath "$(wslpath -w "$out/dedicated-readiness.json")" -TimeoutSeconds 600 ;;
  *) echo "unknown step $s"; exit 2 ;;
esac; done
echo "LOCAL_UP_DONE token=$run_token head=$head tree=$tree"
