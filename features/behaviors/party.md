# party behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## party.001 Recruit troops

Each entry retains authority and separate owner/observer observations in the CSV.

### party.001.case-01

Recruit troops / town

- Entry/action: Client party screen / campaign map; Perform recruit troops specifically in the town context
- Preconditions: Isolated town context for recruit troops
- Expected: Acceptance requirement for town: roster increases once and gold/volunteer stock decreases accordingly
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.001.case-02

Recruit troops / village

- Entry/action: Client party screen / campaign map; Perform recruit troops specifically in the village context
- Preconditions: Isolated village context for recruit troops
- Expected: Acceptance requirement for village: roster increases once and gold/volunteer stock decreases accordingly
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.001.case-03

Recruit troops / mercenary

- Entry/action: Client party screen / campaign map; Perform recruit troops specifically in the mercenary context
- Preconditions: Isolated mercenary context for recruit troops
- Expected: Acceptance requirement for mercenary: roster increases once and gold/volunteer stock decreases accordingly
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.001.case-04

Recruit troops / volunteer slots

- Entry/action: Client party screen / campaign map; Perform recruit troops specifically in the volunteer slots context
- Preconditions: Isolated volunteer slots context for recruit troops
- Expected: Acceptance requirement for volunteer slots: roster increases once and gold/volunteer stock decreases accordingly
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### menu.settlement.town.recruit_volunteers.select

Select town.recruit_volunteers

- Entry/action: Client campaign menu town; Choose the exact registered option recruit_volunteers
- Preconditions: The real menu town is reached; option recruit_volunteers condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_recruit_troops_on_condition; game_menu_recruit_volunteers_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@52-52
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### recruit.base.level-1

Recruitment quote / ordinary level 1

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 1
- Preconditions: Troop level 1; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 10
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-2

Recruitment quote / ordinary level 2

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 2
- Preconditions: Troop level 2; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 20
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-6

Recruitment quote / ordinary level 6

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 6
- Preconditions: Troop level 6; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 20
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-7

Recruitment quote / ordinary level 7

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 7
- Preconditions: Troop level 7; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 50
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-11

Recruitment quote / ordinary level 11

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 11
- Preconditions: Troop level 11; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 50
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-12

Recruitment quote / ordinary level 12

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 12
- Preconditions: Troop level 12; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 100
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-16

Recruitment quote / ordinary level 16

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 16
- Preconditions: Troop level 16; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 100
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-17

Recruitment quote / ordinary level 17

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 17
- Preconditions: Troop level 17; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 200
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-21

Recruitment quote / ordinary level 21

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 21
- Preconditions: Troop level 21; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 200
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-22

Recruitment quote / ordinary level 22

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 22
- Preconditions: Troop level 22; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 400
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-26

Recruitment quote / ordinary level 26

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 26
- Preconditions: Troop level 26; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 400
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-27

Recruitment quote / ordinary level 27

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 27
- Preconditions: Troop level 27; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 600
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-31

Recruitment quote / ordinary level 31

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 31
- Preconditions: Troop level 31; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 600
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-32

Recruitment quote / ordinary level 32

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 32
- Preconditions: Troop level 32; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 1000
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-36

Recruitment quote / ordinary level 36

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 36
- Preconditions: Troop level 36; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 1000
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.base.level-37

Recruitment quote / ordinary level 37

- Entry/action: Client party screen / campaign map; Recruitment quote / ordinary level 37
- Preconditions: Troop level 37; no equipment horse; ordinary occupation; buyerHero null
- Expected: Unmodified recruitment quote starts at 1500
- State: Calculated output only; caller applies any durable state change
- Side effects: No recruitment or gold transfer; buyer-specific reductions are absent in this case
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Crossing the next listed level boundary changes the base; troop Tier is a separate input from Level
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.horse.level-25.omit-0

Recruitment quote / horse item cost at level 25, omit False

- Entry/action: Client party screen / campaign map; Recruitment quote / horse item cost at level 25, omit False
- Preconditions: Equipment.Horse.Item nonnull; level 25; withoutItemCost False; buyerHero null; ordinary occupation
- Expected: Base quote 400; horse-item addition 150
- State: Calculated output only; caller applies any durable state change
- Side effects: Horse cost is a quote addition, not item consumption; no gold transfer in the model
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Level 26 takes the 500 branch, level 25 the 150 branch; withoutItemCost suppresses both
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.horse.level-26.omit-0

