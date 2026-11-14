#!/usr/bin/env bash
# Runs Stryker.NET on the C# files a branch changes in the projects the Linux unit tests cover.
# Usage: run-mutation-tests.sh [--list] [base-ref] [output-dir]
#   --list      print the changed files that would be mutated and exit
#   base-ref    branch or commit to diff against (default origin/development, diffed from the merge base)
#   output-dir  where each project's report goes (default artifacts/mutation)
set -euo pipefail

# Each project has a stryker-config.json naming the unit test projects that cover it.
mutable_projects=(Common Coop.Core GameInterface)

list_only=false
if [[ "${1:-}" == "--list" ]]; then
  list_only=true
  shift
fi

base_ref="${1:-origin/development}"
output_directory="${2:-artifacts/mutation}"
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$repo_root"

merge_base=$(git merge-base "$base_ref" HEAD)

# Lists the C# files changed since the merge base in one project, working tree included.
changed_files() {
  git diff --name-only --diff-filter=AMR "$merge_base" -- "source/$1/*.cs"
}

# Runs Stryker in one project, mutating only the given repo-relative files.
mutate_project() {
  local project="$1"
  shift
  local mutate_args=()
  local file
  for file in "$@"; do
    mutate_args+=(--mutate "${file#source/"$project"/}")
  done

  echo "== Stryker $project ($# changed files)"
  (cd "source/$project" && dotnet stryker "${mutate_args[@]}" --output "$output_directory/$project")
}

if [[ "$list_only" == true ]]; then
  for project in "${mutable_projects[@]}"; do
    changed_files "$project"
  done
  exit 0
fi

mkdir -p "$output_directory"
output_directory=$(cd "$output_directory" && pwd)
for project in "${mutable_projects[@]}"; do
  rm -rf "${output_directory:?}/$project"
done
echo "$merge_base" > "$output_directory/merge-base"

# Stryker builds with Platform=AnyCPU, where Coop.Core treats warnings as errors, so the
# Scriban advisory from NuGet's audit would fail the build. The audit adds nothing to a mutation run.
export NuGetAudit=false
dotnet tool restore

for project in "${mutable_projects[@]}"; do
  mapfile -t files < <(changed_files "$project")
  if [[ ${#files[@]} -eq 0 ]]; then
    continue
  fi
  mutate_project "$project" "${files[@]}"
done

summary_script=".github/scripts/mutation-summary.cjs"
if command -v node >/dev/null; then
  node "$summary_script" "$output_directory" | tee "$output_directory/summary.md"
fi
