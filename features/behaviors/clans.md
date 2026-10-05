# clans behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## clans.001 Create clan

Each entry retains authority and separate owner/observer observations in the CSV.

### clans.001.case-01

Create clan / new player

- Entry/action: Clan screen / lord conversation; Perform create clan specifically in the new player context
- Preconditions: Isolated new player context for create clan
- Expected: Acceptance requirement for new player: one clan is linked to the intended player hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.001.case-02.1

Create clan / initial name

- Entry/action: Clan screen / lord conversation; Perform create clan specifically in the initial name context
- Preconditions: Isolated initial name context for create clan
- Expected: Acceptance requirement for initial name: one clan is linked to the intended player hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.001.case-02.2

Create clan / initial banner

- Entry/action: Clan screen / lord conversation; Perform create clan specifically in the initial banner context
- Preconditions: Isolated initial banner context for create clan
- Expected: Acceptance requirement for initial banner: one clan is linked to the intended player hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## clans.002 Inspect clan roster

Each entry retains authority and separate owner/observer observations in the CSV.

### clans.002.case-01

Inspect clan roster / family

- Entry/action: Clan screen / lord conversation; Perform inspect clan roster specifically in the family context
- Preconditions: Isolated family context for inspect clan roster
- Expected: Acceptance requirement for family: UI represents authoritative membership and assignments
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.002.case-02

Inspect clan roster / companions

- Entry/action: Clan screen / lord conversation; Perform inspect clan roster specifically in the companions context
- Preconditions: Isolated companions context for inspect clan roster
- Expected: Acceptance requirement for companions: UI represents authoritative membership and assignments
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.002.case-03

Inspect clan roster / leaders

- Entry/action: Clan screen / lord conversation; Perform inspect clan roster specifically in the leaders context
- Preconditions: Isolated leaders context for inspect clan roster
- Expected: Acceptance requirement for leaders: UI represents authoritative membership and assignments
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.002.case-04

Inspect clan roster / parties

- Entry/action: Clan screen / lord conversation; Perform inspect clan roster specifically in the parties context
- Preconditions: Isolated parties context for inspect clan roster
- Expected: Acceptance requirement for parties: UI represents authoritative membership and assignments
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## clans.003 Clan tier increase

Each entry retains authority and separate owner/observer observations in the CSV.

### clans.003.case-01

Clan tier increase / renown threshold

- Entry/action: Clan screen / lord conversation; Perform clan tier increase specifically in the renown threshold context
- Preconditions: Isolated renown threshold context for clan tier increase
- Expected: Acceptance requirement for renown threshold: tier and unlocked limits belong to the correct clan
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.003.case-02

Clan tier increase / multiple players

- Entry/action: Clan screen / lord conversation; Perform clan tier increase specifically in the multiple players context
- Preconditions: Isolated multiple players context for clan tier increase
- Expected: Acceptance requirement for multiple players: tier and unlocked limits belong to the correct clan
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clan.tier.1

Clan tier / renown 50

- Entry/action: Clan screen / lord conversation; Clan tier / renown 50
- Preconditions: Clan renown exactly 50
- Expected: Calculated tier 1
- State: Calculated result, not assignment here
- Side effects: Caller tier side effects must be mapped separately
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: One below threshold produces preceding tier within its range
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel@56-67
- Remaining: Model threshold inspected; co-op caller, tier effects and runtime remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### clan.tier.2

Clan tier / renown 150

- Entry/action: Clan screen / lord conversation; Clan tier / renown 150
- Preconditions: Clan renown exactly 150
- Expected: Calculated tier 2
- State: Calculated result, not assignment here
- Side effects: Caller tier side effects must be mapped separately
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: One below threshold produces preceding tier within its range
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel@56-67
- Remaining: Model threshold inspected; co-op caller, tier effects and runtime remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### clan.tier.3

Clan tier / renown 350

- Entry/action: Clan screen / lord conversation; Clan tier / renown 350
- Preconditions: Clan renown exactly 350
- Expected: Calculated tier 3
- State: Calculated result, not assignment here
- Side effects: Caller tier side effects must be mapped separately
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: One below threshold produces preceding tier within its range
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel@56-67
- Remaining: Model threshold inspected; co-op caller, tier effects and runtime remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### clan.tier.4