Recruitment quote / horse item cost at level 26, omit False

- Entry/action: Client party screen / campaign map; Recruitment quote / horse item cost at level 26, omit False
- Preconditions: Equipment.Horse.Item nonnull; level 26; withoutItemCost False; buyerHero null; ordinary occupation
- Expected: Base quote 400; horse-item addition 500
- State: Calculated output only; caller applies any durable state change
- Side effects: Horse cost is a quote addition, not item consumption; no gold transfer in the model
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Level 26 takes the 500 branch, level 25 the 150 branch; withoutItemCost suppresses both
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.horse.level-25.omit-1

Recruitment quote / horse item cost at level 25, omit True

- Entry/action: Client party screen / campaign map; Recruitment quote / horse item cost at level 25, omit True
- Preconditions: Equipment.Horse.Item nonnull; level 25; withoutItemCost True; buyerHero null; ordinary occupation
- Expected: Base quote 400; horse-item addition 0
- State: Calculated output only; caller applies any durable state change
- Side effects: Horse cost is a quote addition, not item consumption; no gold transfer in the model
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Level 26 takes the 500 branch, level 25 the 150 branch; withoutItemCost suppresses both
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### recruit.horse.level-26.omit-1

Recruitment quote / horse item cost at level 26, omit True

- Entry/action: Client party screen / campaign map; Recruitment quote / horse item cost at level 26, omit True
- Preconditions: Equipment.Horse.Item nonnull; level 26; withoutItemCost True; buyerHero null; ordinary occupation
- Expected: Base quote 400; horse-item addition 0
- State: Calculated output only; caller applies any durable state change
- Side effects: Horse cost is a quote addition, not item consumption; no gold transfer in the model
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Level 26 takes the 500 branch, level 25 the 150 branch; withoutItemCost suppresses both
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@214-287
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## party.002 Upgrade troops

Each entry retains authority and separate owner/observer observations in the CSV.

### party.002.case-01

Upgrade troops / eligible

- Entry/action: Client party screen / campaign map; Perform upgrade troops specifically in the eligible context
- Preconditions: Isolated eligible context for upgrade troops
- Expected: Acceptance requirement for eligible: chosen upgrade consumes the correct resources and preserves total troop count
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.002.case-02

Upgrade troops / insufficient XP

- Entry/action: Client party screen / campaign map; Perform upgrade troops specifically in the insufficient XP context
- Preconditions: Isolated insufficient XP context for upgrade troops
- Expected: Acceptance requirement for insufficient XP: chosen upgrade consumes the correct resources and preserves total troop count
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.002.case-03

Upgrade troops / insufficient gold

- Entry/action: Client party screen / campaign map; Perform upgrade troops specifically in the insufficient gold context
- Preconditions: Isolated insufficient gold context for upgrade troops
- Expected: Acceptance requirement for insufficient gold: chosen upgrade consumes the correct resources and preserves total troop count
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.002.case-04

Upgrade troops / mount requirement

- Entry/action: Client party screen / campaign map; Perform upgrade troops specifically in the mount requirement context
- Preconditions: Isolated mount requirement context for upgrade troops
- Expected: Acceptance requirement for mount requirement: chosen upgrade consumes the correct resources and preserves total troop count
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### up.hero

Upgrade / hero subject

- Entry/action: Client party screen / campaign map; Upgrade / hero subject
- Preconditions: Character is hero
- Expected: Condition false
- State: Condition false
- Side effects: No XP, gold or troop mutation
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hero eligibility is not inferred from upgrade target UI
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel@21-28
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### up.no-target

Upgrade / no target

- Entry/action: Client party screen / campaign map; Upgrade / no target
- Preconditions: Nonhero has zero upgrade targets
- Expected: Condition false
- State: Condition false
- Side effects: No resource mutation
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Adding a target changes this guard
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel@21-28
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### up.target

Upgrade / target available

- Entry/action: Client party screen / campaign map; Upgrade / target available
- Preconditions: Nonhero has at least one upgrade target
- Expected: Condition true
- State: Condition true
- Side effects: Resources checked separately
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: True here does not establish affordable upgrade
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel@21-28
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### up.no-item-category

Upgrade / no item requirement

