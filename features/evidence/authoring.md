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

Map validation uses the repository-owned `python tools/feature_map.py check`: cited source
hashes/spans, CSV IDs/required fields, known status values, repository-contained paths,
portable Markdown links, readable-map consistency and allowed installed assembly identities.
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

## Historical expanded discovery

[expanded-sources.json](expanded-sources.json) describes the original expanded documentation
revision `a8900b80dca176b125c2f8f13e8dddfa263d4f59`. Its counts and digests refer to those
historical datasets. The previous three Python checks passed in 71.524 seconds and the
skill validator passed. The compact revision removes the repeated pages, lexical branch
census and effect/leaf inventories; these historical results do not validate removed surfaces
or establish current gameplay coverage. Full source discovery details are in [provenance](../provenance.md).

## Compact map validation

The current map keeps 385 behavior entries and 63 interpreted source cases. Repeated numeric
inputs were combined into boundary tables; the 31 stolen-goods cases and distinct full/partial/
empty trade outcomes remain separate. Only the eight source identities cited by these cases
are retained in the complete-source manifest. Installed definition snapshots remain unchanged.

- `python tools/feature_map.py check` passed for the compact inputs and portable links.
- `python tools/test_feature_map.py -v`: two focused checks passed in 4.661 seconds. They
  cover real command side/arguments, inactive commented sync declarations, fresh on-demand
  inventory hashes and isolated rejection of stale case source, invalid parent/span, mismatched
  installed hash and broken link.
- The native skill-creator validator accepted the revised verification skill.
- `inventory --kind sync-registration` emitted 394 current records. Queries returned the
  partial-stock source case and the installed WrappedHandles definition; neither is a live test.

The earlier passing selector exercise is retained with its exact original source/scope.
No harness rebuild, CI, game build/deployment, launch, save or owner/lane change was performed
for this reduction. All 63 source cases retain `runtime: unrun`.

## Review corrections

Corrections to the review of documentation head `4b51faa483b59e1b4cbc133c3600bbcf83a2265b`
narrow the partial-stock restore to matching proposal slots, scope atomic rejection to the
focus batch, bind prior perk/attribute application and quest caller/consequence spans, and
carry release exclusions/experimental support into the catalog. The quest-type admission
recipe remains blocked on read-only type identification and pre-existing excluded issues.

`python tools/feature_map.py check` passed with 385 behaviors, 63 cases and runtime
`{"unrun": 63}`. `python tools/test_feature_map.py -v` passed both focused checks in 2.843
seconds, including constant-prefix discovery, unresolved non-literal arguments and normalized
BOM/newline source validation while retaining rejection of actual source drift. No `source/`
file changed, and no case, gameplay recipe or selector was run by these corrections.
