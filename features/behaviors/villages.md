# villages behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## villages.001 Enter village

Each entry retains authority and separate owner/observer observations in the CSV.

### villages.001.case-01

Enter village / normal

- Entry/action: Village menu / campaign map; Perform enter village specifically in the normal context
- Preconditions: Isolated normal context for enter village
- Expected: Acceptance requirement for normal: menu reflects authoritative village state and access
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.001.case-02

Enter village / raided

- Entry/action: Village menu / campaign map; Perform enter village specifically in the raided context
- Preconditions: Isolated raided context for enter village
- Expected: Acceptance requirement for raided: menu reflects authoritative village state and access
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.001.case-03

Enter village / hostile

- Entry/action: Village menu / campaign map; Perform enter village specifically in the hostile context
- Preconditions: Isolated hostile context for enter village
- Expected: Acceptance requirement for hostile: menu reflects authoritative village state and access
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.001.case-04

Enter village / own village

- Entry/action: Village menu / campaign map; Perform enter village specifically in the own village context
- Preconditions: Isolated own village context for enter village
- Expected: Acceptance requirement for own village: menu reflects authoritative village state and access
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### menu.village.village.hostile_action.select

Select village.hostile_action

- Entry/action: Client campaign menu village; Choose the exact registered option hostile_action
- Preconditions: The real menu village is reached; option hostile_action condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_hostile_action_on_condition; game_menu_village_hostile_action_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@93-93
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.village_hostile_action.forget_it.select

Select village_hostile_action.forget_it

- Entry/action: Client campaign menu village_hostile_action; Choose the exact registered option forget_it
- Preconditions: The real menu village_hostile_action is reached; option forget_it condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks hostile_action_common_back_on_condition; game_menu_village_hostile_action_forget_it_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@98-98
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.village_looted.leave.select

Select village_looted.leave

- Entry/action: Client campaign menu village_looted; Choose the exact registered option leave
- Preconditions: The real menu village_looted is reached; option leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks hostile_action_common_back_on_condition; village_looted_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@119-119
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.village.village_center.select

Select village.village_center

- Entry/action: Client campaign menu village; Choose the exact registered option village_center
- Preconditions: The real menu village is reached; option village_center condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_village_center_on_condition; game_menu_village_village_center_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@156-156
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.village.village_wait.select

Select village.village_wait

- Entry/action: Client campaign menu village; Choose the exact registered option village_wait
- Preconditions: The real menu village is reached; option village_wait condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_wait_here_on_condition; game_menu_wait_village_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@157-157
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.village.village_return_to_army.select

Select village.village_return_to_army

- Entry/action: Client campaign menu village; Choose the exact registered option village_return_to_army
- Preconditions: The real menu village is reached; option village_return_to_army condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_return_to_army_on_condition; game_menu_return_to_army_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@158-158
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.village.leave.select

Select village.leave

- Entry/action: Client campaign menu village; Choose the exact registered option leave
- Preconditions: The real menu village is reached; option leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_town_leave_on_condition; game_menu_settlement_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@162-162
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.village_wait_menus.wait_leave.select

Select village_wait_menus.wait_leave

- Entry/action: Client campaign menu village_wait_menus; Choose the exact registered option wait_leave
- Preconditions: The real menu village_wait_menus is reached; option wait_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks back_on_condition; game_menu_stop_waiting_at_village_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@169-169
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## villages.002 Recruit volunteers

Each entry retains authority and separate owner/observer observations in the CSV.

### villages.002.case-01

Recruit volunteers / available slots

- Entry/action: Village menu / campaign map; Perform recruit volunteers specifically in the available slots context
- Preconditions: Isolated available slots context for recruit volunteers
- Expected: Acceptance requirement for available slots: volunteer stock and owning player roster balance
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.002.case-02

Recruit volunteers / relation restriction

- Entry/action: Village menu / campaign map; Perform recruit volunteers specifically in the relation restriction context
- Preconditions: Isolated relation restriction context for recruit volunteers
- Expected: Acceptance requirement for relation restriction: volunteer stock and owning player roster balance
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.002.case-03

Recruit volunteers / forced recruitment

- Entry/action: Village menu / campaign map; Perform recruit volunteers specifically in the forced recruitment context
- Preconditions: Isolated forced recruitment context for recruit volunteers
- Expected: Acceptance requirement for forced recruitment: volunteer stock and owning player roster balance
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### menu.settlement.village.recruit_volunteers.select