Clan tier / renown 900

- Entry/action: Clan screen / lord conversation; Clan tier / renown 900
- Preconditions: Clan renown exactly 900
- Expected: Calculated tier 4
- State: Calculated result, not assignment here
- Side effects: Caller tier side effects must be mapped separately
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: One below threshold produces preceding tier within its range
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel@56-67
- Remaining: Model threshold inspected; co-op caller, tier effects and runtime remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### clan.tier.5

Clan tier / renown 2350

- Entry/action: Clan screen / lord conversation; Clan tier / renown 2350
- Preconditions: Clan renown exactly 2350
- Expected: Calculated tier 5
- State: Calculated result, not assignment here
- Side effects: Caller tier side effects must be mapped separately
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: One below threshold produces preceding tier within its range
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel@56-67
- Remaining: Model threshold inspected; co-op caller, tier effects and runtime remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### clan.tier.6

Clan tier / renown 6150

- Entry/action: Clan screen / lord conversation; Clan tier / renown 6150
- Preconditions: Clan renown exactly 6150
- Expected: Calculated tier 6
- State: Calculated result, not assignment here
- Side effects: Caller tier side effects must be mapped separately
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: One below threshold produces preceding tier within its range
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel@56-67
- Remaining: Model threshold inspected; co-op caller, tier effects and runtime remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### clan.tier.1.below

Clan tier / one below 50

- Entry/action: Clan screen / lord conversation; Clan tier / one below 50
- Preconditions: Clan renown exactly 49
- Expected: Calculated tier 0
- State: Calculated output only; caller applies any durable state change
- Side effects: No clan tier assignment by this member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Renown exactly 50 reaches tier 1; tier side effects belong to callers
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel@56-67
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### clan.tier.2.below

Clan tier / one below 150

- Entry/action: Clan screen / lord conversation; Clan tier / one below 150
- Preconditions: Clan renown exactly 149
- Expected: Calculated tier 1
- State: Calculated output only; caller applies any durable state change
- Side effects: No clan tier assignment by this member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Renown exactly 150 reaches tier 2; tier side effects belong to callers
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel@56-67
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### clan.tier.3.below

Clan tier / one below 350

- Entry/action: Clan screen / lord conversation; Clan tier / one below 350
- Preconditions: Clan renown exactly 349
- Expected: Calculated tier 2
- State: Calculated output only; caller applies any durable state change
- Side effects: No clan tier assignment by this member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Renown exactly 350 reaches tier 3; tier side effects belong to callers
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel@56-67
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### clan.tier.4.below

Clan tier / one below 900

- Entry/action: Clan screen / lord conversation; Clan tier / one below 900
- Preconditions: Clan renown exactly 899
- Expected: Calculated tier 3
- State: Calculated output only; caller applies any durable state change
- Side effects: No clan tier assignment by this member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Renown exactly 900 reaches tier 4; tier side effects belong to callers
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel@56-67
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### clan.tier.5.below

Clan tier / one below 2350

- Entry/action: Clan screen / lord conversation; Clan tier / one below 2350
- Preconditions: Clan renown exactly 2349
- Expected: Calculated tier 4
- State: Calculated output only; caller applies any durable state change
- Side effects: No clan tier assignment by this member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Renown exactly 2350 reaches tier 5; tier side effects belong to callers
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel@56-67
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### clan.tier.6.below

Clan tier / one below 6150

- Entry/action: Clan screen / lord conversation; Clan tier / one below 6150
- Preconditions: Clan renown exactly 6149
- Expected: Calculated tier 5
- State: Calculated output only; caller applies any durable state change
- Side effects: No clan tier assignment by this member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Renown exactly 6150 reaches tier 6; tier side effects belong to callers
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel@56-67
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## clans.004 Gain renown

Each entry retains authority and separate owner/observer observations in the CSV.

### clans.004.case-01

Gain renown / battle

