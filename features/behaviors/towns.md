# towns behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## towns.001 Enter town

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.001.case-01

Enter town / friendly

- Entry/action: Town/castle menu / settlement management; Perform enter town specifically in the friendly context
- Preconditions: Isolated friendly context for enter town
- Expected: Acceptance requirement for friendly: menu and encounter state reflect the actual settlement access
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.001.case-02

Enter town / neutral

- Entry/action: Town/castle menu / settlement management; Perform enter town specifically in the neutral context
- Preconditions: Isolated neutral context for enter town
- Expected: Acceptance requirement for neutral: menu and encounter state reflect the actual settlement access
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.001.case-03

Enter town / hostile

- Entry/action: Town/castle menu / settlement management; Perform enter town specifically in the hostile context
- Preconditions: Isolated hostile context for enter town
- Expected: Acceptance requirement for hostile: menu and encounter state reflect the actual settlement access
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.001.case-04

Enter town / siege

- Entry/action: Town/castle menu / settlement management; Perform enter town specifically in the siege context
- Preconditions: Isolated siege context for enter town
- Expected: Acceptance requirement for siege: menu and encounter state reflect the actual settlement access
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### menu.settlement.town.town_keep.select

Select town.town_keep

- Entry/action: Client campaign menu town; Choose the exact registered option town_keep
- Preconditions: The real menu town is reached; option town_keep condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_go_to_keep_on_condition; game_menu_town_go_to_keep_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@41-41
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town.manage_production.select

Select town.manage_production

- Entry/action: Client campaign menu town; Choose the exact registered option manage_production
- Preconditions: The real menu town is reached; option manage_production condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_manage_town_on_condition; null; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@50-50
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town.manage_production_cheat.select

Select town.manage_production_cheat

- Entry/action: Client campaign menu town; Choose the exact registered option manage_production_cheat
- Preconditions: The real menu town is reached; option manage_production_cheat condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_manage_town_cheat_on_condition; null; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@51-51
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town.town_streets.select

Select town.town_streets

- Entry/action: Client campaign menu town; Choose the exact registered option town_streets
- Preconditions: The real menu town is reached; option town_streets condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_town_streets_on_condition; game_menu_town_town_streets_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@58-58
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town.town_return_to_army.select

Select town.town_return_to_army

- Entry/action: Client campaign menu town; Choose the exact registered option town_return_to_army
- Preconditions: The real menu town is reached; option town_return_to_army condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_return_to_army_on_condition; game_menu_return_to_army_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@63-63
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town.town_leave.select

Select town.town_leave

- Entry/action: Client campaign menu town; Choose the exact registered option town_leave
- Preconditions: The real menu town is reached; option town_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_town_leave_on_condition; game_menu_settlement_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@64-64
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town_keep.town_lords_hall_go_to_dungeon.select

Select town_keep.town_lords_hall_go_to_dungeon

- Entry/action: Client campaign menu town_keep; Choose the exact registered option town_lords_hall_go_to_dungeon
- Preconditions: The real menu town_keep is reached; option town_lords_hall_go_to_dungeon condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_go_dungeon_on_condition; game_menu_go_dungeon_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@66-66
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town_keep.open_stash.select

Select town_keep.open_stash

- Entry/action: Client campaign menu town_keep; Choose the exact registered option open_stash
- Preconditions: The real menu town_keep is reached; option open_stash condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_keep_open_stash_on_condition; game_menu_town_keep_open_stash_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@69-69
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town_keep.town_lords_hall.select

Select town_keep.town_lords_hall

- Entry/action: Client campaign menu town_keep; Choose the exact registered option town_lords_hall
- Preconditions: The real menu town_keep is reached; option town_lords_hall condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_keep_go_to_lords_hall_on_condition; game_menu_town_lordshall_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@70-70
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town_keep.town_lords_hall_cheat.select

Select town_keep.town_lords_hall_cheat

- Entry/action: Client campaign menu town_keep; Choose the exact registered option town_lords_hall_cheat
- Preconditions: The real menu town_keep is reached; option town_lords_hall_cheat condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_go_to_lords_hall_cheat_on_condition; game_menu_lordshall_cheat_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@71-71
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town_keep_dungeon.town_prison_leave_prisoners.select

