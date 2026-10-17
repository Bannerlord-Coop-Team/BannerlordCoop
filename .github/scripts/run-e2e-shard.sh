#!/usr/bin/env sh

set -eu

shard_index=${1:?missing shard index}
shard_count=${2:?missing shard count}
dotnet_cmd=${DOTNET:-dotnet}
script_dir=$(cd "$(dirname "$0")" && pwd)
repo_root=$(cd "$script_dir/../.." && pwd)

# Runs the built DLL directly, so the shard needs no restore or project evaluation.
test_dll=${E2E_TEST_DLL:-$repo_root/source/E2E.Tests/bin/Release/net6.0/E2E.Tests.dll}
# The project's RunSettingsFilePath only applies to project runs, so DLL runs pass it explicitly.
run_settings=${E2E_RUN_SETTINGS:-$repo_root/source/E2E.Tests/e2e.runsettings}
# Average seconds per test case for each test class, refreshed from a green run's shard logs with
# `gh run view <run-id> --log > e2e.log && sh .github/scripts/refresh-e2e-durations.sh e2e.log > .github/e2e-test-durations.tsv`.
durations=${E2E_DURATIONS:-$repo_root/.github/e2e-test-durations.tsv}
# Classes missing from the table (new tests) are estimated at this many seconds per test case.
default_seconds=${E2E_DEFAULT_TEST_SECONDS:-1.0}

# Collect more aggressively without capping the live heap required by the fixtures.
export DOTNET_GCConserveMemory=${DOTNET_GCConserveMemory:-9}
export COOP_DISABLE_SEQ=${COOP_DISABLE_SEQ:-1}

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
for required in "$test_dll" "$run_settings" "$durations"; do
    if [ ! -f "$required" ]; then
        echo "missing file: $required" >&2
        exit 1
    fi
done

start_seconds=$(date +%s)
# E2E tests generate AutoSync source files, so every shard must run in its own
# GitHub workspace. Do not run multiple shards concurrently from one checkout.
test_list=$(mktemp)
class_estimates=$(mktemp)
assignments=$(mktemp)
trap 'rm -f "$test_list" "$class_estimates" "$assignments"' 0 1 2 3 15

"$dotnet_cmd" test "$test_dll" \
    --list-tests \
    --verbosity quiet 2>&1 \
    | awk '/^    E2E\.Tests\./ { sub(/^    /, ""); sub(/\r$/, ""); print }' > "$test_list"

if [ ! -s "$test_list" ]; then
    echo "test discovery returned no E2E tests" >&2
    exit 1
fi

# Estimates each discovered class as its case count times its table average, longest first.
awk -F "$(printf '\t')" -v default_seconds="$default_seconds" '
    FNR == NR {
        sub(/\r$/, "")
        if ($0 !~ /^#/ && NF >= 2) seconds_per_test[$1] = $2
        next
    }
    {
        test_name = $0
        sub(/\(.*/, "", test_name)
        class_name = test_name
        if (!sub(/\.[A-Za-z_][A-Za-z0-9_]*$/, "", class_name) || class_name !~ /^E2E\.Tests\./) {
            print "unable to determine test class: " $0 > "/dev/stderr"
            invalid = 1
            next
        }
        counts[class_name]++
    }
    END {
        if (invalid) exit 1
        for (class_name in counts) {
            known = class_name in seconds_per_test
            estimate = counts[class_name] * (known ? seconds_per_test[class_name] : default_seconds)
            printf "%.3f\t%d\t%s\t%s\n", estimate, counts[class_name], (known ? "timed" : "default"), class_name
        }
    }' "$durations" "$test_list" \
    | sort -t "$(printf '\t')" -k1,1nr -k4,4 > "$class_estimates"

# Greedy longest-first: each class goes to the shard with the least estimated time so far.
awk -F "$(printf '\t')" -v shard_count="$shard_count" '
    BEGIN {
        for (i = 0; i < shard_count; i++) {
            loads[i] = 0
            cases[i] = 0
        }
    }
    {
        target = 0
        for (i = 1; i < shard_count; i++) {
            if (loads[i] < loads[target])
                target = i
        }
        # The trailing dot keeps one class filter from also matching a class whose name it prefixes.
        if (filters[target] != "")
            filters[target] = filters[target] "|"
        filters[target] = filters[target] "FullyQualifiedName~" $4 "."
        loads[target] += $1
        cases[target] += $2
    }
    END {
        for (i = 0; i < shard_count; i++)
            printf "%d\t%.0f\t%d\t%s\n", i, loads[i], cases[i], filters[i]
    }' "$class_estimates" > "$assignments"

# Every discovered case must match exactly one class filter across all shards.
awk -F "$(printf '\t')" '
    FNR == NR {
        filter = $4
        gsub(/FullyQualifiedName~/, "", filter)
        class_count = split(filter, parts, "|")
        for (i = 1; i <= class_count; i++)
            if (parts[i] != "") keys[parts[i]] = 1
        next
    }
    {
        test_name = $0
        sub(/\(.*/, "", test_name)
        matches = 0
        for (key in keys)
            if (index(test_name, key) > 0) matches++
        if (matches != 1) {
            print "test matches " matches " shard filters: " $0 > "/dev/stderr"
            invalid = 1
        }
    }
    END { if (invalid) exit 1 }' "$assignments" "$test_list"

timed_classes=$(awk -F "$(printf '\t')" '$3 == "timed"' "$class_estimates" | wc -l | tr -d ' ')
default_classes=$(awk -F "$(printf '\t')" '$3 == "default"' "$class_estimates" | wc -l | tr -d ' ')
echo "estimated E2E shard plan ($timed_classes timed classes, $default_classes at the ${default_seconds}s default):"
awk -F "$(printf '\t')" '{ printf "  shard %d: ~%ss, %d test cases\n", $1, $2, $3 }' "$assignments"
if [ "$default_classes" -gt 0 ]; then
    echo "classes missing from $(basename "$durations"):"
    awk -F "$(printf '\t')" '$3 == "default" { print "  " $4 }' "$class_estimates"
fi

assignment=$(awk -F "$(printf '\t')" -v shard_index="$shard_index" \
    '$1 == shard_index { print; found = 1 } END { if (!found) exit 1 }' "$assignments")
shard_filter=$(printf '%s\n' "$assignment" | cut -f4-)
if [ -z "$shard_filter" ]; then
    echo "shard $shard_index has no assigned E2E tests" >&2
    exit 1
fi

# Runner hardware varies, so a shard far off its estimate may just be on a slower CPU.
if [ -r /proc/cpuinfo ]; then
    echo "runner cpu: $(awk -F ': ' '/^model name/ { print $2; exit }' /proc/cpuinfo), $(grep -c '^processor' /proc/cpuinfo) cores"
fi

estimated_seconds=$(printf '%s\n' "$assignment" | cut -f2)
assigned_case_count=$(printf '%s\n' "$assignment" | cut -f3)
echo "running E2E shard $shard_index/$shard_count with $assigned_case_count discovered test cases (~${estimated_seconds}s estimated)"
# Per-test result lines (not ErrorsOnly): flake triage needs to reconstruct which tests ran,
# in what order, before a failure — an errors-only log makes that unrecoverable afterwards.
"$dotnet_cmd" test "$test_dll" \
    --settings "$run_settings" \
    --filter "$shard_filter" \
    --logger "console;verbosity=normal"

elapsed_seconds=$(( $(date +%s) - start_seconds ))
echo "E2E shard $shard_index completed in ${elapsed_seconds}s"
