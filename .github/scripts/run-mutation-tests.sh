#!/usr/bin/env bash
# Runs Stryker.NET on the lines a branch changes in the projects the Linux unit tests cover.
# Usage: run-mutation-tests.sh [--list] [base-ref] [output-dir] [project]
#   --list      print the projects that have changed lines, one per line, and exit
#   base-ref    branch or commit to diff against (default origin/development, diffed from the merge base)
#   output-dir  where each project's report and summary.md go, relative to the repo root
#               (default artifacts/mutation)
#   project     mutate only this project, one of the keys of test_projects
set -euo pipefail

# The mutated projects and, for each, every unit test project in source/CoopUnitTests.slnf that
# references it, except Coop.IntegrationTests, which starts full sessions and is too slow per mutant.
# Settings shared by all of them are in source/stryker-config.json.
declare -A test_projects=(
  [Common]="Common.Tests Coop.Tests GameInterface.Tests"
  [Coop.Core]="Coop.Tests"
  [GameInterface]="GameInterface.Tests Coop.Tests"
)
# Sorted, because bash leaves associative array order unspecified, so runs and summary rows stay stable.
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
# Absolute, like every path handed to Stryker, because Stryker runs from inside each project folder.
output_directory=$(realpath -m "${2:-artifacts/mutation}")

if [[ -n "${3:-}" ]]; then
  if [[ -z "${test_projects[$3]:-}" ]]; then
    echo "Unknown project '$3'. Mutated projects: ${projects[*]}" >&2
    exit 1
  fi
  projects=("$3")
fi

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
  local args=(
    --config-file "$repo_root/source/stryker-config.json"
    --project "$project.csproj"
    --output "$output_directory/$project"
  )
  local test_project_names test_project pattern
  read -ra test_project_names <<< "${test_projects[$project]}"
  for test_project in "${test_project_names[@]}"; do
    args+=(--test-project "$repo_root/source/$test_project/$test_project.csproj")
  done
  while IFS= read -r pattern; do
    args+=(--mutate "$pattern")
  done <<< "$patterns"

  echo "== Stryker $project ($(wc -l <<< "$patterns") changed files)"
  (cd "source/$project" && dotnet stryker "${args[@]}")
}

scheduled=()
declare -A project_patterns=()
for project in "${projects[@]}"; do
  patterns=$(mutate_patterns "$project")
  if [[ -z "$patterns" ]]; then
    continue
  fi
  scheduled+=("$project")
  project_patterns[$project]=$patterns
done

if [[ "$list_only" == true ]]; then
  for project in "${scheduled[@]}"; do
    echo "$project"
  done
  exit 0
fi

mkdir -p "$output_directory"
rm -f "$output_directory/summary.md"
for project in "${scheduled[@]}"; do
  rm -rf "${output_directory:?}/$project"
done

# Stryker builds with Platform=AnyCPU, where Coop.Core treats warnings as errors, so the
# Scriban advisory from NuGet's audit would fail the build. The audit adds nothing to a mutation run.
export NuGetAudit=false
dotnet tool restore

# Stryker writes each project's JSON report under its --output folder, in reports/.
reports=()
for project in "${scheduled[@]}"; do
  mutate_project "$project" "${project_patterns[$project]}"
  reports+=("$project=$output_directory/$project/reports/mutation-report.json")
done
node "$scripts/mutation-summary.cjs" "${reports[@]}" > "$output_directory/summary.md"
cat "$output_directory/summary.md"
