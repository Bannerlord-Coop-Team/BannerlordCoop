#!/usr/bin/env sh

# Rebuilds .github/e2e-test-durations.tsv from E2E shard job logs, read from the given files or stdin.
# Logs come from green PR runs, e.g. `gh run view <run-id> --log > e2e.log` (all shards in one file).
# Several runs average out runner speed, so pass a few: `sh refresh-e2e-durations.sh run1.log run2.log`.
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

    # Seconds since midnight of the line'"'"'s Actions timestamp, or -1 when it has none.
    function timestamp(text,    clock) {
        if (!match(text, /T[0-9][0-9]:[0-9][0-9]:[0-9][0-9][.0-9]*Z/)) return -1
        clock = substr(text, RSTART + 1, RLENGTH - 2)
        return substr(clock, 1, 2) * 3600 + substr(clock, 4, 2) * 60 + substr(clock, 7)
    }

    # Banks the class that just finished in this job as one sample.
    function close_class(job) {
        if (current[job] == "") return
        total[current[job]] += last_end[job] - class_start[job]
        samples[current[job]] += cases[job]
    }

    {
        sub(/\r$/, "")
        # `gh run view --log` prefixes each line with its job name; a single job log has no prefix.
        job = FILENAME
        if (index($0, "\t")) job = job SUBSEP substr($0, 1, index($0, "\t") - 1)

        if ($0 ~ /Starting test execution, please wait/) {
            close_class(job)
            current[job] = ""
            last_end[job] = timestamp($0)
            first_test[job] = 1
            next
        }
        if (!match($0, /(Passed|Failed|Skipped) E2E\.Tests\./)) next
        line = substr($0, RSTART + index(substr($0, RSTART), " "))
        if (!match(line, /\[[^][]*\]$/)) next
        seconds = to_seconds(substr(line, RSTART))
        name = substr(line, 1, RSTART - 2)
        now = timestamp($0)
        if (now < 0 || !(job in last_end)) next
        if (now < last_end[job]) now += 86400

        sub(/\(.*/, "", name)
        class_name = name
        sub(/\.[^.]*$/, "", class_name)

        # A shard runs one class at a time, so a class costs the time since the previous class
        # finished. That counts class fixtures and setup, which the per-test durations leave out.
        if (class_name != current[job]) {
            close_class(job)
            current[job] = class_name
            class_start[job] = last_end[job]
            cases[job] = 0
        }
        # The first test in every shard pays a 13-25 s one-off bootstrap, not its own cost.
        if (first_test[job]) {
            class_start[job] += seconds
            first_test[job] = 0
        }
        cases[job]++
        last_end[job] = now
    }

    END {
        for (job in current) close_class(job)
        print "# E2E test class<TAB>average seconds per test case, read by run-e2e-shard.sh."
        print "# Regenerate with .github/scripts/refresh-e2e-durations.sh; missing classes use the script default."
        fflush()
        for (class_name in total)
            printf "%s\t%.2f\n", class_name, total[class_name] / samples[class_name] | "LC_ALL=C sort"
    }' "$@"
