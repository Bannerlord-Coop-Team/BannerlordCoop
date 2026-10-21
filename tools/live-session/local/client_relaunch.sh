#!/usr/bin/env bash
set -euo pipefail
unset PSModulePath
out=$1
platform_id=${2:-testclient2}
source "$out/identity.env"
source "$out/fixture.env"
integration=/home/pwisorlowska/.codex/skills/start-integration-test/scripts/start-integration-test.sh
exec "$integration" --repo-root "$live_root" --clients 1 --run-token "$run_token" --expected-tree "$tree" --reuse-verified-build --allow-owned-run \
  --debug-fixture-manifest "$fixture_manifest" --debug-fixture-manifest-sha256 "$fixture_sha" --debug-fixture-inputs-root "$fixture_inputs" \
  --client-platform-id "$platform_id" --dedicated-server-inputs-root "$inputs" --expected-dedicated-server-tree "$dedicated_tree" \
  --expected-dedicated-server-ensure-script-sha256 "$ensure_sha" --expected-dedicated-server-run-windows-script-sha256 "$run_sha" \
  --runtime-profile visual --crash-artifact-dir "$out/crash-dialogs-relaunch" --defer-client-autoconnect --keep-alive --no-focus --clients-only