- Entry/action: Client party screen / campaign map; Upgrade / no item requirement
- Preconditions: Target requires no item category
- Expected: Item requirement true
- State: Item requirement true
- Side effects: No item removed by condition
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: XP/gold/perk gates remain distinct
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel@107-124
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### up.has-item-category

Upgrade / required category present

- Entry/action: Client party screen / campaign map; Upgrade / required category present
- Preconditions: Positive summed quantity in exact required item category
- Expected: Item requirement true
- State: Item requirement true
- Side effects: Actual consumption belongs to caller
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Wrong category does not count
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel@107-124
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### up.missing-item-category

Upgrade / required category absent

- Entry/action: Client party screen / campaign map; Upgrade / required category absent
- Preconditions: Summed required category count zero
- Expected: Item requirement false
- State: Item requirement false
- Side effects: No roster mutation
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Zero fails strict positive-count test
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel@107-124
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### up.bandit-perk

Upgrade / bandit to nonbandit

- Entry/action: Client party screen / campaign map; Upgrade / bandit to nonbandit
- Preconditions: Source culture bandit; target culture nonbandit
- Expected: VeteransRespect required through secondary-role party check
- State: VeteransRespect required through secondary-role party check
- Side effects: requiredPerk output assigned; no resources consumed here
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Other cultural transitions return true from this guard
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel@126-135
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### up.xp-tier-two

Upgrade / XP to tier 2

- Entry/action: Client party screen / campaign map; Upgrade / XP to tier 2
- Preconditions: Valid target; source tier 1, target tier 2
- Expected: Cost 300
- State: Cost 300
- Side effects: Computed cost only
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Actual XP availability and consumption belong to caller
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel@30-74
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### up.xp-invalid

Upgrade / invalid target

- Entry/action: Client party screen / campaign map; Upgrade / invalid target
- Preconditions: Null target or outside source UpgradeTargets
- Expected: Cost sentinel 100000000
- State: Cost sentinel 100000000
- Side effects: No XP consumed by model
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Valid targets use tier accumulation
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel@30-74
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### up.xp-tier-six

Upgrade / XP to tier 6

- Entry/action: Client party screen / campaign map; Upgrade / XP to tier 6
- Preconditions: Valid target; source tier 5, target tier 6
- Expected: Cost 1700
- State: Cost 1700
- Side effects: Computed cost only
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Multi-tier targets accumulate separate increments
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel@30-74
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### upgrade.skill-xp.count-0

Upgrade skill XP / 0 level-10 troops

- Entry/action: Client party screen / campaign map; Upgrade skill XP / 0 level-10 troops
- Preconditions: Troop Level 10; numberOfTroops 0
- Expected: Model XP result 0
- State: Calculated output only; caller applies any durable state change
- Side effects: No XP award or roster mutation here
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Formula uses Level, not Tier; actual XP recipient and award are separate production effects
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel@102-105
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### upgrade.skill-xp.count-1

Upgrade skill XP / 1 level-10 troops

- Entry/action: Client party screen / campaign map; Upgrade skill XP / 1 level-10 troops
- Preconditions: Troop Level 10; numberOfTroops 1
- Expected: Model XP result 20
- State: Calculated output only; caller applies any durable state change
- Side effects: No XP award or roster mutation here
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Formula uses Level, not Tier; actual XP recipient and award are separate production effects
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel@102-105
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### upgrade.skill-xp.count-3

Upgrade skill XP / 3 level-10 troops

- Entry/action: Client party screen / campaign map; Upgrade skill XP / 3 level-10 troops
- Preconditions: Troop Level 10; numberOfTroops 3
- Expected: Model XP result 60
- State: Calculated output only; caller applies any durable state change
- Side effects: No XP award or roster mutation here
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Formula uses Level, not Tier; actual XP recipient and award are separate production effects
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel@102-105
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## party.003 Transfer troops

Each entry retains authority and separate owner/observer observations in the CSV.

### party.003.case-01

Transfer troops / garrison

- Entry/action: Client party screen / campaign map; Perform transfer troops specifically in the garrison context
- Preconditions: Isolated garrison context for transfer troops
- Expected: Acceptance requirement for garrison: source and destination rosters balance after the accepted transfer
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.003.case-02

Transfer troops / companion party

