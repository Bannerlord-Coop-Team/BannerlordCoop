# Granularity and evidence contract

A behavior leaf addresses one input, one eligible subject/context, and one independently
observable outcome. Success, refusal, partial application, payment, reward, persistence and
another peer's observation must not share an inferred pass. A complete feature needs its
required leaves, caller paths and side effects checked on the same named source/build.

The map has three different layers:

| Layer | Purpose | What it establishes |
| --- | --- | --- |
| [385 action groups](player-actions.md) | navigation across 31 areas | the questions to cover, not every internal branch |
| [2,572 behavior leaves](behaviors/README.md) | individual choices, effects, concrete source cases and unresolved action requirements | only the knowledge/runtime state recorded on that leaf |
| [8,637 structural paths](source-paths.md) | complete-member locations, explicit `if` predicates and labeled switch arms in inspected types | source navigation, never an interpreted outcome or gameplay pass |

The leaf dataset contains 1,202 candidate action requirements, 374 perk-choice definitions,
721 individual perk-effect definitions, 165 menu-choice definitions and 110 interpreted
source cases. The source manifest binds 175 inspected installed types;
two repository production files are separately bound there. The structural
index has paths in 171 types; types containing only definitions/properties are not invented
as method paths. These inventories overlap and must not be summed as verified features.

## What a leaf records

[behavior-leaves.csv](behavior-leaves.csv) retains the stable ID, parent/domain, entry/action,
preconditions, expected result, state changes, side effects, authority, owner observation,
other-client observation, boundary/negative case, persistence, support, evidence, recipe and
remaining work. Candidate fields express acceptance requirements; their guards and exact
effects are unresolved. A written requirement is not evidence that the advertised action exists.

`knowledge` and `runtime` are independent:

| Knowledge | Meaning |
| --- | --- |
| `candidate` | named action requirement; exact installed behavior and co-op apply path remain unresolved |
| `definition-inspected` | exact declaration parameters/registration; consumer/caller behavior remains unresolved |
| `source-interpreted` | the stated outcome was interpreted from the named complete member; callers, indirect effects and co-op behavior are separate work |

`runtime: unrun` records no exercise of that leaf. `blocked` requires the concrete missing
prerequisite in retained evidence. `exercised` requires the precise action and expected/actual
observations, source/build/run identities and evidence required by the selected existing recipe.
All new leaves are unrun. The earlier selector receipt remains limited to the three CLI
planning checks in [selection evidence](evidence/authoring.md); it is not a gameplay receipt.

Support is another independent field. A disabled normal issue, release-excluded naval path,
blocked UI, definition or debug factory never becomes enabled because its source was mapped.
The current normal gate permits only the stolen-goods issue, and journal access is separately
blocked. The map does not change either restriction.

## Concrete separations

- `perk.OneHandedWrappedHandles.choice`, `.primary` and `.secondary` are different leaves.
  Selection uses skill threshold 25 and its alternative choice. The primary effect declares
  Personal handling with raw bonus `0.2f`/`AddFactor`; the secondary declares Captain skill
  with raw bonus `30f`/`Add` and `OneHandedUser` troop mask. One observation proves none of
  the other leaves. Both effects of `OneHandedDuelist` remain separate even though both are Personal.
- `q.pay-commit` deducts payment and sets flags; `q.paid-keep` is the later terminal result
  that adds goods and rewards. Dialogue entry, payment, victory, inventory and terminal quest
  state are separate observations. Each of the four terminal choices has its own source case.
- `trade.partial-stock` requests five items for 100 with three currently available. It records
  proposal adjustment/refund 40; actual roster/gold application belongs to the caller.
- `clan.tier.2.below` uses renown 149 and calculated tier 1; `clan.tier.2` uses renown 150
  and tier 2. Neither model return proves the caller applied tier-related side effects.

There is no automatic Cartesian expansion. Add a combination only when source shows an
interaction, ordering dependency or independent failure path. Save/reload and reconnect need
their own real observations for changed durable state; they cannot be inferred from a declaration.

## Structural navigation limits

[source-paths.csv](source-paths.csv) records lexical member/guard/switch locations with local
return expressions, assignment targets and call names. It is not a control-flow graph.
Nested operations may be conditional, returns may belong to exclusive branches, and a local
value may depend on an earlier alias or RNG. Short-circuit operands, `else` routes, switch
selectors/fallthrough, ternaries, expression-bodied properties, DialogFlow delegates,
registration/caller context, helpers and native callbacks are not exhaustively expanded.
Interpret the complete source context before turning a structural path into a behavior oracle.

Perk consumer references in [perk-effects.json](baseline/perk-effects.json) locate mentions
inside the inspected model set. Repeated mentions at one source location are collapsed.
They remain candidate consumers for both effects until role, primary/secondary argument,
troop flags, caps, formula and real application are traced. No match means discovery is
incomplete, not that a perk is unused. Unqualified nested names were matched to the exact
installed `DefaultPerks` skill/property definitions, not assigned a bonus by name.

## Coverage still requiring expansion

All 43 registered issue behavior types and 124 CampaignSystem default models were inspected;
structural navigation covers the 43 issue types and 122 of those models. `DefaultCampaignTimeModel`
and `DefaultTavernMercenaryTroopsModel` have property-only implementations outside this lexical
member index; their complete source identities remain bound in the manifest. Three SandBox
agent models and three menu behaviors are also inspected and indexed.
This does not establish all quest dialogues, scenario branches, publicizer/Harmony replacements,
all mission models, or a caller-complete game feature map. The other 42 normal issue types
remain gated off, and their structural paths are not terminal-result oracles.

The candidate requirements, untraced helpers/consumers, native engine/input/physics,
StoryMode, custom battle, native multiplayer and optional DLC modules remain explicit gaps.
No full-game or full-co-op completeness claim follows from the counts. Use the existing
VerificationHarness and owned runtime recipes for verification; this map supplies no second
harness or tier selector.
