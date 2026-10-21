#!/usr/bin/env bash
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
sed -n '/^cleanup_failed_cycle()/,/^}/p' "$here/cycle.sh" > "$tmp/cleanup.sh"
cat > "$tmp/bench_lock.sh" <<'LOCK'
printf 'released\n' >> "$1.receipt"
LOCK
cat > "$tmp/local_down.sh" <<'DOWN'
exit 1
DOWN
touch "$tmp/identity.env"
if (cd "$tmp"; bash -c 'source "$1/cleanup.sh"; here=$1; out=$1; issue=4013; trap cleanup_failed_cycle EXIT; exit 42' bash "$tmp") > "$tmp/result" 2>&1; then
  echo 'Failed cycle lost its failure status' >&2; exit 1
fi
grep -q 'bench lock retained' "$tmp/result"
[[ ! -e "$tmp/release.receipt" ]]
rm "$tmp/identity.env"
(cd "$tmp"; bash -c 'source "$1/cleanup.sh"; here=$1; out=$1; issue=4013; trap cleanup_failed_cycle EXIT; exit 42' bash "$tmp") || rc=$?
[[ ${rc:-0} -eq 42 && -f "$tmp/release.receipt" ]]
echo 'PASS failed cleanup retains bench lock; failure before launch releases it'