- Entry/action: Clan screen / lord conversation; Perform gain renown specifically in the battle context
- Preconditions: Isolated battle context for gain renown
- Expected: Acceptance requirement for battle: reward reaches the player/clan that earned it
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.004.case-02

Gain renown / tournament

- Entry/action: Clan screen / lord conversation; Perform gain renown specifically in the tournament context
- Preconditions: Isolated tournament context for gain renown
- Expected: Acceptance requirement for tournament: reward reaches the player/clan that earned it
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.004.case-03

Gain renown / quest

- Entry/action: Clan screen / lord conversation; Perform gain renown specifically in the quest context
- Preconditions: Isolated quest context for gain renown
- Expected: Acceptance requirement for quest: reward reaches the player/clan that earned it
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## clans.005 Gain or spend influence

Each entry retains authority and separate owner/observer observations in the CSV.

### clans.005.case-01

Gain or spend influence / battle

- Entry/action: Clan screen / lord conversation; Perform gain or spend influence specifically in the battle context
- Preconditions: Isolated battle context for gain or spend influence
- Expected: Acceptance requirement for battle: influence changes once for the accepted action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.005.case-02

Gain or spend influence / army

- Entry/action: Clan screen / lord conversation; Perform gain or spend influence specifically in the army context
- Preconditions: Isolated army context for gain or spend influence
- Expected: Acceptance requirement for army: influence changes once for the accepted action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.005.case-03

Gain or spend influence / vote

- Entry/action: Clan screen / lord conversation; Perform gain or spend influence specifically in the vote context
- Preconditions: Isolated vote context for gain or spend influence
- Expected: Acceptance requirement for vote: influence changes once for the accepted action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.005.case-04

Gain or spend influence / proposal

- Entry/action: Clan screen / lord conversation; Perform gain or spend influence specifically in the proposal context
- Preconditions: Isolated proposal context for gain or spend influence
- Expected: Acceptance requirement for proposal: influence changes once for the accepted action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## clans.006 Change clan leader

Each entry retains authority and separate owner/observer observations in the CSV.

### clans.006.case-01

Change clan leader / death

- Entry/action: Clan screen / lord conversation; Perform change clan leader specifically in the death context
- Preconditions: Isolated death context for change clan leader
- Expected: Acceptance requirement for death: membership and leader-dependent effects converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.006.case-02

Change clan leader / retirement

- Entry/action: Clan screen / lord conversation; Perform change clan leader specifically in the retirement context
- Preconditions: Isolated retirement context for change clan leader
- Expected: Acceptance requirement for retirement: membership and leader-dependent effects converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.006.case-03

Change clan leader / explicit selection

- Entry/action: Clan screen / lord conversation; Perform change clan leader specifically in the explicit selection context
- Preconditions: Isolated explicit selection context for change clan leader
- Expected: Acceptance requirement for explicit selection: membership and leader-dependent effects converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## clans.007 Join kingdom as vassal

Each entry retains authority and separate owner/observer observations in the CSV.

### clans.007.case-01

Join kingdom as vassal / oath

- Entry/action: Clan screen / lord conversation; Perform join kingdom as vassal specifically in the oath context
- Preconditions: Isolated oath context for join kingdom as vassal
- Expected: Acceptance requirement for oath: clan kingdom changes only for accepted valid entry
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.007.case-02

Join kingdom as vassal / invitation

- Entry/action: Clan screen / lord conversation; Perform join kingdom as vassal specifically in the invitation context
- Preconditions: Isolated invitation context for join kingdom as vassal
- Expected: Acceptance requirement for invitation: clan kingdom changes only for accepted valid entry
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.007.case-03.1

Join kingdom as vassal / eligible

- Entry/action: Clan screen / lord conversation; Perform join kingdom as vassal specifically in the eligible context
- Preconditions: Isolated eligible context for join kingdom as vassal
- Expected: Acceptance requirement for eligible: clan kingdom changes only for accepted valid entry
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.007.case-03.2

Join kingdom as vassal / ineligible

- Entry/action: Clan screen / lord conversation; Perform join kingdom as vassal specifically in the ineligible context
- Preconditions: Isolated ineligible context for join kingdom as vassal
- Expected: Acceptance requirement for ineligible: clan kingdom changes only for accepted valid entry
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## clans.008 Sign mercenary contract

