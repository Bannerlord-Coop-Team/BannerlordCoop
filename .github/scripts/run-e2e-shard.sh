#!/usr/bin/env sh

set -eu

shard_index=${1:?missing shard index}
shard_count=${2:?missing shard count}
dotnet_cmd=${DOTNET:-dotnet}

case "$shard_index" in
    ''|*[!0-9]*)
        echo "shard index must be a non-negative integer: $shard_index" >&2
        exit 1
        ;;
esac
case "$shard_count" in
    ''|*[!0-9]*|0)
        echo "shard count must be a positive integer: $shard_count" >&2
        exit 1
        ;;
esac
if [ "$shard_index" -ge "$shard_count" ]; then
    echo "shard index must be between 0 and $((shard_count - 1)): $shard_index" >&2
    exit 1
fi

start_seconds=$(date +%s)
test_project="E2E.Tests/E2E.Tests.csproj"
# E2E tests generate AutoSync source files, so every shard must run in its own
# GitHub workspace. Do not run multiple shards concurrently from one checkout.
test_list=$(mktemp)
method_counts=$(mktemp)
assignments=$(mktemp)
trap 'rm -f "$test_list" "$method_counts" "$assignments"' 0 1 2 3 15

"$dotnet_cmd" test "$test_project" \
    -c Release \
    --no-build \
    --no-restore \
    --list-tests \
    --verbosity quiet 2>&1 \
    | awk '/^    E2E\.Tests\./ { sub(/^    /, ""); print }' > "$test_list"

if [ ! -s "$test_list" ]; then
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
    | sort -t "$(printf '\t')" -k2,2nr -k1,1 > "$method_counts"

awk -F "$(printf '\t')" -v shard_count="$shard_count" '
    BEGIN {
        for (i = 0; i < shard_count; i++)
            loads[i] = 0
    }
    {
        target = 0
        for (i = 1; i < shard_count; i++) {
            if (loads[i] < loads[target])
                target = i
        }
        if (filters[target] != "")
            filters[target] = filters[target] "|"
        filters[target] = filters[target] "FullyQualifiedName~" $1
        loads[target] += $2
    }
    END {
        for (i = 0; i < shard_count; i++)
            print i "\t" loads[i] "\t" filters[i]
    }' "$method_counts" > "$assignments"

assignment=$(awk -F "$(printf '\t')" -v shard_index="$shard_index" \
    '$1 == shard_index { print; found = 1 } END { if (!found) exit 1 }' "$assignments")
case "$assignment" in
    "")
        echo "shard $shard_index has no assigned E2E tests" >&2
        exit 1
        ;;
esac

assigned_case_count=$(printf '%s\n' "$assignment" | cut -f2)
shard_filter=$(printf '%s\n' "$assignment" | cut -f3-)
echo "running E2E shard $shard_index/$shard_count with $assigned_case_count discovered test cases"
"$dotnet_cmd" test "$test_project" \
    -c Release \
    --no-build \
    --no-restore \
    --filter "$shard_filter" \
    --consoleLoggerParameters:ErrorsOnly

elapsed_seconds=$(( $(date +%s) - start_seconds ))
echo "E2E shard $shard_index completed in ${elapsed_seconds}s"
