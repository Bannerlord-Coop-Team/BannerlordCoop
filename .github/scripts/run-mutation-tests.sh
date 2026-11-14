#!/usr/bin/env bash
# Runs Stryker.NET on the lines a branch changes in the projects the Linux unit tests cover.
# Usage: run-mutation-tests.sh [--list] [base-ref] [output-dir] [project]
#   --list      print the projects that have changed lines, one per line, and exit
#   base-ref    branch or commit to diff against (default origin/development, diffed from the merge base)
#   output-dir  where each project's report and summary.md go, relative to the repo root
#               (default artifacts/mutation)
#   project     mutate only this project, one of mutated_projects
set -euo pipefail

# The mutated projects. Each is tested against every unit test project in source/CoopUnitTests.slnf
# that references it, except Coop.IntegrationTests, which starts full sessions and is too slow per
# mutant. Settings shared by all of them are in source/stryker-config.json.
mutated_projects=(Common Coop.Core GameInterface)
excluded_test_project=Coop.IntegrationTests

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
  echo "node is required to find the changed lines and write the summary." >&2
  exit 1
fi

merge_base=$(git merge-base "$base_ref" HEAD)
mapfile -t unit_test_projects < <(sed -nE 's/^ *"([^"\\]+)\\\\.*\.csproj",?$/\1/p' source/CoopUnitTests.slnf)

# Prints the unit test projects that reference one project through a ProjectReference.
test_projects_for() {
  local project="$1"
  local test_project
  for test_project in "${unit_test_projects[@]}"; do
    if [[ "$test_project" == "$excluded_test_project" ]]; then
      continue
    fi
    if grep -qF "\\$project\\$project.csproj\"" "source/$test_project/$test_project.csproj"; then
      echo "$test_project"
    fi
  done
}

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
  local test_project pattern
  while IFS= read -r test_project; do
    args+=(--test-project "$repo_root/source/$test_project/$test_project.csproj")
  done < <(test_projects_for "$project")
  while IFS= read -r pattern; do
    args+=(--mutate "$pattern")
  done <<< "$patterns"

  echo "== Stryker $project ($(wc -l <<< "$patterns") changed files)"
  (cd "source/$project" && dotnet stryker "${args[@]}")
}

# Projects with changed lines, mapped to their patterns. The loops below walk mutated_projects
# and skip the projects missing here, so the run order stays the order of that list.
declare -A project_patterns=()
for project in "${mutated_projects[@]}"; do
  patterns=$(mutate_patterns "$project")
  if [[ -n "$patterns" ]]; then
    project_patterns[$project]=$patterns
  fi
done

if [[ "$list_only" == true ]]; then
  for project in "${mutated_projects[@]}"; do
    if [[ -n "${project_patterns[$project]:-}" ]]; then
      echo "$project"
    fi
  done
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
for project in "${mutated_projects[@]}"; do
  if [[ -z "${project_patterns[$project]:-}" ]]; then
    continue
  fi
  rm -rf "${output_directory:?}/$project"
  mutate_project "$project" "${project_patterns[$project]}"
  reports+=("$project=$output_directory/$project/reports/mutation-report.json")
done
node "$scripts/mutation-summary.cjs" "${reports[@]}" > "$output_directory/summary.md"
cat "$output_directory/summary.md"