Each entry retains authority and separate owner/observer observations in the CSV.

### clans.008.case-01

Sign mercenary contract / join

- Entry/action: Clan screen / lord conversation; Perform sign mercenary contract specifically in the join context
- Preconditions: Isolated join context for sign mercenary contract
- Expected: Acceptance requirement for join: contract state and payment match the correct clan
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.008.case-02

Sign mercenary contract / renew

- Entry/action: Clan screen / lord conversation; Perform sign mercenary contract specifically in the renew context
- Preconditions: Isolated renew context for sign mercenary contract
- Expected: Acceptance requirement for renew: contract state and payment match the correct clan
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.008.case-03

Sign mercenary contract / leave

- Entry/action: Clan screen / lord conversation; Perform sign mercenary contract specifically in the leave context
- Preconditions: Isolated leave context for sign mercenary contract
- Expected: Acceptance requirement for leave: contract state and payment match the correct clan
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## clans.009 Leave kingdom

Each entry retains authority and separate owner/observer observations in the CSV.

### clans.009.case-01

Leave kingdom / keep fiefs

- Entry/action: Clan screen / lord conversation; Perform leave kingdom specifically in the keep fiefs context
- Preconditions: Isolated keep fiefs context for leave kingdom
- Expected: Acceptance requirement for keep fiefs: clan/fief/diplomatic consequences reflect the accepted choice
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.009.case-02

Leave kingdom / relinquish

- Entry/action: Clan screen / lord conversation; Perform leave kingdom specifically in the relinquish context
- Preconditions: Isolated relinquish context for leave kingdom
- Expected: Acceptance requirement for relinquish: clan/fief/diplomatic consequences reflect the accepted choice
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.009.case-03

Leave kingdom / confirmation cancelled

- Entry/action: Clan screen / lord conversation; Perform leave kingdom specifically in the confirmation cancelled context
- Preconditions: Isolated confirmation cancelled context for leave kingdom
- Expected: Acceptance requirement for confirmation cancelled: clan/fief/diplomatic consequences reflect the accepted choice
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## clans.010 Clan finances

Each entry retains authority and separate owner/observer observations in the CSV.

### clans.010.case-01

Clan finances / party wages

- Entry/action: Clan screen / lord conversation; Perform clan finances specifically in the party wages context
- Preconditions: Isolated party wages context for clan finances
- Expected: Acceptance requirement for party wages: daily accounting belongs to the correct clan and is applied once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.010.case-02

Clan finances / workshop

- Entry/action: Clan screen / lord conversation; Perform clan finances specifically in the workshop context
- Preconditions: Isolated workshop context for clan finances
- Expected: Acceptance requirement for workshop: daily accounting belongs to the correct clan and is applied once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.010.case-03

Clan finances / caravan

- Entry/action: Clan screen / lord conversation; Perform clan finances specifically in the caravan context
- Preconditions: Isolated caravan context for clan finances
- Expected: Acceptance requirement for caravan: daily accounting belongs to the correct clan and is applied once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.010.case-04

Clan finances / tribute

- Entry/action: Clan screen / lord conversation; Perform clan finances specifically in the tribute context
- Preconditions: Isolated tribute context for clan finances
- Expected: Acceptance requirement for tribute: daily accounting belongs to the correct clan and is applied once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## clans.011 Clan destruction

Each entry retains authority and separate owner/observer observations in the CSV.

### clans.011.case-01

Clan destruction / last member

- Entry/action: Clan screen / lord conversation; Perform clan destruction specifically in the last member context
- Preconditions: Isolated last member context for clan destruction
- Expected: Acceptance requirement for last member: dependent ownership and kingdom membership are updated consistently
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### clans.011.case-02

Clan destruction / loss conditions

- Entry/action: Clan screen / lord conversation; Perform clan destruction specifically in the loss conditions context
- Preconditions: Isolated loss conditions context for clan destruction
- Expected: Acceptance requirement for loss conditions: dependent ownership and kingdom membership are updated consistently
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Clans](../../source/GameInterface/Services/Clans), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)