Select village.recruit_volunteers

- Entry/action: Client campaign menu village; Choose the exact registered option recruit_volunteers
- Preconditions: The real menu village is reached; option recruit_volunteers condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_recruit_volunteers_on_condition; game_menu_recruit_volunteers_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@154-154
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## villages.003 Village trade

Each entry retains authority and separate owner/observer observations in the CSV.

### villages.003.case-01

Village trade / buy

- Entry/action: Village menu / campaign map; Perform village trade specifically in the buy context
- Preconditions: Isolated buy context for village trade
- Expected: Acceptance requirement for buy: market stock and player resources balance
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.003.case-02

Village trade / sell

- Entry/action: Village menu / campaign map; Perform village trade specifically in the sell context
- Preconditions: Isolated sell context for village trade
- Expected: Acceptance requirement for sell: market stock and player resources balance
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.003.case-03

Village trade / food

- Entry/action: Village menu / campaign map; Perform village trade specifically in the food context
- Preconditions: Isolated food context for village trade
- Expected: Acceptance requirement for food: market stock and player resources balance
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.003.case-04

Village trade / ordinary goods

- Entry/action: Village menu / campaign map; Perform village trade specifically in the ordinary goods context
- Preconditions: Isolated ordinary goods context for village trade
- Expected: Acceptance requirement for ordinary goods: market stock and player resources balance
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### menu.settlement.village.trade.select

Select village.trade

- Entry/action: Client campaign menu village; Choose the exact registered option trade
- Preconditions: The real menu village is reached; option trade condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_buy_good_on_condition; null; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@155-155
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## villages.004 Raid village

Each entry retains authority and separate owner/observer observations in the CSV.

### villages.004.case-01

Raid village / start

- Entry/action: Village menu / campaign map; Perform raid village specifically in the start context
- Preconditions: Isolated start context for raid village
- Expected: Acceptance requirement for start: raid state and loot advance under authoritative campaign ticks
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.004.case-02

Raid village / progress

- Entry/action: Village menu / campaign map; Perform raid village specifically in the progress context
- Preconditions: Isolated progress context for raid village
- Expected: Acceptance requirement for progress: raid state and loot advance under authoritative campaign ticks
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.004.case-03

Raid village / interruption

- Entry/action: Village menu / campaign map; Perform raid village specifically in the interruption context
- Preconditions: Isolated interruption context for raid village
- Expected: Acceptance requirement for interruption: raid state and loot advance under authoritative campaign ticks
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.004.case-04

Raid village / completion

- Entry/action: Village menu / campaign map; Perform raid village specifically in the completion context
- Preconditions: Isolated completion context for raid village
- Expected: Acceptance requirement for completion: raid state and loot advance under authoritative campaign ticks
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### menu.village.village_hostile_action.raid_village.select

Select village_hostile_action.raid_village

- Entry/action: Client campaign menu village_hostile_action; Choose the exact registered option raid_village
- Preconditions: The real menu village_hostile_action is reached; option raid_village condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_hostile_action_raid_village_on_condition; game_menu_village_hostile_action_raid_village_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@95-95
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.raiding_village.raiding_village_end.select

Select raiding_village.raiding_village_end

- Entry/action: Client campaign menu raiding_village; Choose the exact registered option raiding_village_end
- Preconditions: The real menu raiding_village is reached; option raiding_village_end condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks wait_menu_end_raiding_on_condition; wait_menu_end_raiding_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@100-100
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.raiding_village.leave_army.select

Select raiding_village.leave_army

- Entry/action: Client campaign menu raiding_village; Choose the exact registered option leave_army
- Preconditions: The real menu raiding_village is reached; option leave_army condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks wait_menu_end_raiding_at_army_by_leaving_on_condition; wait_menu_end_raiding_at_army_by_leaving_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@101-101
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.raiding_village.abandon_army.select

Select raiding_village.abandon_army

- Entry/action: Client campaign menu raiding_village; Choose the exact registered option abandon_army
- Preconditions: The real menu raiding_village is reached; option abandon_army condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks wait_menu_end_raiding_at_army_by_abandoning_on_condition; wait_menu_end_raiding_at_army_by_abandoning_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@102-102
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.raid_occupied.raid_occuppied_continue.select

Select raid_occupied.raid_occuppied_continue