Select town_keep_dungeon.town_prison_leave_prisoners

- Entry/action: Client campaign menu town_keep_dungeon; Choose the exact registered option town_prison_leave_prisoners
- Preconditions: The real menu town_keep_dungeon is reached; option town_prison_leave_prisoners condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_leave_prisoners_on_condition; game_menu_castle_leave_prisoners_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@77-77
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town_keep_dungeon.town_prison_manage_prisoners.select

Select town_keep_dungeon.town_prison_manage_prisoners

- Entry/action: Client campaign menu town_keep_dungeon; Choose the exact registered option town_prison_manage_prisoners
- Preconditions: The real menu town_keep_dungeon is reached; option town_prison_manage_prisoners condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_manage_prisoners_on_condition; game_menu_castle_manage_prisoners_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@78-78
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town_keep_dungeon.town_prison.select

Select town_keep_dungeon.town_prison

- Entry/action: Client campaign menu town_keep_dungeon; Choose the exact registered option town_prison
- Preconditions: The real menu town_keep_dungeon is reached; option town_prison condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_enter_the_dungeon_on_condition; game_menu_town_dungeon_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@79-79
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town_keep_dungeon.town_prison_cheat.select

Select town_keep_dungeon.town_prison_cheat

- Entry/action: Client campaign menu town_keep_dungeon; Choose the exact registered option town_prison_cheat
- Preconditions: The real menu town_keep_dungeon is reached; option town_prison_cheat condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_go_to_dungeon_cheat_on_condition; game_menu_dungeon_cheat_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@80-80
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town_keep_bribe.town_keep_bribe_pay.select

Select town_keep_bribe.town_keep_bribe_pay

- Entry/action: Client campaign menu town_keep_bribe; Choose the exact registered option town_keep_bribe_pay
- Preconditions: The real menu town_keep_bribe is reached; option town_keep_bribe_pay condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_keep_bribe_pay_bribe_on_condition; game_menu_town_keep_bribe_pay_bribe_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@86-86
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town_backstreet.town_tavern.select

Select town_backstreet.town_tavern

- Entry/action: Client campaign menu town_backstreet; Choose the exact registered option town_tavern
- Preconditions: The real menu town_backstreet is reached; option town_tavern condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks visit_the_tavern_on_condition; game_menu_town_town_tavern_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@97-97
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town_arena.town_enter_arena.select

Select town_arena.town_enter_arena

- Entry/action: Client campaign menu town_arena; Choose the exact registered option town_enter_arena
- Preconditions: The real menu town_arena is reached; option town_enter_arena condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_enter_the_arena_on_condition; game_menu_town_town_arena_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@111-111
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.settlement_player_unconscious.continue.select

Select settlement_player_unconscious.continue

- Entry/action: Client campaign menu settlement_player_unconscious; Choose the exact registered option continue
- Preconditions: The real menu settlement_player_unconscious is reached; option continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks continue_on_condition; settlement_player_unconscious_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@117-117
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## towns.002 Enter castle

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.002.case-01

Enter castle / friendly

- Entry/action: Town/castle menu / settlement management; Perform enter castle specifically in the friendly context
- Preconditions: Isolated friendly context for enter castle
- Expected: Acceptance requirement for friendly: correct castle access and interior options are shown
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.002.case-02

Enter castle / permission denied

- Entry/action: Town/castle menu / settlement management; Perform enter castle specifically in the permission denied context
- Preconditions: Isolated permission denied context for enter castle
- Expected: Acceptance requirement for permission denied: correct castle access and interior options are shown
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.002.case-03

Enter castle / hostile

- Entry/action: Town/castle menu / settlement management; Perform enter castle specifically in the hostile context
- Preconditions: Isolated hostile context for enter castle
- Expected: Acceptance requirement for hostile: correct castle access and interior options are shown
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### menu.settlement.castle.castle_prison.select

Select castle.castle_prison

- Entry/action: Client campaign menu castle; Choose the exact registered option castle_prison
- Preconditions: The real menu castle is reached; option castle_prison condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_go_to_the_dungeon_on_condition; game_menu_keep_dungeon_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@129-129
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle.castle_prison_cheat.select