- Entry/action: Client party screen / campaign map; Perform transfer troops specifically in the companion party context
- Preconditions: Isolated companion party context for transfer troops
- Expected: Acceptance requirement for companion party: source and destination rosters balance after the accepted transfer
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.003.case-03

Transfer troops / dungeon context

- Entry/action: Client party screen / campaign map; Perform transfer troops specifically in the dungeon context context
- Preconditions: Isolated dungeon context context for transfer troops
- Expected: Acceptance requirement for dungeon context: source and destination rosters balance after the accepted transfer
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.004 Dismiss troops

Each entry retains authority and separate owner/observer observations in the CSV.

### party.004.case-01

Dismiss troops / one

- Entry/action: Client party screen / campaign map; Perform dismiss troops specifically in the one context
- Preconditions: Isolated one context for dismiss troops
- Expected: Acceptance requirement for one: selected troops leave once and roster totals remain consistent
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.004.case-02

Dismiss troops / stack

- Entry/action: Client party screen / campaign map; Perform dismiss troops specifically in the stack context
- Preconditions: Isolated stack context for dismiss troops
- Expected: Acceptance requirement for stack: selected troops leave once and roster totals remain consistent
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.004.case-03

Dismiss troops / wounded troops

- Entry/action: Client party screen / campaign map; Perform dismiss troops specifically in the wounded troops context
- Preconditions: Isolated wounded troops context for dismiss troops
- Expected: Acceptance requirement for wounded troops: selected troops leave once and roster totals remain consistent
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.005 Reorder party roster

Each entry retains authority and separate owner/observer observations in the CSV.

### party.005.case-01

Reorder party roster / hero

- Entry/action: Client party screen / campaign map; Perform reorder party roster specifically in the hero context
- Preconditions: Isolated hero context for reorder party roster
- Expected: Acceptance requirement for hero: saved roster ordering matches the player's selection
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.005.case-02

Reorder party roster / troop stack

- Entry/action: Client party screen / campaign map; Perform reorder party roster specifically in the troop stack context
- Preconditions: Isolated troop stack context for reorder party roster
- Expected: Acceptance requirement for troop stack: saved roster ordering matches the player's selection
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.005.case-03

Reorder party roster / formation preference

- Entry/action: Client party screen / campaign map; Perform reorder party roster specifically in the formation preference context
- Preconditions: Isolated formation preference context for reorder party roster
- Expected: Acceptance requirement for formation preference: saved roster ordering matches the player's selection
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.006 Inspect troop details

Each entry retains authority and separate owner/observer observations in the CSV.

### party.006.case-01

Inspect troop details / healthy

- Entry/action: Client party screen / campaign map; Perform inspect troop details specifically in the healthy context
- Preconditions: Isolated healthy context for inspect troop details
- Expected: Acceptance requirement for healthy: UI totals agree with authoritative roster state
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.006.case-02

Inspect troop details / wounded

- Entry/action: Client party screen / campaign map; Perform inspect troop details specifically in the wounded context
- Preconditions: Isolated wounded context for inspect troop details
- Expected: Acceptance requirement for wounded: UI totals agree with authoritative roster state
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.006.case-03

Inspect troop details / XP

- Entry/action: Client party screen / campaign map; Perform inspect troop details specifically in the XP context
- Preconditions: Isolated XP context for inspect troop details
- Expected: Acceptance requirement for XP: UI totals agree with authoritative roster state
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.006.case-04

Inspect troop details / upgrade targets

- Entry/action: Client party screen / campaign map; Perform inspect troop details specifically in the upgrade targets context
- Preconditions: Isolated upgrade targets context for inspect troop details
- Expected: Acceptance requirement for upgrade targets: UI totals agree with authoritative roster state
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.007 Recruit prisoners

Each entry retains authority and separate owner/observer observations in the CSV.

### party.007.case-01

Recruit prisoners / available

- Entry/action: Client party screen / campaign map; Perform recruit prisoners specifically in the available context
- Preconditions: Isolated available context for recruit prisoners
- Expected: Acceptance requirement for available: only permitted prisoners become troops and prisoner totals decrease
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.007.case-02

Recruit prisoners / insufficient conformity

- Entry/action: Client party screen / campaign map; Perform recruit prisoners specifically in the insufficient conformity context
- Preconditions: Isolated insufficient conformity context for recruit prisoners
- Expected: Acceptance requirement for insufficient conformity: only permitted prisoners become troops and prisoner totals decrease
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.007.case-03