- Entry/action: Client campaign menu raid_occupied; Choose the exact registered option raid_occuppied_continue
- Preconditions: The real menu raid_occupied is reached; option raid_occuppied_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks raid_occupied_on_condition; raid_occupied_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@104-104
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.raid_village_no_resist_warn_player.raid_village_warn_continue.select

Select raid_village_no_resist_warn_player.raid_village_warn_continue

- Entry/action: Client campaign menu raid_village_no_resist_warn_player; Choose the exact registered option raid_village_warn_continue
- Preconditions: The real menu raid_village_no_resist_warn_player is reached; option raid_village_warn_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_hostile_action_raid_village_warn_continue_on_condition; game_menu_village_hostile_action_raid_village_warn_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@106-106
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.raid_village_no_resist_warn_player.raid_village_warn_leave.select

Select raid_village_no_resist_warn_player.raid_village_warn_leave

- Entry/action: Client campaign menu raid_village_no_resist_warn_player; Choose the exact registered option raid_village_warn_leave
- Preconditions: The real menu raid_village_no_resist_warn_player is reached; option raid_village_warn_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks hostile_action_common_back_on_condition; game_menu_village_hostile_action_warn_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@107-107
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.village_player_raid_ended.continue.select

Select village_player_raid_ended.continue

- Entry/action: Client campaign menu village_player_raid_ended; Choose the exact registered option continue
- Preconditions: The real menu village_player_raid_ended is reached; option continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks hostile_action_common_continue_on_condition; village_player_raid_ended_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@121-121
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.village_raid_ended_leaded_by_someone_else.continue.select

Select village_raid_ended_leaded_by_someone_else.continue

- Entry/action: Client campaign menu village_raid_ended_leaded_by_someone_else; Choose the exact registered option continue
- Preconditions: The real menu village_raid_ended_leaded_by_someone_else is reached; option continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks hostile_action_common_continue_on_condition; village_raid_ended_leaded_by_someone_else_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@123-123
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## villages.005 Force recruits

Each entry retains authority and separate owner/observer observations in the CSV.

### villages.005.case-01

Force recruits / accepted hostile action

- Entry/action: Village menu / campaign map; Perform force recruits specifically in the accepted hostile action context
- Preconditions: Isolated accepted hostile action context for force recruits
- Expected: Acceptance requirement for accepted hostile action: recruits and relation/crime effects occur only for the completed action
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.005.case-02

Force recruits / combat

- Entry/action: Village menu / campaign map; Perform force recruits specifically in the combat context
- Preconditions: Isolated combat context for force recruits
- Expected: Acceptance requirement for combat: recruits and relation/crime effects occur only for the completed action
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.005.case-03

Force recruits / cancellation

- Entry/action: Village menu / campaign map; Perform force recruits specifically in the cancellation context
- Preconditions: Isolated cancellation context for force recruits
- Expected: Acceptance requirement for cancellation: recruits and relation/crime effects occur only for the completed action
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### menu.village.village_hostile_action.force_peasants_to_give_volunteers.select

Select village_hostile_action.force_peasants_to_give_volunteers

- Entry/action: Client campaign menu village_hostile_action; Choose the exact registered option force_peasants_to_give_volunteers
- Preconditions: The real menu village_hostile_action is reached; option force_peasants_to_give_volunteers condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_hostile_action_force_volunteers_condition; game_menu_village_hostile_action_force_volunteers_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@96-96
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## villages.006 Take supplies

Each entry retains authority and separate owner/observer observations in the CSV.

### villages.006.case-01

Take supplies / accepted hostile action

- Entry/action: Village menu / campaign map; Perform take supplies specifically in the accepted hostile action context
- Preconditions: Isolated accepted hostile action context for take supplies
- Expected: Acceptance requirement for accepted hostile action: received goods and village consequences converge
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.006.case-02

Take supplies / combat

- Entry/action: Village menu / campaign map; Perform take supplies specifically in the combat context
- Preconditions: Isolated combat context for take supplies
- Expected: Acceptance requirement for combat: received goods and village consequences converge
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.006.case-03

Take supplies / cancellation

- Entry/action: Village menu / campaign map; Perform take supplies specifically in the cancellation context
- Preconditions: Isolated cancellation context for take supplies
- Expected: Acceptance requirement for cancellation: received goods and village consequences converge
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### menu.village.village_hostile_action.force_peasants_to_give_supplies.select

Select village_hostile_action.force_peasants_to_give_supplies

