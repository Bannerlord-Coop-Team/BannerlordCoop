#!/usr/bin/env bash
# Runs Stryker.NET on the lines a branch changes in the projects the Linux unit tests cover.
# Usage: run-mutation-tests.sh [--list] [base-ref] [output-dir]
#   --list      print the --mutate patterns for the changed lines and exit
#   base-ref    branch or commit to diff against (default origin/development, diffed from the merge base)
#   output-dir  where each project's report and summary.md go (default artifacts/mutation)
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

base_ref="${1:-origin/development}"
output_directory="${2:-artifacts/mutation}"
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
scripts="$repo_root/.github/scripts"
cd "$repo_root"

if ! command -v node >/dev/null; then
  echo "node is required to find the changed lines and write the summary." >&2
  exit 1
fi

merge_base=$(git merge-base "$base_ref" HEAD)

# Prints the --mutate patterns covering the changed lines of one project, nothing if it has none.
mutate_patterns() {
  local files
  mapfile -t files < <(git diff --name-only --diff-filter=AMR "$merge_base" -- "source/$1/*.cs")
  if [[ ${#files[@]} -eq 0 ]]; then
    return
  fi
  node "$scripts/mutation-spans.cjs" "$merge_base" "${files[@]}"
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
    args+=(--mutate "${pattern#source/"$project"/}")
  done <<< "$patterns"

  echo "== Stryker $project ($(wc -l <<< "$patterns") changed files)"
  (cd "source/$project" && dotnet stryker "${args[@]}")
}

# Rewrites summary.md from the reports so far, so a failed or cut-off run still has one.
write_summary() {
  node "$scripts/mutation-summary.cjs" "$output_directory" > "$output_directory/summary.md"
}

if [[ "$list_only" == true ]]; then
  for project in "${projects[@]}"; do
    mutate_patterns "$project"
  done
  exit 0
fi

# Absolute, because Stryker runs from inside each project folder and resolves --output from there.
mkdir -p "$output_directory"
output_directory=$(cd "$output_directory" && pwd)
rm -f "$output_directory/summary.md"

declare -A project_patterns=()
for project in "${projects[@]}"; do
  rm -rf "${output_directory:?}/$project"
  patterns=$(mutate_patterns "$project")
  if [[ -z "$patterns" ]]; then
    continue
  fi
  project_patterns[$project]=$patterns
  # The folder stays without a report if Stryker fails or never gets to this project.
  mkdir -p "$output_directory/$project"
done
write_summary

# Stryker builds with Platform=AnyCPU, where Coop.Core treats warnings as errors, so the
# Scriban advisory from NuGet's audit would fail the build. The audit adds nothing to a mutation run.
export NuGetAudit=false
dotnet tool restore

for project in "${projects[@]}"; do
  if [[ -z "${project_patterns[$project]:-}" ]]; then
    continue
  fi
  mutate_project "$project" "${project_patterns[$project]}"
  write_summary
done
cat "$output_directory/summary.md"
