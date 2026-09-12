#!/usr/bin/env bash

set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
docker_image=${E2E_IMAGE:-garrettluskey/bannerlordcoop:latest}
shard_count=${E2E_SHARDS:-8}
run_id="bannerlordcoop-e2e-$(date +%s)-$$"
seed_container="${run_id}-seed"
local_image="${run_id}:workspace"
status_dir=$(mktemp -d)
container_names=()

cleanup() {
    for container_name in "${container_names[@]}"; do
        docker rm --force "$container_name" >/dev/null 2>&1 || true
    done
    docker rm --force "$seed_container" >/dev/null 2>&1 || true
    docker image rm "$local_image" >/dev/null 2>&1 || true
    rm -rf "$status_dir"
}
trap cleanup EXIT INT TERM

case "$shard_count" in
    ''|*[!0-9]*|0)
        echo "E2E_SHARDS must be a positive integer: $shard_count" >&2
        exit 1
        ;;
esac

if ! command -v docker >/dev/null 2>&1; then
    echo "docker is required" >&2
    exit 1
fi
if ! docker image inspect "$docker_image" >/dev/null 2>&1; then
    echo "Docker image $docker_image is not available locally" >&2
    echo "Pull it first with: docker pull $docker_image" >&2
    exit 1
fi

echo "preparing one Docker test workspace from $docker_image"
docker create --name "$seed_container" "$docker_image" /bin/sh -c 'sleep infinity' >/dev/null
docker start "$seed_container" >/dev/null
docker exec "$seed_container" /bin/sh -c 'mkdir -p /workspace'
docker cp "$repo_root/source" "$seed_container:/workspace/source"
docker cp "$repo_root/deploy" "$seed_container:/workspace/deploy"
docker cp "$repo_root/.github/scripts/run-e2e-shard.sh" "$seed_container:/workspace/run-e2e-shard.sh"
docker exec "$seed_container" /bin/sh -eu -c '
    find /workspace/source -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
    ln -s /home/mb2 /workspace/mb2
    dotnet build /workspace/source/CoopTests.slnf -c Release
'
docker commit "$seed_container" "$local_image" >/dev/null
docker rm --force "$seed_container" >/dev/null

echo "running $shard_count Docker E2E shards"
for ((shard_index = 0; shard_index < shard_count; shard_index++)); do
    container_name="${run_id}-${shard_index}"
    container_names+=("$container_name")
    docker run --rm --name "$container_name" "$local_image" /bin/sh -eu -c \
        "cd /workspace/source && sh /workspace/run-e2e-shard.sh $shard_index $shard_count" \
        >"$status_dir/$shard_index.log" 2>&1 &
    echo "$!" > "$status_dir/$shard_index.pid"
done

failed=0
for ((shard_index = 0; shard_index < shard_count; shard_index++)); do
    if ! wait "$(<"$status_dir/$shard_index.pid")"; then
        failed=1
    fi
    sed "s/^/[e2e $shard_index] /" "$status_dir/$shard_index.log"
done

if ((failed)); then
    echo "one or more local Docker E2E shards failed" >&2
    exit 1
fi

echo "all local Docker E2E shards passed"
