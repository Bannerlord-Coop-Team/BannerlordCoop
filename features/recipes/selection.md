# Verification profile selection

Status: exercised on implementation commit `5f4d63cc0975174ab42cfe1da67fc6d7214065e7`,
tree `9fc0c1eb1986a7c6d86549dadccd324280610431`, native Windows / .NET 10.
The result proves the CLI slice below, not product compatibility or completion of selected tests.

Entry: [the existing VerificationHarness](../../source/VerificationHarness/Program.cs).
It selects cumulative requirements for a named source/path set. Known documentation,
unknown paths and invalid source identity are distinct cases.

Use an isolated supported checkout. Verify the harness/Common source roots and their build
imports have no unrelated edits or untracked compile inputs. The inspected build graph imports
no game deployment target and requires no `mb2` junction. Run from the repository root with
the installed Windows `dotnet` and native Python. Do not run a WSL/Linux executor.

Execute this block in one PowerShell process so its source/evidence variables remain in scope.
It includes launch, doctor, real planner actions and independent assertions:

```powershell
$verificationHead = git rev-parse HEAD
$verificationTree = git rev-parse 'HEAD^{tree}'
git diff --exit-code HEAD -- source/Common source/VerificationHarness
if ($LASTEXITCODE -ne 0) { throw 'Harness source differs from the named commit' }
dotnet build source/VerificationHarness/VerificationHarness.csproj -c Release -p:NuGetAudit=false --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Harness build failed' }
$verificationHarness = 'source/VerificationHarness/bin/Release/net10.0/VerificationHarness.dll'
$verificationEvidence = 'artifacts/feature-map/selection'
New-Item -ItemType Directory -Path $verificationEvidence -Force | Out-Null
Get-FileHash -LiteralPath $verificationHarness -Algorithm SHA256 | Format-List | Out-File "$verificationEvidence/harness-hash.txt"

dotnet $verificationHarness plan --head $verificationHead --tree $verificationTree doc/automated-testing/verification-harness.md | Set-Content "$verificationEvidence/documentation-plan.json" -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Documentation plan failed' }
$verificationPlan = Get-Content -Raw "$verificationEvidence/documentation-plan.json" | ConvertFrom-Json
if (-not $verificationPlan.inputValid -or $verificationPlan.highestRequiredTier -ne 'unit' -or $verificationPlan.verdict -ne 'pending') { throw 'Unexpected documentation plan' }
if ($verificationPlan.source.head -ne $verificationHead -or $verificationPlan.source.syntheticTree -ne $verificationTree) { throw 'Plan source mismatch' }

dotnet $verificationHarness plan --head $verificationHead --tree $verificationTree features/catalog.csv | Set-Content "$verificationEvidence/new-path-plan.json" -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Unknown-path plan failed' }
$verificationUnknown = Get-Content -Raw "$verificationEvidence/new-path-plan.json" | ConvertFrom-Json
if ($verificationUnknown.highestRequiredTier -ne 'full-live' -or $verificationUnknown.reasons.ruleId -notcontains 'unknown-path') { throw 'Unknown-path gate changed; inspect before updating this witness' }

dotnet $verificationHarness plan --head invalid --tree $verificationTree doc/automated-testing/verification-harness.md 2> "$verificationEvidence/invalid-head.txt"
if ($LASTEXITCODE -ne 2) { throw 'Invalid source was not rejected with exit 2' }
```

The unknown-path witness uses the newly introduced catalog path as it existed in the bound
planner. If the authoritative selector later explicitly classifies it, inspect that change and
update this negative witness to another actual unclassified repository path; do not weaken the
unknown-path gate or fabricate a changed-path set for a real handoff.

Oracle: documentation selects `unit`; unknown paths select `full-live` and all cumulative
checks; an invalid head exits `2`. Both generated plans remain `pending`, even when the planner
succeeds. A passing planner invocation is not a passing unit, CI or live result. Source IDs in
the CLI are syntactically validated, so the caller must establish actual source/build identity.

Evidence: keep plan JSON, negative stderr, command/exit observations, head/tree and harness
SHA-256 under the directory above. [Authoring evidence](../evidence/authoring.md) retains the
initial exercise. The official schema is [verification-report-v1.schema.json](../../doc/automated-testing/verification-report-v1.schema.json).

Cleanup: every invocation above is a bounded CLI process with no server/clients. Confirm each
exit and re-read evidence after completion. Retain generated files; do not delete another
checkout's outputs. No process-peer suite, deployment, game UI, gameplay, save or network
admission is exercised by this recipe. Production changed paths still require their real plan.
