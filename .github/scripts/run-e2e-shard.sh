#!/usr/bin/env bash

set -euo pipefail

shard_index=${1:?missing shard index}
shard_count=${2:?missing shard count}
dotnet_cmd=${DOTNET:-dotnet}

if ((shard_index < 0 || shard_index >= shard_count)); then
    echo "shard index must be between 0 and $((shard_count - 1)): $shard_index" >&2
    exit 1
fi

start_seconds=$SECONDS
test_project="E2E.Tests/E2E.Tests.csproj"
# E2E tests generate AutoSync source files, so every shard must run in its own
# GitHub workspace. Do not run multiple shards concurrently from one checkout.
test_list=$(mktemp)
method_counts=$(mktemp)
trap 'rm -f "$test_list" "$method_counts"' EXIT

"$dotnet_cmd" test "$test_project" \
    -c Release \
    --no-build \
    --no-restore \
    --list-tests \
    --verbosity quiet 2>&1 \
    | awk '/^    E2E\.Tests\./ { sub(/^    /, ""); print }' > "$test_list"

if [[ ! -s "$test_list" ]]; then
    echo "test discovery returned no E2E tests" >&2
    exit 1
fi

awk 'BEGIN { invalid = 0 }
    {
        sub(/\r$/, "")
        field_count = split($0, fields, "[.]")
        method_name = ""
        for (i = 3; i < field_count; i++) {
            if (fields[i] ~ /Tests?$/ && fields[i + 1] ~ /^[A-Za-z_]/) {
                for (j = 1; j <= i + 1; j++)
                    method_name = method_name (j == 1 ? "" : ".") fields[j]
                break
            }
        }
        if (method_name == "") {
            print "unable to determine test method: " $0 > "/dev/stderr"
            invalid = 1
        } else {
            sub(/\(.*/, "", method_name)
            counts[method_name]++
        }
    }
    END {
        if (invalid) exit 1
        for (method_name in counts) print method_name "\t" counts[method_name]
    }' \
    "$test_list" \
    | sort -t $'\t' -k2,2nr -k1,1 > "$method_counts"

declare -a shard_loads
declare -a shard_filters
for ((i = 0; i < shard_count; i++)); do
    shard_loads[i]=0
    shard_filters[i]=""
done

while IFS=$'\t' read -r method_name test_count; do
    target_shard=0
    for ((i = 1; i < shard_count; i++)); do
        if ((shard_loads[i] < shard_loads[target_shard])); then
            target_shard=$i
        fi
    done

    if [[ -n "${shard_filters[target_shard]}" ]]; then
        shard_filters[target_shard]+="|"
    fi
    shard_filters[target_shard]+="FullyQualifiedName~$method_name"
    shard_loads[target_shard]=$((shard_loads[target_shard] + test_count))
done < "$method_counts"

if [[ -z "${shard_filters[shard_index]}" ]]; then
    echo "shard $shard_index has no assigned E2E tests" >&2
    exit 1
fi

echo "running E2E shard $shard_index/$shard_count with ${shard_loads[shard_index]} discovered test cases"
"$dotnet_cmd" test "$test_project" \
    -c Release \
    --no-build \
    --no-restore \
    --filter "${shard_filters[shard_index]}" \
    --consoleLoggerParameters:ErrorsOnly

elapsed_seconds=$((SECONDS - start_seconds))
echo "E2E shard $shard_index completed in ${elapsed_seconds}s"
