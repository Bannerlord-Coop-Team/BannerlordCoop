#!/usr/bin/env bash
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
cat > "$tmp/capture.sh" <<'CAPTURE'
#!/usr/bin/env bash
printf '%s\n' "$@"
[[ ! -v PSModulePath ]]
CAPTURE
chmod +x "$tmp/capture.sh"
cat > "$tmp/identity.env" <<'IDENTITY'
live_root=/test
run_token=owned-run
tree=abcdef
inputs=/inputs
dedicated_tree=abc
ensure_sha=def
run_sha=ghi
IDENTITY
cat > "$tmp/fixture.env" <<'FIXTURE'
fixture_manifest=/manifest
fixture_sha=abc
fixture_inputs=/fixtures
FIXTURE
for name in clients_up.sh client_relaunch.sh; do
    sed "s|^integration=.*|integration=$tmp/capture.sh|" "$here/$name" > "$tmp/$name"
    PSModulePath=wrong bash "$tmp/$name" "$tmp" > "$tmp/arguments"
    grep -Fxq -- '--defer-client-autoconnect' "$tmp/arguments"
    grep -Fxq -- 'owned-run' "$tmp/arguments"
done
printf 'PASS manual/deferred launch arguments and PSModulePath isolation for initial and replacement clients\n'