Select castle.castle_prison_cheat

- Entry/action: Client campaign menu castle; Choose the exact registered option castle_prison_cheat
- Preconditions: The real menu castle is reached; option castle_prison_cheat condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_go_to_dungeon_cheat_on_condition; game_menu_dungeon_cheat_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@130-130
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle.manage_production.select

Select castle.manage_production

- Entry/action: Client campaign menu castle; Choose the exact registered option manage_production
- Preconditions: The real menu castle is reached; option manage_production condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_manage_castle_on_condition; null; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@132-132
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle.open_stash.select

Select castle.open_stash

- Entry/action: Client campaign menu castle; Choose the exact registered option open_stash
- Preconditions: The real menu castle is reached; option open_stash condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_keep_open_stash_on_condition; game_menu_town_keep_open_stash_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@133-133
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle.take_a_walk_around_the_castle.select

Select castle.take_a_walk_around_the_castle

- Entry/action: Client campaign menu castle; Choose the exact registered option take_a_walk_around_the_castle
- Preconditions: The real menu castle is reached; option take_a_walk_around_the_castle condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_take_a_walk_on_condition; game_menu_castle_take_a_walk_around_the_castle_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@135-135
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle.castle_lords_hall.select

Select castle.castle_lords_hall

- Entry/action: Client campaign menu castle; Choose the exact registered option castle_lords_hall
- Preconditions: The real menu castle is reached; option castle_lords_hall condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_go_to_lords_hall_on_condition; game_menu_castle_lordshall_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@136-136
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle.castle_lords_hall_cheat.select

Select castle.castle_lords_hall_cheat

- Entry/action: Client campaign menu castle; Choose the exact registered option castle_lords_hall_cheat
- Preconditions: The real menu castle is reached; option castle_lords_hall_cheat condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_go_to_lords_hall_cheat_on_condition; game_menu_lordshall_cheat_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@137-137
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle.castle_return_to_army.select

Select castle.castle_return_to_army

- Entry/action: Client campaign menu castle; Choose the exact registered option castle_return_to_army
- Preconditions: The real menu castle is reached; option castle_return_to_army condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_return_to_army_on_condition; game_menu_return_to_army_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@142-142
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle.leave.select

Select castle.leave

- Entry/action: Client campaign menu castle; Choose the exact registered option leave
- Preconditions: The real menu castle is reached; option leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_town_leave_on_condition; game_menu_settlement_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@143-143
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle_dungeon.town_prison_leave_prisoners.select

Select castle_dungeon.town_prison_leave_prisoners

- Entry/action: Client campaign menu castle_dungeon; Choose the exact registered option town_prison_leave_prisoners
- Preconditions: The real menu castle_dungeon is reached; option town_prison_leave_prisoners condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_leave_prisoners_on_condition; game_menu_castle_leave_prisoners_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@145-145
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle_dungeon.town_prison_manage_prisoners.select

Select castle_dungeon.town_prison_manage_prisoners

- Entry/action: Client campaign menu castle_dungeon; Choose the exact registered option town_prison_manage_prisoners
- Preconditions: The real menu castle_dungeon is reached; option town_prison_manage_prisoners condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_manage_prisoners_on_condition; game_menu_castle_manage_prisoners_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@146-146
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle_dungeon.town_prison.select

Select castle_dungeon.town_prison

- Entry/action: Client campaign menu castle_dungeon; Choose the exact registered option town_prison
- Preconditions: The real menu castle_dungeon is reached; option town_prison condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_enter_the_dungeon_on_condition; game_menu_castle_dungeon_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@147-147
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle_dungeon.town_prison_cheat.select

Select castle_dungeon.town_prison_cheat

- Entry/action: Client campaign menu castle_dungeon; Choose the exact registered option town_prison_cheat
- Preconditions: The real menu castle_dungeon is reached; option town_prison_cheat condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_go_to_dungeon_cheat_on_condition; game_menu_dungeon_cheat_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@148-148
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## towns.003 Wait in settlement

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.003.case-01

Wait in settlement / start

