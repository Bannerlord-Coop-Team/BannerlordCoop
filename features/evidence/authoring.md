# Authoring validation

Date: 2026-10-05. Bound implementation commit:
`5f4d63cc0975174ab42cfe1da67fc6d7214065e7`, tree
`9fc0c1eb1986a7c6d86549dadccd324280610431`.
Native Windows, Python 3.14.4, .NET SDK 10.0.300. An isolated worktree excluded unrelated
main-checkout edits. Harness/Common source was unchanged from the named implementation.

The [selection recipe](../recipes/selection.md) was exercised using the real repository CLI:

| Action | Independent expected result | Observed result |
| --- | --- | --- |
| build `source/VerificationHarness/VerificationHarness.csproj`, Release, `NuGetAudit=false` | successful build; no deployment | exit 0, zero warnings/errors; inspected graph has no game deployment target |
| plan `doc/automated-testing/verification-harness.md` | `unit`, valid input, exact head/tree, pending verdict | exit 0; all expected fields matched |
| plan `features/catalog.csv` | current unknown-path rule selects cumulative `full-live` | exit 0; `unknown-path`, `full-live`, all seven required checks |
| plan with `--head invalid` | reject malformed identity | exit 2; Git object ID must contain exactly 40 hexadecimal characters |

Retained, readable planner outputs: [documentation-plan.json](documentation-plan.json),
[new-path-plan.json](new-path-plan.json), [negative error](invalid-head.txt) and
[harness binary digest](harness.sha256). These use the repository's existing plan schema;
their verdict is `pending`, not passing verification. No child/game processes were created
by these planner invocations, and each command exited. Evidence remained readable afterward.

Installed source identities/ranges were checked with the read-only Bannerlord Analysis
validator against the current installed build: [installed-sources.json](installed-sources.json).
This receipt proves identity/range/hash freshness, not semantics or gameplay.

Map validation uses the repository-owned `python tools/feature_map.py check`: source declarations
and file hashes, CSV IDs/required fields, known status values, repository-contained paths,
portable Markdown links, generated-page consistency and allowed installed assembly identities.
The authoring checks also exercise stale-source and broken-link detection using a copied
isolated fixture, retaining failures rather than editing production source.

The skill frontmatter/scaffold is checked with the host's native skill-creator validator.
Result: valid. The two focused `python tools/test_feature_map.py -v` checks passed, covering
real command side/argument contracts, commented versus active sync declarations and isolated
stale-source/broken-link rejection. The corrected selector recipe also passed when its single
PowerShell block was executed cold; both plans and the expected exit-2 error remained readable.
The generated skill, feature helper and recipes depend only on repository-owned artifacts,
Python/.NET and the already-existing direct MCP driver, not a personal plugin installation.

Game proof gap: the authoring tool surface did not expose `bannerlord-coop` preflight/launch/
command/stop tools. No owned runtime, matching source-bound deployed DEBUG mod or isolated
campaign was admitted. Consequently the two-client, tournament, garrison and quest-gate recipes
are draft, source-inspected only. No live test was attempted, no game process was stopped,
no source/helper/lane owner was changed and no save/deployment was touched. Future execution
must satisfy [runtime prerequisites](../recipes/runtime.md), exercise the recipe, retain real
action/expected/actual evidence and confirm cleanup before changing its status.

Unknown paths in the existing selector remain `full-live`; this documentation does not lower
that gate. Passing map/skill validation, planner CLI behavior or installed source inspection
does not complete CI, rendered/full-live coverage, parent acceptance or release verification.

## Granularity expansion

The expanded map contains 385 groups and 2,572 individual leaves: 1,202 candidate action
requirements, 374 perk choices, 721 perk effects, 165 menu choices and 110 interpreted source
cases. All 11 declared perk roles retain distinct groups. Paired contexts such as success/failure,
acceptance/rejection and pacer/observer departure were separated; the two effects of a perk
remain distinct even when their declared roles match. The 31 interpreted stolen-goods cases
and concrete wage, recruitment, clan-tier, upgrade-XP, trade and focus cases remain unrun.

The separate structural index contains 8,637 source paths across 171 types. Discovery read
175 complete installed types: all 43 registered issue behaviors, 124 CampaignSystem default
models, three SandBox agent models, three menu behaviors, DefaultPerks and PerkObject.
Two property-only models have source identities but no lexical member paths. Two repository
production files carry separate file hashes. Perk sheets locate 913 distinct reference locations
in the inspected model set, without automatically assigning them to primary/secondary effects.

The existing focus-safe native decompiler fallback was used after managed-app artifact reads
stalled or timed out. The installed type index resolved namespaces before retrying source
reads, including seven SandBox-owned issues. No game/runtime action was retried. Both owning
DLL hashes were verified again after reads; Native's module descriptor remained `v1.4.8`.
Decompiler: `ilspycmd` and `ICSharpCode.Decompiler` `10.1.0.8386`.
[expanded-sources.json](expanded-sources.json) retains the native authoring receipt, counts and
dataset digests. It does not claim app validation of the expanded source ranges.

Expansion validation:

- `python tools/feature_map.py check` passed, including generated pages, all parent IDs,
  complete-source span bounds, installed/repository hash bindings, curated-case consistency,
  perk definition/leaf correspondence and portable links.
- `python tools/test_feature_map.py -v`: three focused checks passed in 71.524 seconds.
  They cover real command contracts, inactive commented sync declarations, primary/secondary
  roles/masks, two Personal effects, no invented secondary effect, shortened model references,
  specialist-role grouping and isolated rejection of stale source, broken links, unknown
  parents, out-of-range spans and mismatched installed DLL hashes.
- The native skill-creator validator accepted the updated verification skill.
- Representative `find` queries returned the partial-stock source case and both independent
  WrappedHandles effects. Isolation/portability checks passed; no decompiled `.cs` files are
  packaged. The implementation under `source` and the existing MCP driver are unchanged.

The previously passing selector exercise was retained with its original source/scope rather
than repeated. No additional harness build, CI, game build/deployment, launch, save mutation,
owner/lane change or live test occurred. All new leaves retain `runtime: unrun`.
