#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "usage: run-verification-plan.sh <base-commit> <output-directory>" >&2
  exit 2
fi

requested_base="$1"
output_directory="$2"
harness_project="source/VerificationHarness/VerificationHarness.csproj"
test_project="source/VerificationHarness.Tests/VerificationHarness.Tests.csproj"

if [[ ! "$requested_base" =~ ^[0-9a-fA-F]{40}$ ]]; then
  echo "base commit must be a 40-hex Git object id" >&2
  exit 2
fi

if [[ -n "$(git status --porcelain=v1 --untracked-files=all)" ]]; then
  echo "verification planning requires a clean checkout so HEAD identifies the built source" >&2
  exit 4
fi

ignored_compile_inputs=()
while IFS= read -r ignored_path; do
  case "$ignored_path" in
    */bin/*|*/obj/*) continue ;;
  esac
  ignored_compile_inputs+=("$ignored_path")
done < <(git ls-files --others --ignored --exclude-standard -- \
  source/VerificationHarness \
  source/VerificationHarness.Tests \
  source/Common \
  Directory.Build.props \
  Directory.Build.targets \
  Directory.Packages.props \
  global.json \
  NuGet.Config \
  nuget.config \
  source/Directory.Build.props \
  source/Directory.Build.targets \
  source/Directory.Packages.props \
  source/global.json \
  source/NuGet.Config \
  source/nuget.config)
if (( ${#ignored_compile_inputs[@]} > 0 )); then
  printf 'verification planning found ignored compile input absent from HEAD: %s\n' \
    "${ignored_compile_inputs[@]}" >&2
  exit 4
fi

mkdir -p "$output_directory"
for output_file in \
  changed-paths.txt \
  plan.json \
  plan-receipt.json \
  process-peer-artifacts.json \
  process-peer-evidence.json; do
  rm -f -- "$output_directory/$output_file"
done
for generated_directory in \
  source/Common/bin \
  source/Common/obj \
  source/VerificationHarness/bin \
  source/VerificationHarness/obj \
  source/VerificationHarness.Tests/bin \
  source/VerificationHarness.Tests/obj; do
  rm -rf -- "$generated_directory"
done
base_commit="$(git rev-parse --verify "${requested_base}^{commit}")"
head_commit="$(git rev-parse --verify 'HEAD^{commit}')"
source_tree="$(git rev-parse 'HEAD^{tree}')"
git diff --name-only --no-renames "$base_commit...$head_commit" > "$output_directory/changed-paths.txt"

if [[ ! -s "$output_directory/changed-paths.txt" ]]; then
  echo "verification planning requires at least one changed path" >&2
  exit 3
fi

dotnet build "$test_project" -c Release --no-incremental --verbosity minimal
dotnet run --project "$harness_project" -c Release --no-build -- \
  plan --head "$head_commit" --tree "$source_tree" --stdin \
  < "$output_directory/changed-paths.txt" \
  > "$output_directory/plan.json"
dotnet run --project "$harness_project" -c Release --no-build -- \
  validate-plan \
  --plan "$output_directory/plan.json" \
  --head "$head_commit" \
  --tree "$source_tree" \
  --base "$base_commit" \
  --changed-paths "$output_directory/changed-paths.txt" \
  --output "$output_directory/plan-receipt.json"

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  verdict="$(jq -r '.verdict' "$output_directory/plan-receipt.json")"
  external_profiles="$(jq -r \
    '.externalRuntimeProfiles | if length == 0 then "none" else join(", ") end' \
    "$output_directory/plan-receipt.json")"
  {
    echo "### Verification plan handoff"
    echo
    echo "- Head: \`$head_commit\`"
    echo "- Tree: \`$source_tree\`"
    echo "- Verdict: \`$verdict\`"
    echo "- External runtime profiles: $external_profiles"
    echo "- Scope: profile selection and local-harness handoff; this receipt contains no unit, E2E, or runtime pass evidence."
  } >> "$GITHUB_STEP_SUMMARY"
fi

dotnet test "$test_project" -c Release --no-build \
  --filter 'Category!=ProcessPeer' \
  --consoleLoggerParameters:ErrorsOnly

if jq -e '.harnessOwnedProfiles | index("process-peer") != null' \
    "$output_directory/plan-receipt.json" >/dev/null; then
  dotnet run --project "$harness_project" -c Release --no-build -- \
    process-peer-manifest \
    --head "$head_commit" \
    --tree "$source_tree" \
    --output "$output_directory/process-peer-artifacts.json"
  dotnet run --project "$harness_project" -c Release --no-build -- \
    process-peer-suite \
    --head "$head_commit" \
    --tree "$source_tree" \
    --seed "0x${source_tree:0:16}" \
    --artifact-manifest "$output_directory/process-peer-artifacts.json" \
    --output "$output_directory/process-peer-evidence.json"
fi

jq . "$output_directory/plan-receipt.json"