- Entry/action: Town/castle menu / settlement management; Perform wait in settlement specifically in the start context
- Preconditions: Isolated start context for wait in settlement
- Expected: Acceptance requirement for start: time and party/settlement state agree when waiting ends
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.003.case-02

Wait in settlement / stop

- Entry/action: Town/castle menu / settlement management; Perform wait in settlement specifically in the stop context
- Preconditions: Isolated stop context for wait in settlement
- Expected: Acceptance requirement for stop: time and party/settlement state agree when waiting ends
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.003.case-03

Wait in settlement / interrupted by siege

- Entry/action: Town/castle menu / settlement management; Perform wait in settlement specifically in the interrupted by siege context
- Preconditions: Isolated interrupted by siege context for wait in settlement
- Expected: Acceptance requirement for interrupted by siege: time and party/settlement state agree when waiting ends
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## towns.004 Inspect settlement ownership

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.004.case-01

Inspect settlement ownership / town

- Entry/action: Town/castle menu / settlement management; Perform inspect settlement ownership specifically in the town context
- Preconditions: Isolated town context for inspect settlement ownership
- Expected: Acceptance requirement for town: owner clan/kingdom and map presentation agree
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.004.case-02

Inspect settlement ownership / castle

- Entry/action: Town/castle menu / settlement management; Perform inspect settlement ownership specifically in the castle context
- Preconditions: Isolated castle context for inspect settlement ownership
- Expected: Acceptance requirement for castle: owner clan/kingdom and map presentation agree
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.004.case-03

Inspect settlement ownership / capture

- Entry/action: Town/castle menu / settlement management; Perform inspect settlement ownership specifically in the capture context
- Preconditions: Isolated capture context for inspect settlement ownership
- Expected: Acceptance requirement for capture: owner clan/kingdom and map presentation agree
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## towns.005 Change governor

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.005.case-01

Change governor / assign

- Entry/action: Town/castle menu / settlement management; Perform change governor specifically in the assign context
- Preconditions: Isolated assign context for change governor
- Expected: Acceptance requirement for assign: old/new governor assignments and settlement effects converge
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.005.case-02

Change governor / replace

- Entry/action: Town/castle menu / settlement management; Perform change governor specifically in the replace context
- Preconditions: Isolated replace context for change governor
- Expected: Acceptance requirement for replace: old/new governor assignments and settlement effects converge
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.005.case-03

Change governor / remove

- Entry/action: Town/castle menu / settlement management; Perform change governor specifically in the remove context
- Preconditions: Isolated remove context for change governor
- Expected: Acceptance requirement for remove: old/new governor assignments and settlement effects converge
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.005.case-04

Change governor / hero unavailable

- Entry/action: Town/castle menu / settlement management; Perform change governor specifically in the hero unavailable context
- Preconditions: Isolated hero unavailable context for change governor
- Expected: Acceptance requirement for hero unavailable: old/new governor assignments and settlement effects converge
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## towns.006 Manage garrison roster

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.006.case-01

Manage garrison roster / deposit

- Entry/action: Town/castle menu / settlement management; Perform manage garrison roster specifically in the deposit context
- Preconditions: Isolated deposit context for manage garrison roster
- Expected: Acceptance requirement for deposit: party and garrison troop counts balance
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.006.case-02

Manage garrison roster / withdraw

- Entry/action: Town/castle menu / settlement management; Perform manage garrison roster specifically in the withdraw context
- Preconditions: Isolated withdraw context for manage garrison roster
- Expected: Acceptance requirement for withdraw: party and garrison troop counts balance
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.006.case-03

Manage garrison roster / upgrade

- Entry/action: Town/castle menu / settlement management; Perform manage garrison roster specifically in the upgrade context
- Preconditions: Isolated upgrade context for manage garrison roster
- Expected: Acceptance requirement for upgrade: party and garrison troop counts balance
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### menu.settlement.town_keep.leave_troops_to_garrison.select

Select town_keep.leave_troops_to_garrison

- Entry/action: Client campaign menu town_keep; Choose the exact registered option leave_troops_to_garrison
- Preconditions: The real menu town_keep is reached; option leave_troops_to_garrison condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_leave_troops_garrison_on_condition; game_menu_leave_troops_garrison_on_consequece; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@67-67
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.town_keep.manage_garrison.select

