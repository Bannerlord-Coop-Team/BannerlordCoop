#!/usr/bin/env bash
# Launch the two no-focus Bannerlord clients against the already-started dedicated server.
# Runs in the foreground with --keep-alive; start it as a background task.
# usage: clients_up.sh <out-dir-with-identity.env> [visual|functional]
set -euo pipefail
unset PSModulePath
source "$(dirname "${BASH_SOURCE[0]}")/local_env.sh"
out=$1; profile=${2:-visual}
[[ $profile == visual || $profile == functional ]] || { echo 'Unknown runtime profile' >&2; exit 2; }
printf '%s\n' "$profile" > "$out/runtime-profile.txt"
source "$out/identity.env"
mkdir -p "$out/crash-dialogs"
fixture_args=()
if [[ -f $out/fixture.env ]]; then source "$out/fixture.env"; fixture_args=(--debug-fixture-manifest "$fixture_manifest" --debug-fixture-manifest-sha256 "$fixture_sha" --debug-fixture-inputs-root "$fixture_inputs"); fi
exec "$integration" --repo-root "$live_root" --clients 2 --run-token "$run_token" --expected-tree "$tree" --reuse-verified-build "${fixture_args[@]}" \
  --client-platform-id testclient --dedicated-server-inputs-root "$inputs" --expected-dedicated-server-tree "$dedicated_tree" \
  --expected-dedicated-server-ensure-script-sha256 "$ensure_sha" --expected-dedicated-server-run-windows-script-sha256 "$run_sha" \
  --runtime-profile "$profile" --crash-artifact-dir "$out/crash-dialogs" --defer-client-autoconnect --keep-alive --no-focus --clients-only
