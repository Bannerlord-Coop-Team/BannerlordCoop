#!/usr/bin/env bash
# Runs Stryker.NET on the lines a branch changes in the projects the Linux unit tests cover.
# Usage: run-mutation-tests.sh [--list] [base-ref] [output-dir] [project]
#   --list      print the projects that have changed lines, one per line, and exit
#   base-ref    branch or commit to diff against (default origin/development, diffed from the merge base)
#   output-dir  where each project's report and summary.md go, relative to the repo root
#               (default artifacts/mutation)
#   project     mutate only this project, one of mutated_projects
set -euo pipefail

# The mutated projects. mutation-projects.cjs finds the unit test projects that cover each one, and
# the settings shared by all of them are in source/stryker-config.json.
mutated_projects=(Common Coop.Core GameInterface)

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
only_project="${3:-}"

if [[ -n "$only_project" ]]; then
  if [[ " ${mutated_projects[*]} " != *" $only_project "* ]]; then
    echo "Unknown project '$only_project'. Mutated projects: ${mutated_projects[*]}" >&2
    exit 1
  fi
  mutated_projects=("$only_project")
fi

if ! command -v node >/dev/null; then
  echo "node is required to find the changed lines, the test projects and write the summary." >&2
  exit 1
fi

merge_base=$(git merge-base "$base_ref" HEAD)

# Prints the --mutate patterns, relative to the project folder, covering one project's changed lines.
mutate_patterns() {
  node "$scripts/mutation-spans.cjs" "$merge_base" "source/$1"
}

# Prints the mutated projects that have changed lines, in the order of mutated_projects.
changed_projects() {
  local project patterns
  for project in "${mutated_projects[@]}"; do
    patterns=$(mutate_patterns "$project")
    if [[ -n "$patterns" ]]; then
      echo "$project"
    fi
  done
}

# Runs Stryker in one project, mutating only its changed lines against the tests that cover it.
mutate_project() {
  local project="$1"
  local patterns test_projects test_project pattern
  patterns=$(mutate_patterns "$project")
  test_projects=$(node "$scripts/mutation-projects.cjs" "$project")
  local args=(
    --config-file "$repo_root/source/stryker-config.json"
    --project "$project.csproj"
    --output "$output_directory/$project"
  )
  while IFS= read -r test_project; do
    args+=(--test-project "$repo_root/$test_project")
  done <<< "$test_projects"
  while IFS= read -r pattern; do
    args+=(--mutate "$pattern")
  done <<< "$patterns"

  echo "== Stryker $project ($(wc -l <<< "$patterns") changed files)"
  (cd "source/$project" && dotnet stryker "${args[@]}")
}

projects_to_mutate=$(changed_projects)
if [[ "$list_only" == true ]]; then
  if [[ -n "$projects_to_mutate" ]]; then
    echo "$projects_to_mutate"
  fi
  exit 0
fi

mkdir -p "$output_directory"
rm -f "$output_directory/summary.md"

# Stryker builds with Platform=AnyCPU, where Coop.Core treats warnings as errors, so the
# Scriban advisory from NuGet's audit would fail the build. The audit adds nothing to a mutation run.
export NuGetAudit=false
dotnet tool restore

# Stryker writes each project's JSON report under its --output folder, in reports/.
reports=()
while IFS= read -r project; do
  if [[ -z "$project" ]]; then
    continue
  fi
  rm -rf "${output_directory:?}/$project"
  mutate_project "$project"
  reports+=("$project=$output_directory/$project/reports/mutation-report.json")
done <<< "$projects_to_mutate"

# Captured first, so a summary that fails to render leaves no summary.md behind.
summary=$(node "$scripts/mutation-summary.cjs" "${reports[@]}")
echo "$summary" > "$output_directory/summary.md"
echo "$summary"
