# naval behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## naval.001 Embark party

Each entry retains authority and separate owner/observer observations in the CSV.

### naval.001.case-01

Embark party / port

- Entry/action: Installed naval menu paths / optional mod mission module; Perform embark party specifically in the port context
- Preconditions: Isolated port context for embark party
- Expected: Acceptance requirement for port: embark path exists for the enabled module set and retains party identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.001.case-02

Embark party / shoreline where allowed

- Entry/action: Installed naval menu paths / optional mod mission module; Perform embark party specifically in the shoreline where allowed context
- Preconditions: Isolated shoreline where allowed context for embark party
- Expected: Acceptance requirement for shoreline where allowed: embark path exists for the enabled module set and retains party identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.001.case-03

Embark party / ships available

- Entry/action: Installed naval menu paths / optional mod mission module; Perform embark party specifically in the ships available context
- Preconditions: Isolated ships available context for embark party
- Expected: Acceptance requirement for ships available: embark path exists for the enabled module set and retains party identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.village.leave_set_sail.select

Select village.leave_set_sail

- Entry/action: Client campaign menu village; Choose the exact registered option leave_set_sail
- Preconditions: The real menu village is reached; option leave_set_sail condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_set_sail_leave_on_condition; game_menu_village_set_sail_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unsupported-release-naval
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@160-160
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.village.leave_at_sea.select

Select village.leave_at_sea

- Entry/action: Client campaign menu village; Choose the exact registered option leave_at_sea
- Preconditions: The real menu village is reached; option leave_at_sea condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_leave_at_sea_on_condition; game_menu_village_set_sail_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unsupported-release-naval
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@161-161
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.village_looted.leave_set_sail.select

Select village_looted.leave_set_sail

- Entry/action: Client campaign menu village_looted; Choose the exact registered option leave_set_sail
- Preconditions: The real menu village_looted is reached; option leave_set_sail condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_set_sail_leave_on_condition; game_menu_village_set_sail_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unsupported-release-naval
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@164-164
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## naval.002 Disembark party

Each entry retains authority and separate owner/observer observations in the CSV.

### naval.002.case-01

Disembark party / port

- Entry/action: Installed naval menu paths / optional mod mission module; Perform disembark party specifically in the port context
- Preconditions: Isolated port context for disembark party
- Expected: Acceptance requirement for port: accepted landing produces one authoritative land party
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.002.case-02

Disembark party / landing

- Entry/action: Installed naval menu paths / optional mod mission module; Perform disembark party specifically in the landing context
- Preconditions: Isolated landing context for disembark party
- Expected: Acceptance requirement for landing: accepted landing produces one authoritative land party
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.002.case-03

Disembark party / invalid location

- Entry/action: Installed naval menu paths / optional mod mission module; Perform disembark party specifically in the invalid location context
- Preconditions: Isolated invalid location context for disembark party
- Expected: Acceptance requirement for invalid location: accepted landing produces one authoritative land party
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.village.disembark.select

Select village.disembark

- Entry/action: Client campaign menu village; Choose the exact registered option disembark
- Preconditions: The real menu village is reached; option disembark condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_disembark_on_condition; game_menu_village_disembark_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unsupported-release-naval
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@159-159
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.village_looted.disembark.select

Select village_looted.disembark

- Entry/action: Client campaign menu village_looted; Choose the exact registered option disembark
- Preconditions: The real menu village_looted is reached; option disembark condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_disembark_on_condition; game_menu_village_disembark_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unsupported-release-naval
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@163-163
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## naval.003 Naval campaign movement

Each entry retains authority and separate owner/observer observations in the CSV.

### naval.003.case-01

Naval campaign movement / sea route

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval campaign movement specifically in the sea route context
- Preconditions: Isolated sea route context for naval campaign movement
- Expected: Acceptance requirement for sea route: party movement and ship identity converge where naval opt-in is active
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.003.case-02

Naval campaign movement / destination

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval campaign movement specifically in the destination context
- Preconditions: Isolated destination context for naval campaign movement
- Expected: Acceptance requirement for destination: party movement and ship identity converge where naval opt-in is active
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.003.case-03

Naval campaign movement / retreat

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval campaign movement specifically in the retreat context
- Preconditions: Isolated retreat context for naval campaign movement
- Expected: Acceptance requirement for retreat: party movement and ship identity converge where naval opt-in is active
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