Select town_keep.manage_garrison

- Entry/action: Client campaign menu town_keep; Choose the exact registered option manage_garrison
- Preconditions: The real menu town_keep is reached; option manage_garrison condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_manage_garrison_on_condition; game_menu_manage_garrison_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@68-68
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle.manage_garrison.select

Select castle.manage_garrison

- Entry/action: Client campaign menu castle; Choose the exact registered option manage_garrison
- Preconditions: The real menu castle is reached; option manage_garrison condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_manage_garrison_on_condition; game_menu_manage_garrison_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@131-131
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.settlement.castle.leave_troops_to_garrison.select

Select castle.leave_troops_to_garrison

- Entry/action: Client campaign menu castle; Choose the exact registered option leave_troops_to_garrison
- Preconditions: The real menu castle is reached; option leave_troops_to_garrison condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_leave_troops_garrison_on_condition; game_menu_leave_troops_garrison_on_consequece; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@134-134
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## towns.007 Garrison component backlink

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.007.case-01

Garrison component backlink / initialize

- Entry/action: Town/castle menu / settlement management; Perform garrison component backlink specifically in the initialize context
- Preconditions: Isolated initialize context for garrison component backlink
- Expected: Acceptance requirement for initialize: settlement references the correct registered active garrison component
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.007.case-02

Garrison component backlink / finalize

- Entry/action: Town/castle menu / settlement management; Perform garrison component backlink specifically in the finalize context
- Preconditions: Isolated finalize context for garrison component backlink
- Expected: Acceptance requirement for finalize: settlement references the correct registered active garrison component
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.007.case-03

Garrison component backlink / repeated lifecycle

- Entry/action: Town/castle menu / settlement management; Perform garrison component backlink specifically in the repeated lifecycle context
- Preconditions: Isolated repeated lifecycle context for garrison component backlink
- Expected: Acceptance requirement for repeated lifecycle: settlement references the correct registered active garrison component
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## towns.008 Automatic garrison recruitment

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.008.case-01

Automatic garrison recruitment / enabled

- Entry/action: Town/castle menu / settlement management; Perform automatic garrison recruitment specifically in the enabled context
- Preconditions: Isolated enabled context for automatic garrison recruitment
- Expected: Acceptance requirement for enabled: recruits appear only under the permitted authoritative policy
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.008.case-02

Automatic garrison recruitment / disabled

- Entry/action: Town/castle menu / settlement management; Perform automatic garrison recruitment specifically in the disabled context
- Preconditions: Isolated disabled context for automatic garrison recruitment
- Expected: Acceptance requirement for disabled: recruits appear only under the permitted authoritative policy
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.008.case-03

Automatic garrison recruitment / wage limit

- Entry/action: Town/castle menu / settlement management; Perform automatic garrison recruitment specifically in the wage limit context
- Preconditions: Isolated wage limit context for automatic garrison recruitment
- Expected: Acceptance requirement for wage limit: recruits appear only under the permitted authoritative policy
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## towns.009 Inspect militia

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.009.case-01

Inspect militia / growth

- Entry/action: Town/castle menu / settlement management; Perform inspect militia specifically in the growth context
- Preconditions: Isolated growth context for inspect militia
- Expected: Acceptance requirement for growth: militia roster/state agree across peers
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.009.case-02

Inspect militia / veteran ratio

- Entry/action: Town/castle menu / settlement management; Perform inspect militia specifically in the veteran ratio context
- Preconditions: Isolated veteran ratio context for inspect militia
- Expected: Acceptance requirement for veteran ratio: militia roster/state agree across peers
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.009.case-03

Inspect militia / casualties

- Entry/action: Town/castle menu / settlement management; Perform inspect militia specifically in the casualties context
- Preconditions: Isolated casualties context for inspect militia
- Expected: Acceptance requirement for casualties: militia roster/state agree across peers
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## towns.010 Town prosperity

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.010.case-01

Town prosperity / daily growth