Recruit prisoners / hero prisoner

- Entry/action: Client party screen / campaign map; Perform recruit prisoners specifically in the hero prisoner context
- Preconditions: Isolated hero prisoner context for recruit prisoners
- Expected: Acceptance requirement for hero prisoner: only permitted prisoners become troops and prisoner totals decrease
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.008 Ransom prisoners

Each entry retains authority and separate owner/observer observations in the CSV.

### party.008.case-01

Ransom prisoners / ordinary troops

- Entry/action: Client party screen / campaign map; Perform ransom prisoners specifically in the ordinary troops context
- Preconditions: Isolated ordinary troops context for ransom prisoners
- Expected: Acceptance requirement for ordinary troops: gold and prisoner removal reflect one completed ransom
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.008.case-02

Ransom prisoners / hero

- Entry/action: Client party screen / campaign map; Perform ransom prisoners specifically in the hero context
- Preconditions: Isolated hero context for ransom prisoners
- Expected: Acceptance requirement for hero: gold and prisoner removal reflect one completed ransom
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.008.case-03

Ransom prisoners / selected stack

- Entry/action: Client party screen / campaign map; Perform ransom prisoners specifically in the selected stack context
- Preconditions: Isolated selected stack context for ransom prisoners
- Expected: Acceptance requirement for selected stack: gold and prisoner removal reflect one completed ransom
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.009 Transfer prisoners

Each entry retains authority and separate owner/observer observations in the CSV.

### party.009.case-01

Transfer prisoners / party to dungeon

- Entry/action: Client party screen / campaign map; Perform transfer prisoners specifically in the party to dungeon context
- Preconditions: Isolated party to dungeon context for transfer prisoners
- Expected: Acceptance requirement for party to dungeon: both prisoner rosters conserve the transferred count
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.009.case-02

Transfer prisoners / dungeon to party

- Entry/action: Client party screen / campaign map; Perform transfer prisoners specifically in the dungeon to party context
- Preconditions: Isolated dungeon to party context for transfer prisoners
- Expected: Acceptance requirement for dungeon to party: both prisoner rosters conserve the transferred count
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.010 Troop healing

Each entry retains authority and separate owner/observer observations in the CSV.

### party.010.case-01

Troop healing / wounded

- Entry/action: Client party screen / campaign map; Perform troop healing specifically in the wounded context
- Preconditions: Isolated wounded context for troop healing
- Expected: Acceptance requirement for wounded: healthy/wounded totals follow observed healing ticks
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.010.case-02

Troop healing / settlement rest

- Entry/action: Client party screen / campaign map; Perform troop healing specifically in the settlement rest context
- Preconditions: Isolated settlement rest context for troop healing
- Expected: Acceptance requirement for settlement rest: healthy/wounded totals follow observed healing ticks
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.010.case-03

Troop healing / surgeon role

- Entry/action: Client party screen / campaign map; Perform troop healing specifically in the surgeon role context
- Preconditions: Isolated surgeon role context for troop healing
- Expected: Acceptance requirement for surgeon role: healthy/wounded totals follow observed healing ticks
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.011 Troop casualties

Each entry retains authority and separate owner/observer observations in the CSV.

### party.011.case-01

Troop casualties / battle

- Entry/action: Client party screen / campaign map; Perform troop casualties specifically in the battle context
- Preconditions: Isolated battle context for troop casualties
- Expected: Acceptance requirement for battle: casualty types and resulting roster counts converge
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.011.case-02

Troop casualties / simulation

- Entry/action: Client party screen / campaign map; Perform troop casualties specifically in the simulation context
- Preconditions: Isolated simulation context for troop casualties
- Expected: Acceptance requirement for simulation: casualty types and resulting roster counts converge
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.011.case-03

Troop casualties / raid

- Entry/action: Client party screen / campaign map; Perform troop casualties specifically in the raid context
- Preconditions: Isolated raid context for troop casualties
- Expected: Acceptance requirement for raid: casualty types and resulting roster counts converge
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.011.case-04

Troop casualties / starvation

- Entry/action: Client party screen / campaign map; Perform troop casualties specifically in the starvation context
- Preconditions: Isolated starvation context for troop casualties
- Expected: Acceptance requirement for starvation: casualty types and resulting roster counts converge
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.012 Party food consumption

