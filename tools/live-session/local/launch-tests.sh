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
sed -n '/^for s in /,$p' "$here/local_up.sh" > "$tmp/start-body.sh"
cat > "$tmp/start-test.sh" <<'START'
#!/usr/bin/env bash
set -euo pipefail
out=$1; reused=$2; steps=start; server_save=proof; hidden=unused; powershell=unused
prepare=(unused); run_token=owned-run; head=abc; tree=def
wslpath() { printf '%s\n' "$2"; }
step() { if [[ $1 == dedicated-start ]]; then printf 'startedPid=123 reused=%s\n' "$reused" > "$out/dedicated-start.stdout.log"; fi; }
source "$out/start-body.sh"
START
bash "$tmp/start-test.sh" "$tmp" false > /dev/null
if bash "$tmp/start-test.sh" "$tmp" true > /dev/null 2>&1; then
    echo 'Named-save startup accepted a reused server' >&2; exit 1
fi
printf 'PASS named-save startup rejects reused server\n'
printf 'PASS manual/deferred launch arguments and PSModulePath isolation for initial and replacement clients\n'