- Entry/action: Town/castle menu / settlement management; Perform town prosperity specifically in the daily growth context
- Preconditions: Isolated daily growth context for town prosperity
- Expected: Acceptance requirement for daily growth: prosperity and related economy state change once per tick
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.010.case-02

Town prosperity / loss

- Entry/action: Town/castle menu / settlement management; Perform town prosperity specifically in the loss context
- Preconditions: Isolated loss context for town prosperity
- Expected: Acceptance requirement for loss: prosperity and related economy state change once per tick
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.010.case-03

Town prosperity / starvation

- Entry/action: Town/castle menu / settlement management; Perform town prosperity specifically in the starvation context
- Preconditions: Isolated starvation context for town prosperity
- Expected: Acceptance requirement for starvation: prosperity and related economy state change once per tick
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## towns.011 Town loyalty

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.011.case-01

Town loyalty / governor

- Entry/action: Town/castle menu / settlement management; Perform town loyalty specifically in the governor context
- Preconditions: Isolated governor context for town loyalty
- Expected: Acceptance requirement for governor: loyalty and rebellion conditions use the authoritative settlement state
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.011.case-02

Town loyalty / culture

- Entry/action: Town/castle menu / settlement management; Perform town loyalty specifically in the culture context
- Preconditions: Isolated culture context for town loyalty
- Expected: Acceptance requirement for culture: loyalty and rebellion conditions use the authoritative settlement state
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.011.case-03

Town loyalty / policies

- Entry/action: Town/castle menu / settlement management; Perform town loyalty specifically in the policies context
- Preconditions: Isolated policies context for town loyalty
- Expected: Acceptance requirement for policies: loyalty and rebellion conditions use the authoritative settlement state
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.011.case-04

Town loyalty / starvation

- Entry/action: Town/castle menu / settlement management; Perform town loyalty specifically in the starvation context
- Preconditions: Isolated starvation context for town loyalty
- Expected: Acceptance requirement for starvation: loyalty and rebellion conditions use the authoritative settlement state
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## towns.012 Town security

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.012.case-01

Town security / garrison

- Entry/action: Town/castle menu / settlement management; Perform town security specifically in the garrison context
- Preconditions: Isolated garrison context for town security
- Expected: Acceptance requirement for garrison: security and dependent state converge
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.012.case-02

Town security / bandits

- Entry/action: Town/castle menu / settlement management; Perform town security specifically in the bandits context
- Preconditions: Isolated bandits context for town security
- Expected: Acceptance requirement for bandits: security and dependent state converge
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.012.case-03

Town security / policies

- Entry/action: Town/castle menu / settlement management; Perform town security specifically in the policies context
- Preconditions: Isolated policies context for town security
- Expected: Acceptance requirement for policies: security and dependent state converge
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.012.case-04

Town security / low security

- Entry/action: Town/castle menu / settlement management; Perform town security specifically in the low security context
- Preconditions: Isolated low security context for town security
- Expected: Acceptance requirement for low security: security and dependent state converge
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## towns.013 Town food stock

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.013.case-01

Town food stock / production

- Entry/action: Town/castle menu / settlement management; Perform town food stock specifically in the production context
- Preconditions: Isolated production context for town food stock
- Expected: Acceptance requirement for production: stock, food change and starvation casualties agree
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.013.case-02

Town food stock / consumption

- Entry/action: Town/castle menu / settlement management; Perform town food stock specifically in the consumption context
- Preconditions: Isolated consumption context for town food stock
- Expected: Acceptance requirement for consumption: stock, food change and starvation casualties agree
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.013.case-03

Town food stock / siege

- Entry/action: Town/castle menu / settlement management; Perform town food stock specifically in the siege context
- Preconditions: Isolated siege context for town food stock
- Expected: Acceptance requirement for siege: stock, food change and starvation casualties agree
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.013.case-04

Town food stock / starvation

- Entry/action: Town/castle menu / settlement management; Perform town food stock specifically in the starvation context
- Preconditions: Isolated starvation context for town food stock
- Expected: Acceptance requirement for starvation: stock, food change and starvation casualties agree
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## towns.014 Town rebellion

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.014.case-01

Town rebellion / eligibility

