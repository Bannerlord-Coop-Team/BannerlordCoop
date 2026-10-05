# Bannerlord feature map

This map is derived from this BannerlordCoop checkout and the installed Native v1.4.8
managed assemblies. It separates player actions, exact installed definitions, co-op changes,
and available verification. [Provenance](provenance.md) binds the sources and proof limits.

There are 379 action-level coverage rows in 31 areas. The installed snapshot contains
212 campaign behavior registrations, including 43 issue behaviors, 374 initialized perks
and 165 menu options. The implementation index contains 625 framework commands, 8 legacy
commands, 394 explicit sync registrations and 2,126 Harmony target declarations.
These counts describe different inventories and must not be added together as verified features.

The action map is broad; the executable recipes are a starter set. This is not exhaustive:
329 candidate action rows still need their exact installed implementation expanded, and the
four game recipes need real exercise. Native engine/input behavior, StoryMode progression,
native multiplayer, custom battle and DLC-specific paths are explicit expansion gaps.

Read [player actions](player-actions.md) for individual triggers, variants and required observations;
read [installed definitions](installed.md) for every mapped menu, perk and issue;
read [co-op differences](coop-differences.md) for observed replacements and restrictions.
The [focused stolen-goods branch map](quest-branches.md) expands the one allowlisted issue;
it records inspected outcomes without claiming any were played.
The portable [verification skill](../.agents/skills/verify-bannerlord/SKILL.md) explains execution.

| Surface | Granularity | Evidence meaning |
| --- | --- | --- |
| [catalog.csv](catalog.csv) | action, entry point, variants, independent oracle, support, status, sources, applicable recipe | acceptance targets; candidates are unresolved questions |
| [inventory.csv](inventory.csv) | command/argument/side, patch target, sync declaration, menu callback, perk role/bonus, issue gate, source line/hash | lexical declarations and installed definitions, not runtime admission |
| [baseline/installed.json](baseline/installed.json) | exact installed registrar, menu and perk records with owning DLL hash | reproducible discovery snapshot; no copied game implementation |
| [evidence](evidence/authoring.md) | exact exercised scope and blockers | selection/map checks only; no gameplay pass |

From the repository root:

```powershell
python tools/feature_map.py find governor
python tools/feature_map.py find tournaments --kind command
python tools/feature_map.py find Issues --kind vanilla-issue
python tools/feature_map.py find Riding --kind vanilla-perk
python tools/feature_map.py find village --kind vanilla-menu-option
python tools/feature_map.py check
```

`find` emits existing records, not a generated test plan. Commands can be declared for both
sides yet still require server-only mutation by the verification procedure. Legacy side checks,
conditional compilation, Autofac registration and Harmony activation require source/runtime checks.

| Area | Rows | Area | Rows |
| --- | ---: | --- | ---: |
| sessions | 12 | character | 10 |
| movement | 14 | party | 20 |
| heroes | 17 | progression | 13 |
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

No other action inherits a recipe's pass. In particular, the tournament recipe does not prove
every round, bet, reward or battle; the quest gate recipe does not prove any quest completion.

Maintain the map by editing `catalog.csv` and regenerating with `python tools/feature_map.py refresh`.
Refresh only baseline records actually re-inspected against a named installed DLL hash; do not
infer newer game definitions from these older records. `refresh` regenerates the local lexical
inventory and readable pages, without reading the game, building, deploying, launching or testing.
Then run `check` and review the diff. Existing harness selection remains the only source of test tiers.
