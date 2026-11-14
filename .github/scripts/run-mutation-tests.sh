#!/usr/bin/env bash
# Runs Stryker.NET on the lines a branch changes in the projects the Linux unit tests cover.
# Usage: run-mutation-tests.sh [--list] [base-ref] [output-dir]
#   --list      print the --mutate patterns for the changed lines and exit
#   base-ref    branch or commit to diff against (default origin/development, diffed from the merge base)
#   output-dir  where each project's report and summary.md go, relative to the repo root
#               (default artifacts/mutation)
set -euo pipefail

# The mutated projects and the unit test projects that cover each one. Settings shared by all of
# them are in source/stryker-config.json.
declare -A test_projects=(
  [Common]="Common.Tests Coop.Tests"
  [Coop.Core]="Coop.Tests"
  [GameInterface]="GameInterface.Tests Coop.Tests"
)
mapfile -t projects < <(printf '%s\n' "${!test_projects[@]}" | sort)

list_only=false
if [[ "${1:-}" == "--list" ]]; then
  list_only=true
  shift
fi

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
scripts="$repo_root/.github/scripts"
cd "$repo_root"
base_ref="${1:-origin/development}"
# Absolute, because Stryker runs from inside each project folder and resolves --output from there.
output_directory=$(realpath -m "${2:-artifacts/mutation}")

if ! command -v node >/dev/null; then
  echo "node is required to find the changed lines and write the summary." >&2
  exit 1
fi

merge_base=$(git merge-base "$base_ref" HEAD)

# Prints the --mutate patterns, relative to the project folder, covering one project's changed lines.
mutate_patterns() {
  node "$scripts/mutation-spans.cjs" "$merge_base" "source/$1"
}

# Runs Stryker in one project, mutating only the given newline-separated patterns.
mutate_project() {
  local project="$1"
  local patterns="$2"
  local args=(--config-file ../stryker-config.json --project "$project.csproj" --output "$output_directory/$project")
  local test_project pattern
  for test_project in ${test_projects[$project]}; do
    args+=(--test-project "../$test_project/$test_project.csproj")
  done
  while IFS= read -r pattern; do
    args+=(--mutate "$pattern")
  done <<< "$patterns"

  echo "== Stryker $project ($(wc -l <<< "$patterns") changed files)"
  (cd "source/$project" && dotnet stryker "${args[@]}")
}

if [[ "$list_only" == true ]]; then
  for project in "${projects[@]}"; do
    mutate_patterns "$project" | sed "s|^|source/$project/|"
  done
  exit 0
fi

mkdir -p "$output_directory"
rm -f "$output_directory/summary.md"

scheduled=()
declare -A project_patterns=()
for project in "${projects[@]}"; do
  rm -rf "${output_directory:?}/$project"
  patterns=$(mutate_patterns "$project")
  if [[ -z "$patterns" ]]; then
    continue
  fi
  scheduled+=("$project")
  project_patterns[$project]=$patterns
done

# Rewrites summary.md from the reports so far, so a failed or cut-off run still has one. It goes
# through a temp file so a summary that fails to render keeps the last good one.
write_summary() {
  node "$scripts/mutation-summary.cjs" "$output_directory" "${scheduled[@]}" > "$output_directory/summary.md.tmp"
  mv "$output_directory/summary.md.tmp" "$output_directory/summary.md"
}
write_summary

# Stryker builds with Platform=AnyCPU, where Coop.Core treats warnings as errors, so the
# Scriban advisory from NuGet's audit would fail the build. The audit adds nothing to a mutation run.
export NuGetAudit=false
dotnet tool restore

for project in "${scheduled[@]}"; do
  mutate_project "$project" "${project_patterns[$project]}"
  write_summary
done
cat "$output_directory/summary.md"