Each entry retains authority and separate owner/observer observations in the CSV.

### party.012.case-01

Party food consumption / food present

- Entry/action: Client party screen / campaign map; Perform party food consumption specifically in the food present context
- Preconditions: Isolated food present context for party food consumption
- Expected: Acceptance requirement for food present: consumed items and party food state agree after the tick
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.012.case-02

Party food consumption / no food

- Entry/action: Client party screen / campaign map; Perform party food consumption specifically in the no food context
- Preconditions: Isolated no food context for party food consumption
- Expected: Acceptance requirement for no food: consumed items and party food state agree after the tick
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.012.case-03

Party food consumption / multiple food types

- Entry/action: Client party screen / campaign map; Perform party food consumption specifically in the multiple food types context
- Preconditions: Isolated multiple food types context for party food consumption
- Expected: Acceptance requirement for multiple food types: consumed items and party food state agree after the tick
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.013 Party morale

Each entry retains authority and separate owner/observer observations in the CSV.

### party.013.case-01

Party morale / food diversity

- Entry/action: Client party screen / campaign map; Perform party morale specifically in the food diversity context
- Preconditions: Isolated food diversity context for party morale
- Expected: Acceptance requirement for food diversity: morale and consequences match the applicable party state
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.013.case-02

Party morale / wages

- Entry/action: Client party screen / campaign map; Perform party morale specifically in the wages context
- Preconditions: Isolated wages context for party morale
- Expected: Acceptance requirement for wages: morale and consequences match the applicable party state
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.013.case-03

Party morale / battles

- Entry/action: Client party screen / campaign map; Perform party morale specifically in the battles context
- Preconditions: Isolated battles context for party morale
- Expected: Acceptance requirement for battles: morale and consequences match the applicable party state
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.013.case-04

Party morale / recent events

- Entry/action: Client party screen / campaign map; Perform party morale specifically in the recent events context
- Preconditions: Isolated recent events context for party morale
- Expected: Acceptance requirement for recent events: morale and consequences match the applicable party state
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.014 Pay party wages

Each entry retains authority and separate owner/observer observations in the CSV.

### party.014.case-01

Pay party wages / sufficient funds

- Entry/action: Client party screen / campaign map; Perform pay party wages specifically in the sufficient funds context
- Preconditions: Isolated sufficient funds context for pay party wages
- Expected: Acceptance requirement for sufficient funds: gold and wage-related consequences occur once per authoritative tick
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.014.case-02

Pay party wages / insufficient funds

- Entry/action: Client party screen / campaign map; Perform pay party wages specifically in the insufficient funds context
- Preconditions: Isolated insufficient funds context for pay party wages
- Expected: Acceptance requirement for insufficient funds: gold and wage-related consequences occur once per authoritative tick
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.014.case-03

Pay party wages / daily tick

- Entry/action: Client party screen / campaign map; Perform pay party wages specifically in the daily tick context
- Preconditions: Isolated daily tick context for pay party wages
- Expected: Acceptance requirement for daily tick: gold and wage-related consequences occur once per authoritative tick
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### wage.ordinary.tier-0

Individual wage / ordinary tier 0

- Entry/action: Client party screen / campaign map; Individual wage / ordinary tier 0
- Preconditions: Character tier 0; Occupation not Mercenary
- Expected: Individual wage result 1
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.mercenary.tier-0

Individual wage / mercenary tier 0

- Entry/action: Client party screen / campaign map; Individual wage / mercenary tier 0
- Preconditions: Character tier 0; Occupation Mercenary
- Expected: Individual wage result 1
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.ordinary.tier-1

Individual wage / ordinary tier 1

- Entry/action: Client party screen / campaign map; Individual wage / ordinary tier 1
- Preconditions: Character tier 1; Occupation not Mercenary
- Expected: Individual wage result 2
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.mercenary.tier-1

Individual wage / mercenary tier 1

- Entry/action: Client party screen / campaign map; Individual wage / mercenary tier 1
- Preconditions: Character tier 1; Occupation Mercenary
- Expected: Individual wage result 3
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.ordinary.tier-2

Individual wage / ordinary tier 2

- Entry/action: Client party screen / campaign map; Individual wage / ordinary tier 2
- Preconditions: Character tier 2; Occupation not Mercenary
- Expected: Individual wage result 3
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.mercenary.tier-2

