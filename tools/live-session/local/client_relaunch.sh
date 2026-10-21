#!/usr/bin/env bash
set -euo pipefail
unset PSModulePath
source "$(dirname "${BASH_SOURCE[0]}")/local_env.sh"
out=$1
platform_id=${2:-testclient2}
source "$out/identity.env"
[[ -f $out/runtime-profile.txt ]] || { echo 'Unknown recorded runtime profile' >&2; exit 2; }
profile=$(cat "$out/runtime-profile.txt")
[[ $profile == visual || $profile == functional ]] || { echo 'Unknown recorded runtime profile' >&2; exit 2; }
fixture_args=()
if [[ -f $out/fixture.env ]]; then source "$out/fixture.env"; fixture_args=(--debug-fixture-manifest "$fixture_manifest" --debug-fixture-manifest-sha256 "$fixture_sha" --debug-fixture-inputs-root "$fixture_inputs"); fi
exec "$integration" --repo-root "$live_root" --clients 1 --run-token "$run_token" --expected-tree "$tree" --reuse-verified-build --allow-owned-run \
  "${fixture_args[@]}" \
  --client-platform-id "$platform_id" --dedicated-server-inputs-root "$inputs" --expected-dedicated-server-tree "$dedicated_tree" \
  --expected-dedicated-server-ensure-script-sha256 "$ensure_sha" --expected-dedicated-server-run-windows-script-sha256 "$run_sha" \
  --runtime-profile "$profile" --crash-artifact-dir "$out/crash-dialogs-relaunch" --defer-client-autoconnect --keep-alive --no-focus --clients-only