- Entry/action: Client campaign menu village_hostile_action; Choose the exact registered option force_peasants_to_give_supplies
- Preconditions: The real menu village_hostile_action is reached; option force_peasants_to_give_supplies condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_hostile_action_take_food_on_condition; game_menu_village_hostile_action_take_food_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@97-97
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.force_supplies_village.force_supplies_village_continue.select

Select force_supplies_village.force_supplies_village_continue

- Entry/action: Client campaign menu force_supplies_village; Choose the exact registered option force_supplies_village_continue
- Preconditions: The real menu force_supplies_village is reached; option force_supplies_village_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks hostile_action_common_continue_on_condition; village_force_supplies_ended_successfully_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@109-109
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.force_supplies_village_resist_warn_player.force_supplies_village_resist_warn_player_continue.select

Select force_supplies_village_resist_warn_player.force_supplies_village_resist_warn_player_continue

- Entry/action: Client campaign menu force_supplies_village_resist_warn_player; Choose the exact registered option force_supplies_village_resist_warn_player_continue
- Preconditions: The real menu force_supplies_village_resist_warn_player is reached; option force_supplies_village_resist_warn_player_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_force_supplies_village_resist_warn_player_continue_on_condition; game_menu_force_supplies_village_resist_warn_player_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@111-111
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.force_supplies_village_resist_warn_player.force_supplies_village_resist_warn_player_leave.select

Select force_supplies_village_resist_warn_player.force_supplies_village_resist_warn_player_leave

- Entry/action: Client campaign menu force_supplies_village_resist_warn_player; Choose the exact registered option force_supplies_village_resist_warn_player_leave
- Preconditions: The real menu force_supplies_village_resist_warn_player is reached; option force_supplies_village_resist_warn_player_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks hostile_action_common_back_on_condition; game_menu_village_hostile_action_warn_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@112-112
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.force_troops_village_resist_warn_player.force_supplies_village_resist_warn_player_continue.select

Select force_troops_village_resist_warn_player.force_supplies_village_resist_warn_player_continue

- Entry/action: Client campaign menu force_troops_village_resist_warn_player; Choose the exact registered option force_supplies_village_resist_warn_player_continue
- Preconditions: The real menu force_troops_village_resist_warn_player is reached; option force_supplies_village_resist_warn_player_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_force_troops_village_resist_warn_player_continue_on_condition; game_menu_force_troops_village_resist_warn_player_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@114-114
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.force_troops_village_resist_warn_player.force_supplies_village_resist_warn_player_leave.select

Select force_troops_village_resist_warn_player.force_supplies_village_resist_warn_player_leave

- Entry/action: Client campaign menu force_troops_village_resist_warn_player; Choose the exact registered option force_supplies_village_resist_warn_player_leave
- Preconditions: The real menu force_troops_village_resist_warn_player is reached; option force_supplies_village_resist_warn_player_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks hostile_action_common_back_on_condition; game_menu_village_hostile_action_warn_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@115-115
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.village.force_volunteers_village.force_supplies_village_continue.select

Select force_volunteers_village.force_supplies_village_continue

- Entry/action: Client campaign menu force_volunteers_village; Choose the exact registered option force_supplies_village_continue
- Preconditions: The real menu force_volunteers_village is reached; option force_supplies_village_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks hostile_action_common_continue_on_condition; village_force_volunteers_ended_successfully_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior@117-117
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## villages.007 Village production

Each entry retains authority and separate owner/observer observations in the CSV.

### villages.007.case-01

Village production / production type

- Entry/action: Village menu / campaign map; Perform village production specifically in the production type context
- Preconditions: Isolated production type context for village production
- Expected: Acceptance requirement for production type: produced goods and shipment state agree
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.007.case-02

Village production / daily stock

- Entry/action: Village menu / campaign map; Perform village production specifically in the daily stock context
- Preconditions: Isolated daily stock context for village production
- Expected: Acceptance requirement for daily stock: produced goods and shipment state agree
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.007.case-03

Village production / bound town

- Entry/action: Village menu / campaign map; Perform village production specifically in the bound town context
- Preconditions: Isolated bound town context for village production
- Expected: Acceptance requirement for bound town: produced goods and shipment state agree
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## villages.008 Village hearths

Each entry retains authority and separate owner/observer observations in the CSV.

### villages.008.case-01

Village hearths / growth