## naval.004 Naval encounter

Each entry retains authority and separate owner/observer observations in the CSV.

### naval.004.case-01

Naval encounter / hostile fleet

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval encounter specifically in the hostile fleet context
- Preconditions: Isolated hostile fleet context for naval encounter
- Expected: Acceptance requirement for hostile fleet: current encounter side and outcome agree for enabled naval support
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.004.case-02

Naval encounter / join ally

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval encounter specifically in the join ally context
- Preconditions: Isolated join ally context for naval encounter
- Expected: Acceptance requirement for join ally: current encounter side and outcome agree for enabled naval support
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.004.case-03

Naval encounter / disengage

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval encounter specifically in the disengage context
- Preconditions: Isolated disengage context for naval encounter
- Expected: Acceptance requirement for disengage: current encounter side and outcome agree for enabled naval support
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.naval_town_outside.attack_the_blockade.select

Select naval_town_outside.attack_the_blockade

- Entry/action: Client campaign menu naval_town_outside; Choose the exact registered option attack_the_blockade
- Preconditions: The real menu naval_town_outside is reached; option attack_the_blockade condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks attack_blockade_besieger_side_on_condition; attack_blockade_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unsupported-release-naval
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@132-132
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.naval_town_outside.join_siege_defender.select

Select naval_town_outside.join_siege_defender

- Entry/action: Client campaign menu naval_town_outside; Choose the exact registered option join_siege_defender
- Preconditions: The real menu naval_town_outside is reached; option join_siege_defender condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks attack_blockade_besieger_side_break_in_on_condition; game_menu_join_siege_event_on_defender_side_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unsupported-release-naval
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@133-133
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.naval_town_outside.join_encounter_leave.select

Select naval_town_outside.join_encounter_leave

- Entry/action: Client campaign menu naval_town_outside; Choose the exact registered option join_encounter_leave
- Preconditions: The real menu naval_town_outside is reached; option join_encounter_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_leave_on_condition; game_menu_town_naval_outside_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unsupported-release-naval
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@134-134
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.besiegers_lift_the_blockade.continue.select

Select besiegers_lift_the_blockade.continue

- Entry/action: Client campaign menu besiegers_lift_the_blockade; Choose the exact registered option continue
- Preconditions: The real menu besiegers_lift_the_blockade is reached; option continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_try_to_get_away_continue_on_condition; break_in_debrief_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unsupported-release-naval
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@147-147
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.menu_siege_strategies.menu_siege_strategies_break_out_from_port.select

Select menu_siege_strategies.menu_siege_strategies_break_out_from_port

- Entry/action: Client campaign menu menu_siege_strategies; Choose the exact registered option menu_siege_strategies_break_out_from_port
- Preconditions: The real menu menu_siege_strategies is reached; option menu_siege_strategies_break_out_from_port condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks menu_defender_siege_break_out_from_port_on_condition; menu_defender_siege_break_out_from_port_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unsupported-release-naval
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@149-149
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.menu_siege_strategies.menu_siege_strategies_sally_out_from_port.select

Select menu_siege_strategies.menu_siege_strategies_sally_out_from_port

- Entry/action: Client campaign menu menu_siege_strategies; Choose the exact registered option menu_siege_strategies_sally_out_from_port
- Preconditions: The real menu menu_siege_strategies is reached; option menu_siege_strategies_sally_out_from_port condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks menu_sally_out_from_port_on_condition; menu_sally_out_naval_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unsupported-release-naval
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@151-151
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.naval_encounter_disengaged.naval_encounter_disengaged_continue.select

Select naval_encounter_disengaged.naval_encounter_disengaged_continue

- Entry/action: Client campaign menu naval_encounter_disengaged; Choose the exact registered option naval_encounter_disengaged_continue
- Preconditions: The real menu naval_encounter_disengaged is reached; option naval_encounter_disengaged_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks naval_encounter_disengage_condition; naval_encounter_disengaged_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unsupported-release-naval
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@311-311
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## naval.005 Naval battle entry

Each entry retains authority and separate owner/observer observations in the CSV.

### naval.005.case-01

Naval battle entry / ship selection

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval battle entry specifically in the ship selection context
- Preconditions: Isolated ship selection context for naval battle entry
- Expected: Acceptance requirement for ship selection: players board the intended registered vessels
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.005.case-02.1

