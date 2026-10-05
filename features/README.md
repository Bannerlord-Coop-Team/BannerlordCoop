# Bannerlord feature map

This is a compact map of player-visible behavior in BannerlordCoop, bound to the installed
Native v1.4.8 snapshot and repository source in [provenance](provenance.md).
It lists **385 behaviors in 31 areas**, with **63 interpreted source cases** where concrete
outcomes or boundaries have been inspected. These are coverage requirements and source
observations, not a gameplay compatibility claim.

Start with [the readable behavior map](player-actions.md), or search the canonical
[catalog.csv](catalog.csv). Each entry records its input surface, outcome variants, required
observation, support restriction, source and recipe when available.
[source-cases.csv](source-cases.csv) adds material success/refusal/partial/reward boundaries;
numeric examples of one calculation share a boundary table instead of becoming separate features.
Read [the granularity contract](granularity.md) before treating an entry as an acceptance target.

The [co-op differences](coop-differences.md), [stolen-goods branch map](quest-branches.md),
[perk inspection guide](perks.md) and repository-local
[verification skill](../.agents/skills/verify-bannerlord/SKILL.md) explain the relevant restrictions
and execution. All 63 source cases remain unrun. Four game recipes are draft.

| Area | Behaviors | Area | Behaviors |
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

From the repository root:

```powershell
python tools/feature_map.py find governor --kind feature
python tools/feature_map.py find trade.partial-stock --kind interpreted-case
python tools/feature_map.py find tournaments --kind command
python tools/feature_map.py find Issues --kind vanilla-issue
python tools/feature_map.py find Riding --kind vanilla-perk
python tools/feature_map.py inventory --kind sync-registration
python tools/feature_map.py check
```

`find` returns matching records. `inventory` emits CSV on demand: current repository command,
patch and sync declarations, plus definitions from [the installed snapshot](baseline/installed.json).
The snapshot retains 212 campaign registrations, 374 perk choices and 165 menu choices.
Repository declarations are regenerated from tracked source on every query; installed records
remain tied to the named DLL hashes. Neither proves runtime activation. No generated source
inventory, every-branch census or per-perk page tree is committed.

| Starter recipe | Covered slice | Evidence status |
| --- | --- | --- |
| [Verification selection](recipes/selection.md) | real CLI planning, source binding, unknown-path fallback and invalid-head rejection | exercised on the implementation named in [the receipt](evidence/authoring.md) |
| [Two-client admission](recipes/two-clients.md) | join readiness, distinct identities, shared observation and disconnect | draft; source-inspected; needs an owned source-bound Debug runtime |
| [Danustica tournament](recipes/tournament.md) | shared lobby, start/choice, leave and owned fixture restore | draft; same runtime requirement |
| [Danustica garrison](recipes/garrison.md) | authoritative lifecycle and both clients' backlinks | draft; same runtime requirement |
| [Quest gate](recipes/quest-gate.md) | normal allowlist, debug catalog distinction and journal restriction | draft; normal runtime/UI observations missing |

The recipes are a starter set. Tournament admission does not prove every round, bet or reward;
the quest gate does not prove quest completion. StoryMode, native engine/input, native multiplayer,
custom battle and optional modules require separate source discovery and runtime proof.

Maintain behavior requirements in `catalog.csv` and interpreted outcomes in `source-cases.csv`.
Add branch detail only when it changes an observable outcome or required side effect.
`python tools/feature_map.py refresh` updates the single readable behavior map. Baselines must
be re-inspected against a named installed build before updating their records. Run `check` after
edits. The existing VerificationHarness remains the only source of required test tiers.