- Entry/action: Town/castle menu / settlement management; Perform town rebellion specifically in the eligibility context
- Preconditions: Isolated eligibility context for town rebellion
- Expected: Acceptance requirement for eligibility: new rebel objects are registered and state changes reach both clients
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.014.case-02

Town rebellion / outbreak

- Entry/action: Town/castle menu / settlement management; Perform town rebellion specifically in the outbreak context
- Preconditions: Isolated outbreak context for town rebellion
- Expected: Acceptance requirement for outbreak: new rebel objects are registered and state changes reach both clients
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.014.case-03

Town rebellion / new clan

- Entry/action: Town/castle menu / settlement management; Perform town rebellion specifically in the new clan context
- Preconditions: Isolated new clan context for town rebellion
- Expected: Acceptance requirement for new clan: new rebel objects are registered and state changes reach both clients
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.014.case-04

Town rebellion / ownership

- Entry/action: Town/castle menu / settlement management; Perform town rebellion specifically in the ownership context
- Preconditions: Isolated ownership context for town rebellion
- Expected: Acceptance requirement for ownership: new rebel objects are registered and state changes reach both clients
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## towns.015 Town market tax

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.015.case-01

Town market tax / trade

- Entry/action: Town/castle menu / settlement management; Perform town market tax specifically in the trade context
- Preconditions: Isolated trade context for town market tax
- Expected: Acceptance requirement for trade: trade tax and payout occur for the correct owner
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.015.case-02

Town market tax / daily accumulation

- Entry/action: Town/castle menu / settlement management; Perform town market tax specifically in the daily accumulation context
- Preconditions: Isolated daily accumulation context for town market tax
- Expected: Acceptance requirement for daily accumulation: trade tax and payout occur for the correct owner
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.015.case-03

Town market tax / owner payout

- Entry/action: Town/castle menu / settlement management; Perform town market tax specifically in the owner payout context
- Preconditions: Isolated owner payout context for town market tax
- Expected: Acceptance requirement for owner payout: trade tax and payout occur for the correct owner
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## towns.016 Settlement capture

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.016.case-01

Settlement capture / battle

- Entry/action: Town/castle menu / settlement management; Perform settlement capture specifically in the battle context
- Preconditions: Isolated battle context for settlement capture
- Expected: Acceptance requirement for battle: new ownership and garrison/dungeon side effects agree
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.016.case-02

Settlement capture / simulation

- Entry/action: Town/castle menu / settlement management; Perform settlement capture specifically in the simulation context
- Preconditions: Isolated simulation context for settlement capture
- Expected: Acceptance requirement for simulation: new ownership and garrison/dungeon side effects agree
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.016.case-03

Settlement capture / kingdom allocation

- Entry/action: Town/castle menu / settlement management; Perform settlement capture specifically in the kingdom allocation context
- Preconditions: Isolated kingdom allocation context for settlement capture
- Expected: Acceptance requirement for kingdom allocation: new ownership and garrison/dungeon side effects agree
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

## towns.017 Settlement devastation

Each entry retains authority and separate owner/observer observations in the CSV.

### towns.017.case-01

Settlement devastation / pillage

- Entry/action: Town/castle menu / settlement management; Perform settlement devastation specifically in the pillage context
- Preconditions: Isolated pillage context for settlement devastation
- Expected: Acceptance requirement for pillage: accepted aftermath choice yields the intended state and relation changes
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.017.case-02

Settlement devastation / mercy

- Entry/action: Town/castle menu / settlement management; Perform settlement devastation specifically in the mercy context
- Preconditions: Isolated mercy context for settlement devastation
- Expected: Acceptance requirement for mercy: accepted aftermath choice yields the intended state and relation changes
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)

### towns.017.case-03

Settlement devastation / devastation choice

- Entry/action: Town/castle menu / settlement management; Perform settlement devastation specifically in the devastation choice context
- Preconditions: Isolated devastation choice context for settlement devastation
- Expected: Acceptance requirement for devastation choice: accepted aftermath choice yields the intended state and relation changes
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

Sources: [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns), [source/GameInterface/Services/Settlements](../../source/GameInterface/Services/Settlements), [source/GameInterface/Services/Fiefs](../../source/GameInterface/Services/Fiefs)