Individual wage / mercenary tier 2

- Entry/action: Client party screen / campaign map; Individual wage / mercenary tier 2
- Preconditions: Character tier 2; Occupation Mercenary
- Expected: Individual wage result 4
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.ordinary.tier-3

Individual wage / ordinary tier 3

- Entry/action: Client party screen / campaign map; Individual wage / ordinary tier 3
- Preconditions: Character tier 3; Occupation not Mercenary
- Expected: Individual wage result 5
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.mercenary.tier-3

Individual wage / mercenary tier 3

- Entry/action: Client party screen / campaign map; Individual wage / mercenary tier 3
- Preconditions: Character tier 3; Occupation Mercenary
- Expected: Individual wage result 7
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.ordinary.tier-4

Individual wage / ordinary tier 4

- Entry/action: Client party screen / campaign map; Individual wage / ordinary tier 4
- Preconditions: Character tier 4; Occupation not Mercenary
- Expected: Individual wage result 8
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.mercenary.tier-4

Individual wage / mercenary tier 4

- Entry/action: Client party screen / campaign map; Individual wage / mercenary tier 4
- Preconditions: Character tier 4; Occupation Mercenary
- Expected: Individual wage result 12
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.ordinary.tier-5

Individual wage / ordinary tier 5

- Entry/action: Client party screen / campaign map; Individual wage / ordinary tier 5
- Preconditions: Character tier 5; Occupation not Mercenary
- Expected: Individual wage result 12
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.mercenary.tier-5

Individual wage / mercenary tier 5

- Entry/action: Client party screen / campaign map; Individual wage / mercenary tier 5
- Preconditions: Character tier 5; Occupation Mercenary
- Expected: Individual wage result 18
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.ordinary.tier-6

Individual wage / ordinary tier 6

- Entry/action: Client party screen / campaign map; Individual wage / ordinary tier 6
- Preconditions: Character tier 6; Occupation not Mercenary
- Expected: Individual wage result 17
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.mercenary.tier-6

Individual wage / mercenary tier 6

- Entry/action: Client party screen / campaign map; Individual wage / mercenary tier 6
- Preconditions: Character tier 6; Occupation Mercenary
- Expected: Individual wage result 25
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.ordinary.tier-7

Individual wage / ordinary tier 7

- Entry/action: Client party screen / campaign map; Individual wage / ordinary tier 7
- Preconditions: Character tier 7; Occupation not Mercenary
- Expected: Individual wage result 23
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### wage.mercenary.tier-7

Individual wage / mercenary tier 7

- Entry/action: Client party screen / campaign map; Individual wage / mercenary tier 7
- Preconditions: Character tier 7; Occupation Mercenary
- Expected: Individual wage result 34
- State: Calculated output only; caller applies any durable state change
- Side effects: No party payment or treasury mutation by this model member
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Party aggregation, hero exemptions, perks and policy modifiers are separate paths; tiers outside 0 through 6 use the default base 23
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel@23-41
- Remaining: Complete member interpreted; source-bound co-op caller, effect propagation and runtime measurements remain unresolved

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## party.015 Party capacity

Each entry retains authority and separate owner/observer observations in the CSV.

### party.015.case-01

Party capacity / below cap

- Entry/action: Client party screen / campaign map; Perform party capacity specifically in the below cap context
- Preconditions: Isolated below cap context for party capacity
- Expected: Acceptance requirement for below cap: displayed limit and overcrowding consequences match the current model
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.015.case-02

Party capacity / at cap

- Entry/action: Client party screen / campaign map; Perform party capacity specifically in the at cap context
- Preconditions: Isolated at cap context for party capacity
- Expected: Acceptance requirement for at cap: displayed limit and overcrowding consequences match the current model
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.015.case-03

Party capacity / over cap

- Entry/action: Client party screen / campaign map; Perform party capacity specifically in the over cap context
- Preconditions: Isolated over cap context for party capacity
- Expected: Acceptance requirement for over cap: displayed limit and overcrowding consequences match the current model
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.016 Party trade gold

Each entry retains authority and separate owner/observer observations in the CSV.

### party.016.case-01

Party trade gold / trade

- Entry/action: Client party screen / campaign map; Perform party trade gold specifically in the trade context
- Preconditions: Isolated trade context for party trade gold
- Expected: Acceptance requirement for trade: party funds converge separately from hero funds
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.016.case-02

