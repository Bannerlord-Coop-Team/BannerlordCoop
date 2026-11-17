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

    # Holds the class that just finished in this job until the job is known to have finished.
    function close_class(job,    key) {
        if (current[job] == "") return
        key = job SUBSEP current[job]
        if (!(key in pending_total)) pending_classes[job] = pending_classes[job] "\n" current[job]
        pending_total[key] += last_end[job] - class_start[job]
        pending_cases[key] += cases[job]
        current[job] = ""
    }

    # Banks or drops the job'"'"'s held classes. A cancelled job has no summary, and its last class
    # stops at the cancel, so it would understate exactly the shards that timed out.
    function finish_job(job, keep,    count, names, i, key) {
        close_class(job)
        count = split(pending_classes[job], names, "\n")
        for (i = 2; i <= count; i++) {
            key = job SUBSEP names[i]
            if (keep) {
                total[names[i]] += pending_total[key]
                samples[names[i]] += pending_cases[key]
            }
            delete pending_total[key]
            delete pending_cases[key]
        }
        if (!keep && count > 1) dropped++
        delete pending_classes[job]
        delete last_end[job]
    }

    {
        sub(/\r$/, "")
        # `gh run view --log` prefixes each line with its job name; a single job log has no prefix.
        job = FILENAME
        if (index($0, "\t")) job = job SUBSEP substr($0, 1, index($0, "\t") - 1)

        if ($0 ~ /Starting test execution, please wait/) {
            finish_job(job, 0)
            if (timestamp($0) < 0) next
            last_end[job] = timestamp($0)
            first_test[job] = 1
            next
        }
        if ($0 ~ /Total tests: [0-9]/) {
            finish_job(job, 1)
            next
        }
        if (!match($0, /(Passed|Failed|Skipped) E2E\.Tests\./)) next
        line = substr($0, RSTART + index(substr($0, RSTART), " "))
        if (!match(line, /\[[^][]*\]$/)) next
        seconds = to_seconds(substr(line, RSTART))
        name = substr(line, 1, RSTART - 2)
        now = timestamp($0)
        if (now < 0 || !(job in last_end)) next
        # Only a jump back of more than half a day is midnight; a line out of order costs nothing.
        if (now < last_end[job]) {
            if (last_end[job] - now > 43200) now += 86400
            else now = last_end[job]
        }

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
        # The first harness test in every shard pays a 13-25 s one-off bootstrap, not its own cost.
        # Lightweight tests can finish before it, so it is the first one that took 10 s or more.
        if (first_test[job] && seconds >= 10) {
            class_start[job] += seconds
            if (class_start[job] > now) class_start[job] = now
            first_test[job] = 0
        }
        cases[job]++
        last_end[job] = now
    }

    END {
        for (job in last_end) open_jobs[++open_count] = job
        for (i = 1; i <= open_count; i++) finish_job(open_jobs[i], 0)
        if (dropped) printf "skipped %d shard logs with no test run summary (cancelled or cut short)\n", dropped > "/dev/stderr"
        print "# E2E test class<TAB>average seconds per test case, read by run-e2e-shard.sh."
        print "# Regenerate with .github/scripts/refresh-e2e-durations.sh; missing classes use the script default."
        fflush()
        for (class_name in total)
            printf "%s\t%.2f\n", class_name, total[class_name] / samples[class_name] | "LC_ALL=C sort"
    }' "$@"