- Entry/action: Village menu / campaign map; Perform village hearths specifically in the growth context
- Preconditions: Isolated growth context for village hearths
- Expected: Acceptance requirement for growth: hearth count and dependent production agree
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.008.case-02

Village hearths / raid damage

- Entry/action: Village menu / campaign map; Perform village hearths specifically in the raid damage context
- Preconditions: Isolated raid damage context for village hearths
- Expected: Acceptance requirement for raid damage: hearth count and dependent production agree
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.008.case-03

Village hearths / healing

- Entry/action: Village menu / campaign map; Perform village hearths specifically in the healing context
- Preconditions: Isolated healing context for village hearths
- Expected: Acceptance requirement for healing: hearth count and dependent production agree
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## villages.009 Village recovery

Each entry retains authority and separate owner/observer observations in the CSV.

### villages.009.case-01

Village recovery / raided

- Entry/action: Village menu / campaign map; Perform village recovery specifically in the raided context
- Preconditions: Isolated raided context for village recovery
- Expected: Acceptance requirement for raided: state and restored access follow authoritative healing ticks
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.009.case-02

Village recovery / recovering

- Entry/action: Village menu / campaign map; Perform village recovery specifically in the recovering context
- Preconditions: Isolated recovering context for village recovery
- Expected: Acceptance requirement for recovering: state and restored access follow authoritative healing ticks
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.009.case-03

Village recovery / normal

- Entry/action: Village menu / campaign map; Perform village recovery specifically in the normal context
- Preconditions: Isolated normal context for village recovery
- Expected: Acceptance requirement for normal: state and restored access follow authoritative healing ticks
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## villages.010 Village militia

Each entry retains authority and separate owner/observer observations in the CSV.

### villages.010.case-01

Village militia / spawn

- Entry/action: Village menu / campaign map; Perform village militia specifically in the spawn context
- Preconditions: Isolated spawn context for village militia
- Expected: Acceptance requirement for spawn: militia identity and roster converge
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.010.case-02

Village militia / battle loss

- Entry/action: Village menu / campaign map; Perform village militia specifically in the battle loss context
- Preconditions: Isolated battle loss context for village militia
- Expected: Acceptance requirement for battle loss: militia identity and roster converge
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.010.case-03

Village militia / daily growth

- Entry/action: Village menu / campaign map; Perform village militia specifically in the daily growth context
- Preconditions: Isolated daily growth context for village militia
- Expected: Acceptance requirement for daily growth: militia identity and roster converge
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## villages.011 Villager party departure

Each entry retains authority and separate owner/observer observations in the CSV.

### villages.011.case-01

Villager party departure / goods ready

- Entry/action: Village menu / campaign map; Perform villager party departure specifically in the goods ready context
- Preconditions: Isolated goods ready context for villager party departure
- Expected: Acceptance requirement for goods ready: one registered villager party carries the intended goods
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.011.case-02

Villager party departure / town route

- Entry/action: Village menu / campaign map; Perform villager party departure specifically in the town route context
- Preconditions: Isolated town route context for villager party departure
- Expected: Acceptance requirement for town route: one registered villager party carries the intended goods
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.011.case-03

Villager party departure / party creation

- Entry/action: Village menu / campaign map; Perform villager party departure specifically in the party creation context
- Preconditions: Isolated party creation context for villager party departure
- Expected: Acceptance requirement for party creation: one registered villager party carries the intended goods
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## villages.012 Villager party return

Each entry retains authority and separate owner/observer observations in the CSV.

### villages.012.case-01

Villager party return / successful sale

- Entry/action: Village menu / campaign map; Perform villager party return specifically in the successful sale context
- Preconditions: Isolated successful sale context for villager party return
- Expected: Acceptance requirement for successful sale: village stock and party lifecycle reach the intended state
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.012.case-02

Villager party return / intercepted

- Entry/action: Village menu / campaign map; Perform villager party return specifically in the intercepted context
- Preconditions: Isolated intercepted context for villager party return
- Expected: Acceptance requirement for intercepted: village stock and party lifecycle reach the intended state
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### villages.012.case-03

Villager party return / target changed

- Entry/action: Village menu / campaign map; Perform villager party return specifically in the target changed context
- Preconditions: Isolated target changed context for villager party return
- Expected: Acceptance requirement for target changed: village stock and party lifecycle reach the intended state
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

Sources: [source/GameInterface/Services/Villages](../../source/GameInterface/Services/Villages), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)
