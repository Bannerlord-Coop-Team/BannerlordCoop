#!/usr/bin/env bash
# Flags tech-debt candidates on the lines a unified diff adds, tagged by lens. Every hit is a lead to verify, not a finding.
#   git diff "$(git merge-base origin/development HEAD)" | scan.sh   local branch + uncommitted changes
#   gh pr diff 1234 | scan.sh                                        a PR
set -euo pipefail

# Emits "path<TAB>line<TAB>text" for every added line in the diff on stdin.
added_lines() {
    awk '
        /^\+\+\+ / { path = substr($0, 5); sub(/^b\//, "", path); next }
        /^--- /    { next }
        /^@@ /     { split($3, n, ","); line = substr(n[1], 2) + 0; next }
        /^\+/      { print path "\t" line "\t" substr($0, 2); line++; next }
        /^ /       { line++ }
    '
}

# Prints added lines in scope (prod, tests or all .cs) that match a regex and not the optional skip regex.
check() {
    # Regexes go through ENVIRON because awk -v would eat their backslashes.
    SCOPE="$1" RE="$2" RULE="$3" SKIP="${4:-}" awk -F'\t' '
        function in_scope(path,   test) {
            if (path !~ /\.cs$/) return 0
            test = path ~ /(\.Tests|IntegrationTests|MissionTests)\//
            return ENVIRON["SCOPE"] == "all" || (ENVIRON["SCOPE"] == "tests") == test
        }
        in_scope($1) && $3 ~ ENVIRON["RE"] && (ENVIRON["SKIP"] == "" || $3 !~ ENVIRON["SKIP"]) {
            text = $3; gsub(/^[ \t]+/, "", text)
            printf "%s:%s  [%s]  %s\n", $1, $2, ENVIRON["RULE"], substr(text, 1, 140)
        }
    ' "$ADDED"
}

# Flags runs of 6+ consecutive added comment lines in .cs files.
long_comments() {
    awk -F'\t' '
        function flush() { if (run >= 6) printf "%s:%s  [fit: long comment]  %d-line comment block\n", file, start, run; run = 0 }
        $1 ~ /\.cs$/ && $3 ~ /^[ \t]*\/\// {
            if ($1 == file && $2 == last + 1 && run > 0) { run++ } else { flush(); file = $1; start = $2; run = 1 }
            last = $2; next
        }
        { flush() }
        END { flush() }
    ' "$ADDED"
}

ADDED=$(mktemp)
trap 'rm -f "$ADDED"' EXIT
added_lines > "$ADDED"

# fit: codebase fit and complexity
check prod  'AccessTools\.(Field|Property)|FieldRefAccess|BindingFlags\.NonPublic|\.Get(Field|Property)\(|\.(Get|Set)Value\(' 'fit: reflection on a publicized member?' 'registry\.Add'
check prod  'static class ' 'fit: new static helper?' '(Patch|Patches|Extensions)([^A-Za-z]|$)'
check prod  '\[CommandLineArgumentFunction' 'fit: legacy command, use ICoopCommand?'
long_comments

# defensive: exceptions, validation and symptom fixes
check prod  'catch[ \t]*(\([^)]*\))?[ \t]*\{[ \t]*\}' 'defensive: empty catch'
check prod  'catch[ \t]*(\(|$|\{)' 'defensive: catch at a boundary? recovers?'
check prod  '(==|is)[ \t]*null\)[ \t]*(throw|return)|ArgumentNullException|ThrowIfNull' 'defensive: validating an always-valid input?'
check all   'Thread\.Sleep|Task\.Delay' 'defensive: timing workaround?'

# flow: sync invariants, temporary and dead code
check all   '(TODO|HACK|FIXME|XXX)' 'flow: marker without issue?' '#[0-9]+'
check all   '\[Obsolete' 'flow: obsolete code added'
check prod  '(class|void|bool|Task)[ \t]+[A-Za-z]*(Repair|Backfill|Fallback|Legacy|Compat|Workaround)' 'flow: temporary code, removal condition?'
check prod  'new AllowedThread' 'flow: AllowedThread outside a receive apply?'
check prod  '\.TryGetObject[<(]' 'flow: lookup on the poll thread?'
check prod  'GameThread\.Run\(' 'flow: Run instead of RunSafe?'
check prod  '(Hero|Party|PartyBase|Campaign\.Current)\.Main(Hero|Party)' 'flow: host-party assumption?' 'IsClient'

# tests
check tests 'Skip[ \t]*=' 'tests: skipped test'
check tests '\.Verify\(' 'tests: mock verify restates the implementation?'