Naval battle entry / player ownership

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval battle entry specifically in the player ownership context
- Preconditions: Isolated player ownership context for naval battle entry
- Expected: Acceptance requirement for player ownership: players board the intended registered vessels
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.005.case-02.2

Naval battle entry / team ownership

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval battle entry specifically in the team ownership context
- Preconditions: Isolated team ownership context for naval battle entry
- Expected: Acceptance requirement for team ownership: players board the intended registered vessels
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

## naval.006 Ship movement

Each entry retains authority and separate owner/observer observations in the CSV.

### naval.006.case-01

Ship movement / course

- Entry/action: Installed naval menu paths / optional mod mission module; Perform ship movement specifically in the course context
- Preconditions: Isolated course context for ship movement
- Expected: Acceptance requirement for course: ship authority and observed movement agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.006.case-02

Ship movement / speed

- Entry/action: Installed naval menu paths / optional mod mission module; Perform ship movement specifically in the speed context
- Preconditions: Isolated speed context for ship movement
- Expected: Acceptance requirement for speed: ship authority and observed movement agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.006.case-03

Ship movement / collision

- Entry/action: Installed naval menu paths / optional mod mission module; Perform ship movement specifically in the collision context
- Preconditions: Isolated collision context for ship movement
- Expected: Acceptance requirement for collision: ship authority and observed movement agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

## naval.007 Boarding combat

Each entry retains authority and separate owner/observer observations in the CSV.

### naval.007.case-01

Boarding combat / board

- Entry/action: Installed naval menu paths / optional mod mission module; Perform boarding combat specifically in the board context
- Preconditions: Isolated board context for boarding combat
- Expected: Acceptance requirement for board: agents and roster casualties remain associated with correct parties
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.007.case-02

Boarding combat / repel

- Entry/action: Installed naval menu paths / optional mod mission module; Perform boarding combat specifically in the repel context
- Preconditions: Isolated repel context for boarding combat
- Expected: Acceptance requirement for repel: agents and roster casualties remain associated with correct parties
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.007.case-03

Boarding combat / casualties

- Entry/action: Installed naval menu paths / optional mod mission module; Perform boarding combat specifically in the casualties context
- Preconditions: Isolated casualties context for boarding combat
- Expected: Acceptance requirement for casualties: agents and roster casualties remain associated with correct parties
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

## naval.008 Naval projectile hit

Each entry retains authority and separate owner/observer observations in the CSV.

### naval.008.case-01

Naval projectile hit / ship

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval projectile hit specifically in the ship context
- Preconditions: Isolated ship context for naval projectile hit
- Expected: Acceptance requirement for ship: impact/damage is applied once to the intended target
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.008.case-02

Naval projectile hit / crew

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval projectile hit specifically in the crew context
- Preconditions: Isolated crew context for naval projectile hit
- Expected: Acceptance requirement for crew: impact/damage is applied once to the intended target
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.008.case-03

Naval projectile hit / water miss

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval projectile hit specifically in the water miss context
- Preconditions: Isolated water miss context for naval projectile hit
- Expected: Acceptance requirement for water miss: impact/damage is applied once to the intended target
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

## naval.009 Naval disable/opt-in boundary

Each entry retains authority and separate owner/observer observations in the CSV.

### naval.009.case-01

Naval disable/opt-in boundary / module absent

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval disable/opt-in boundary specifically in the module absent context
- Preconditions: Isolated module absent context for naval disable/opt-in boundary
- Expected: Acceptance requirement for module absent: unsupported launch/action is visible and not reported as tested support
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.009.case-02

Naval disable/opt-in boundary / opt-in absent

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval disable/opt-in boundary specifically in the opt-in absent context
- Preconditions: Isolated opt-in absent context for naval disable/opt-in boundary
- Expected: Acceptance requirement for opt-in absent: unsupported launch/action is visible and not reported as tested support
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)

### naval.009.case-03

Naval disable/opt-in boundary / enabled

- Entry/action: Installed naval menu paths / optional mod mission module; Perform naval disable/opt-in boundary specifically in the enabled context
- Preconditions: Isolated enabled context for naval disable/opt-in boundary
- Expected: Acceptance requirement for enabled: unsupported launch/action is visible and not reported as tested support
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unknown; no Missions.Naval project in bound tree
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Missions](../../source/Missions), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [features/baseline/installed.json](../../features/baseline/installed.json)
