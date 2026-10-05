# Bannerlord feature map

This map is derived from this BannerlordCoop checkout and the installed Native v1.4.8
managed assemblies. It separates player actions, exact installed definitions, co-op changes,
and available verification. [Provenance](provenance.md) binds the sources and proof limits.

There are 385 navigation groups in 31 areas and **2,572 individual behavior leaves**:
110 interpreted source cases, 1,260 inspected definitions and 1,202 unresolved action requirements.
The definitions include 374 separate perk choices, **721 primary/secondary perk effects** and
165 menu choices. All 11 declared perk roles have their own groups, including specialists.
Read [the granularity contract](granularity.md) for what these counts establish.

The separate [structural source index](source-paths.md) has 8,637 member/guard/switch paths
from all 43 registered issue types, 122 CampaignSystem default models, three SandBox agent
models and three menu behaviors. All 124 default models were inspected; the two property-only
models are bound in the manifest and are outside this lexical member index.
Its paths are navigation, not interpreted behavior counts.
[Source identities](baseline/behavior-sources.json) bind 175 installed types and two repository files.

The original installed snapshot also retains 212 campaign behavior registrations, including
43 issue behaviors. The implementation index contains 625 framework commands, 8 legacy
commands, 394 explicit sync registrations and 2,126 Harmony target declarations.
These overlapping inventories must not be summed as verified features.

Start with [individual behavior leaves](behaviors/README.md) for separately identified actions,
preconditions, outcomes, side effects, authority, peer observations and remaining work.
Read [player action groups](player-actions.md) for navigation;
read [perk effect sheets](perks.md) for every effect and located consumer reference;
read [installed definitions](installed.md) for every mapped menu, perk and issue;
read [co-op differences](coop-differences.md) for observed replacements and restrictions.
The [focused stolen-goods branch map](quest-branches.md) expands the one allowlisted issue;
it records inspected outcomes without claiming any were played.
The portable [verification skill](../.agents/skills/verify-bannerlord/SKILL.md) explains execution.

| Surface | Granularity | Evidence meaning |
| --- | --- | --- |
| [catalog.csv](catalog.csv) | navigation group, entry point, variants, acceptance target, support and sources | a group never inherits all its leaves' evidence |
| [behavior-leaves.csv](behavior-leaves.csv) | one case/choice/effect, preconditions, state/side effects, owner/observer, boundary, persistence, source span, knowledge/runtime, gap | candidates, definitions and interpreted cases remain separate |
| [source-paths.csv](source-paths.csv) | exact member/guard/switch location, local returns, assignment/call names and containing span | structural navigation requiring interpretation |
| [inventory.csv](inventory.csv) | command/argument/side, patch target, sync declaration, menu callback, perk role/bonus, issue gate, source line/hash | lexical declarations and installed definitions, not runtime admission |
| [baseline/installed.json](baseline/installed.json) | exact installed registrar, menu and perk records with owning DLL hash | reproducible discovery snapshot; no copied game implementation |
| [baseline/perk-effects.json](baseline/perk-effects.json) | effect description, declared role/bonus/increment, troop mask, threshold, alternative and candidate consumers | definition evidence, no automatic consumer/effect assignment |
| [baseline/behavior-sources.json](baseline/behavior-sources.json) | type/file identity, owning hash, complete source digest and line count | source binding for the expanded leaves/paths |
| [interpreted-cases.json](interpreted-cases.json) | curated concrete complete-member outcomes and boundaries | maintain source interpretation independently from lexical discovery |
| [evidence](evidence/authoring.md) | exact exercised scope and blockers | selection/map checks only; no gameplay pass |

From the repository root:

```powershell
python tools/feature_map.py find governor
python tools/feature_map.py find tournaments --kind command
python tools/feature_map.py find Issues --kind vanilla-issue
python tools/feature_map.py find Riding --kind vanilla-perk
python tools/feature_map.py find WrappedHandles --kind perk-effect
python tools/feature_map.py find trade.partial-stock --kind interpreted-case
python tools/feature_map.py find GangLeaderNeedsToOffload --kind quest-guard
python tools/feature_map.py find village --kind vanilla-menu-option
python tools/feature_map.py check
```

`find` emits existing records, not a generated test plan. Commands can be declared for both
sides yet still require server-only mutation by the verification procedure. Legacy side checks,
conditional compilation, Autofac registration and Harmony activation require source/runtime checks.

| Area | Groups | Area | Groups |
| --- | ---: | --- | ---: |
| sessions | 12 | character | 10 |
| movement | 14 | party | 20 |
| heroes | 17 | progression | 19 |
| inventory | 12 | trade | 12 |
| workshops | 6 | caravans | 6 |
| crafting | 11 | towns | 17 |
| villages | 12 | buildings | 7 |
| locations | 13 | alleys | 6 |
| prisoners | 10 | clans | 11 |
| kingdoms | 20 | armies | 10 |
| quests | 13 | tournaments | 12 |
| battles | 29 | sieges | 16 |
| naval | 9 | ai | 11 |
| presentation | 15 | options | 7 |
| save | 11 | tools | 10 |
| vanilla-modes | 6 | | |

Five starter recipes:

| Recipe | Exact covered slice | Current status |
| --- | --- | --- |
| [Verification selection](recipes/selection.md) | real CLI planning, source binding, unknown-path fallback, invalid-head rejection | exercised on `5f4d63cc0975174ab42cfe1da67fc6d7214065e7` |
| [Two-client admission](recipes/two-clients.md) | staged join, readiness, distinct identities, shared observation, disconnect | draft; source-inspected; blocked on direct runtime tools and source-bound owned Debug run |
| [Danustica tournament](recipes/tournament.md) | owned fixture, shared lobby, current start/choice, leave and restore | draft; source-inspected; same runtime blocker |
| [Danustica garrison](recipes/garrison.md) | authoritative component lifecycle and both clients' backlinks | draft; source-inspected; same runtime blocker |
| [Quest gate](recipes/quest-gate.md) | normal allowlist, debug catalog distinction, journal restriction | draft; source-inspected; runtime/UI observations missing |

The executable recipes are a starter set. No behavior leaf was newly played during expansion.
The candidate requirements, consumer/caller traces and four game recipes remain unfinished;
native engine/input, StoryMode, native multiplayer, custom battle and optional modules need
separate discovery. This is not an exhaustive gameplay or compatibility claim.

No other action inherits a recipe's pass. In particular, the tournament recipe does not prove
every round, bet, reward or battle; the quest gate recipe does not prove any quest completion.

Maintain group requirements in `catalog.csv` and individual requirements in `behavior-leaves.csv`.
Keep curated source cases consistent with `interpreted-cases.json`, and perk definition leaves
consistent with `baseline/perk-effects.json`. `source-paths.csv` is structural input, not a case oracle.
Regenerate readable pages with `python tools/feature_map.py refresh`.
Refresh only baseline records actually re-inspected against a named installed DLL hash; do not
infer newer game definitions from these older records. `refresh` regenerates the local lexical
inventory and readable pages, without reading the game, building, deploying, launching or testing.
Then run `check` and review the diff. Existing harness selection remains the only source of test tiers.