Party trade gold / daily income

- Entry/action: Client party screen / campaign map; Perform party trade gold specifically in the daily income context
- Preconditions: Isolated daily income context for party trade gold
- Expected: Acceptance requirement for daily income: party funds converge separately from hero funds
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.016.case-03

Party trade gold / gold transfer

- Entry/action: Client party screen / campaign map; Perform party trade gold specifically in the gold transfer context
- Preconditions: Isolated gold transfer context for party trade gold
- Expected: Acceptance requirement for gold transfer: party funds converge separately from hero funds
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.017 Party role assignment

Each entry retains authority and separate owner/observer observations in the CSV.

### party.017.case-01

Party role assignment / scout

- Entry/action: Client party screen / campaign map; Perform party role assignment specifically in the scout context
- Preconditions: Isolated scout context for party role assignment
- Expected: Acceptance requirement for scout: assigned hero and effective role are consistent after replacement
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.017.case-02

Party role assignment / surgeon

- Entry/action: Client party screen / campaign map; Perform party role assignment specifically in the surgeon context
- Preconditions: Isolated surgeon context for party role assignment
- Expected: Acceptance requirement for surgeon: assigned hero and effective role are consistent after replacement
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.017.case-03

Party role assignment / engineer

- Entry/action: Client party screen / campaign map; Perform party role assignment specifically in the engineer context
- Preconditions: Isolated engineer context for party role assignment
- Expected: Acceptance requirement for engineer: assigned hero and effective role are consistent after replacement
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.017.case-04

Party role assignment / quartermaster

- Entry/action: Client party screen / campaign map; Perform party role assignment specifically in the quartermaster context
- Preconditions: Isolated quartermaster context for party role assignment
- Expected: Acceptance requirement for quartermaster: assigned hero and effective role are consistent after replacement
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.018 Create companion party

Each entry retains authority and separate owner/observer observations in the CSV.

### party.018.case-01

Create companion party / eligible companion

- Entry/action: Client party screen / campaign map; Perform create companion party specifically in the eligible companion context
- Preconditions: Isolated eligible companion context for create companion party
- Expected: Acceptance requirement for eligible companion: new party has one owner and registered component/rosters
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.018.case-02

Create companion party / party limit

- Entry/action: Client party screen / campaign map; Perform create companion party specifically in the party limit context
- Preconditions: Isolated party limit context for create companion party
- Expected: Acceptance requirement for party limit: new party has one owner and registered component/rosters
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.019 Disband companion party

Each entry retains authority and separate owner/observer observations in the CSV.

### party.019.case-01

Disband companion party / travelling

- Entry/action: Client party screen / campaign map; Perform disband companion party specifically in the travelling context
- Preconditions: Isolated travelling context for disband companion party
- Expected: Acceptance requirement for travelling: party reaches the correct disband state without a zombie party
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.019.case-02

Disband companion party / returning

- Entry/action: Client party screen / campaign map; Perform disband companion party specifically in the returning context
- Preconditions: Isolated returning context for disband companion party
- Expected: Acceptance requirement for returning: party reaches the correct disband state without a zombie party
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.019.case-03

Disband companion party / owner change

- Entry/action: Client party screen / campaign map; Perform disband companion party specifically in the owner change context
- Preconditions: Isolated owner change context for disband companion party
- Expected: Acceptance requirement for owner change: party reaches the correct disband state without a zombie party
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## party.020 Party wage limit

Each entry retains authority and separate owner/observer observations in the CSV.

### party.020.case-01

Party wage limit / limit edit

- Entry/action: Client party screen / campaign map; Perform party wage limit specifically in the limit edit context
- Preconditions: Isolated limit edit context for party wage limit
- Expected: Acceptance requirement for limit edit: wage limit controls recruitment as the source specifies
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### party.020.case-02

Party wage limit / automatic recruitment

- Entry/action: Client party screen / campaign map; Perform party wage limit specifically in the automatic recruitment context
- Preconditions: Isolated automatic recruitment context for party wage limit
- Expected: Acceptance requirement for automatic recruitment: wage limit controls recruitment as the source specifies
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

Sources: [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party), [source/GameInterface/Services/TroopRosters](../../source/GameInterface/Services/TroopRosters), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)
