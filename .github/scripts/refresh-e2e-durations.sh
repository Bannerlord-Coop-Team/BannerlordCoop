#!/usr/bin/env sh

# Rebuilds .github/e2e-test-durations.tsv from E2E shard job logs, read from the given files or stdin.
# Logs come from a green PR run, e.g. `gh run view <run-id> --log > e2e.log` (all 8 shards in one file).
# Usage: sh .github/scripts/refresh-e2e-durations.sh e2e.log > .github/e2e-test-durations.tsv

set -eu

awk '
    # Seconds in a vstest console duration such as "780 ms", "1 s", "1 m 5 s" or "< 1 ms".
    function to_seconds(text,    count, parts, i, total) {
        gsub(/[][<]/, "", text)
        count = split(text, parts, " ")
        total = 0
        for (i = 1; i < count; i += 2) {
            if (parts[i + 1] == "ms") total += parts[i] / 1000
            else if (parts[i + 1] == "s") total += parts[i]
            else if (parts[i + 1] == "m") total += parts[i] * 60
            else if (parts[i + 1] == "h") total += parts[i] * 3600
        }
        return total
    }

    {
        sub(/\r$/, "")
        if (!match($0, /(Passed|Failed|Skipped) E2E\.Tests\./)) next
        line = substr($0, RSTART + index(substr($0, RSTART), " "))
        if (!match(line, /\[[^][]*\]$/)) next
        seconds = to_seconds(substr(line, RSTART))
        name = substr(line, 1, RSTART - 2)

        # The console logger truncates whole seconds, so "[1 s]" covers 1.000-1.999 s.
        if (seconds >= 1) seconds += 0.25
        # The first harness test in every shard pays a 13-25 s one-off bootstrap, not its own cost.
        if (seconds >= 10) next

        sub(/\(.*/, "", name)
        class_name = name
        sub(/\.[^.]*$/, "", class_name)
        total[class_name] += seconds
        samples[class_name]++
    }

    END {
        print "# E2E test class<TAB>average seconds per test case, read by run-e2e-shard.sh."
        print "# Regenerate with .github/scripts/refresh-e2e-durations.sh; missing classes use the script default."
        fflush()
        for (class_name in total)
            printf "%s\t%.2f\n", class_name, total[class_name] / samples[class_name] | "LC_ALL=C sort"
    }' "$@"
