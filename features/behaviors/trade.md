# trade behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## trade.001 Buy goods

Each entry retains authority and separate owner/observer observations in the CSV.

### trade.001.case-01

Buy goods / one item

- Entry/action: Town/village trade screen / barter conversation; Perform buy goods specifically in the one item context
- Preconditions: Isolated one item context for buy goods
- Expected: Acceptance requirement for one item: buyer gold decreases and market/player item counts balance
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.001.case-02

Buy goods / stack

- Entry/action: Town/village trade screen / barter conversation; Perform buy goods specifically in the stack context
- Preconditions: Isolated stack context for buy goods
- Expected: Acceptance requirement for stack: buyer gold decreases and market/player item counts balance
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.001.case-03

Buy goods / insufficient gold

- Entry/action: Town/village trade screen / barter conversation; Perform buy goods specifically in the insufficient gold context
- Preconditions: Isolated insufficient gold context for buy goods
- Expected: Acceptance requirement for insufficient gold: buyer gold decreases and market/player item counts balance
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### menu.settlement.town.trade.select

Select town.trade

- Entry/action: Client campaign menu town; Choose the exact registered option trade
- Preconditions: The real menu town is reached; option trade condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_trade_on_condition; game_menu_town_town_market_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior@53-53
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## trade.002 Sell goods

Each entry retains authority and separate owner/observer observations in the CSV.

### trade.002.case-01

Sell goods / one item

- Entry/action: Town/village trade screen / barter conversation; Perform sell goods specifically in the one item context
- Preconditions: Isolated one item context for sell goods
- Expected: Acceptance requirement for one item: seller gold and market stock change by the completed transaction
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.002.case-02

Sell goods / stack

- Entry/action: Town/village trade screen / barter conversation; Perform sell goods specifically in the stack context
- Preconditions: Isolated stack context for sell goods
- Expected: Acceptance requirement for stack: seller gold and market stock change by the completed transaction
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.002.case-03

Sell goods / merchant funds exhausted

- Entry/action: Town/village trade screen / barter conversation; Perform sell goods specifically in the merchant funds exhausted context
- Preconditions: Isolated merchant funds exhausted context for sell goods
- Expected: Acceptance requirement for merchant funds exhausted: seller gold and market stock change by the completed transaction
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

## trade.003 Trade cancellation

Each entry retains authority and separate owner/observer observations in the CSV.

### trade.003.case-01

Trade cancellation / before Done

- Entry/action: Town/village trade screen / barter conversation; Perform trade cancellation specifically in the before Done context
- Preconditions: Isolated before Done context for trade cancellation
- Expected: Acceptance requirement for before Done: cancelled selection does not change final authoritative inventories
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.003.case-02

Trade cancellation / disconnect

- Entry/action: Town/village trade screen / barter conversation; Perform trade cancellation specifically in the disconnect context
- Preconditions: Isolated disconnect context for trade cancellation
- Expected: Acceptance requirement for disconnect: cancelled selection does not change final authoritative inventories
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.003.case-03

Trade cancellation / leave screen

- Entry/action: Town/village trade screen / barter conversation; Perform trade cancellation specifically in the leave screen context
- Preconditions: Isolated leave screen context for trade cancellation
- Expected: Acceptance requirement for leave screen: cancelled selection does not change final authoritative inventories
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

## trade.004 Trade price

Each entry retains authority and separate owner/observer observations in the CSV.

### trade.004.case-01

Trade price / scarcity

- Entry/action: Town/village trade screen / barter conversation; Perform trade price specifically in the scarcity context
- Preconditions: Isolated scarcity context for trade price
- Expected: Acceptance requirement for scarcity: quoted and charged price agree for the accepted item selection
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.004.case-02

Trade price / category

- Entry/action: Town/village trade screen / barter conversation; Perform trade price specifically in the category context
- Preconditions: Isolated category context for trade price
- Expected: Acceptance requirement for category: quoted and charged price agree for the accepted item selection
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.004.case-03

Trade price / modifier

- Entry/action: Town/village trade screen / barter conversation; Perform trade price specifically in the modifier context
- Preconditions: Isolated modifier context for trade price
- Expected: Acceptance requirement for modifier: quoted and charged price agree for the accepted item selection
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.004.case-04

Trade price / skill influence

- Entry/action: Town/village trade screen / barter conversation; Perform trade price specifically in the skill influence context
- Preconditions: Isolated skill influence context for trade price
- Expected: Acceptance requirement for skill influence: quoted and charged price agree for the accepted item selection
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### price.buy-rounding

Quote / buying rounding

- Entry/action: Town/village trade screen / barter conversation; Quote / buying rounding
- Preconditions: Buying; independently known item value and factor; caravan discount absent
- Expected: Ceiling of product; minimum price 1
- State: Ceiling of product; minimum price 1
- Side effects: Quote only, no item/gold transfer
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Caravan SilverTongue branch may further adjust quote
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel@184-194
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### price.sell-rounding

Quote / selling rounding

- Entry/action: Town/village trade screen / barter conversation; Quote / selling rounding
- Preconditions: Selling; independently known value and factor
- Expected: Floor of product; minimum price 1
- State: Floor of product; minimum price 1
- Side effects: Quote only
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Selling uses a different penalty factor direction
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel@184-194
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### price.trade-good-clamp

Base price / trade-good bounds

- Entry/action: Town/village trade screen / barter conversation; Base price / trade-good bounds
- Preconditions: Trade-good category; raw factor outside bounds
- Expected: Clamp to 0.1 through 10
- State: Clamp to 0.1 through 10
- Side effects: Base factor only, before penalty
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Do not apply these bounds to final integer price
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel@170-182
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### price.nontrade-clamp

Base price / non-trade bounds

- Entry/action: Town/village trade screen / barter conversation; Base price / non-trade bounds
- Preconditions: Category not marked trade good
- Expected: Clamp to 0.8 through 1.3
- State: Clamp to 0.8 through 1.3
- Side effects: Base factor only
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Animal exponent remains a separate input
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel@170-182
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## trade.005 Concurrent market trade

Each entry retains authority and separate owner/observer observations in the CSV.

### trade.005.case-01

Concurrent market trade / two clients purchase the same stock

- Entry/action: Town/village trade screen / barter conversation; Perform concurrent market trade specifically in the two clients purchase the same stock context
- Preconditions: Isolated two clients purchase the same stock context for concurrent market trade
- Expected: Acceptance requirement for two clients purchase the same stock: stock is conserved and each accepted transaction is applied once
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.full-stock

Purchase / full stock remains

- Entry/action: Town/village trade screen / barter conversation; Purchase / full stock remains
- Preconditions: Requested 5 for 100; merchant currently has at least 5
- Expected: Proposal counts and total amount unchanged
- State: Proposal counts and total amount unchanged
- Side effects: No refund adjustment
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Caller performs roster apply afterward
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: coop.trade@316-345
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [source/GameInterface/Services/Inventory/Handlers/TradeHandler.cs](../../source/GameInterface/Services/Inventory/Handlers/TradeHandler.cs)

### trade.partial-stock

Purchase / partial concurrent stock

- Entry/action: Town/village trade screen / barter conversation; Purchase / partial concurrent stock
- Preconditions: Requested 5 for 100; merchant currently has 3
- Expected: Party proposal loses 2; merchant proposal restored by 2; amount reduced by 40
- State: Party proposal loses 2; merchant proposal restored by 2; amount reduced by 40
- Side effects: Equipment and modifier identity match; integer prorating
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Other proposed transactions retain their own deltas
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: coop.trade@316-345
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [source/GameInterface/Services/Inventory/Handlers/TradeHandler.cs](../../source/GameInterface/Services/Inventory/Handlers/TradeHandler.cs)

### trade.empty-stock

Purchase / stock disappeared

- Entry/action: Town/village trade screen / barter conversation; Purchase / stock disappeared
- Preconditions: Requested 5 for 100; matching merchant equipment absent
- Expected: Party proposal loses 5; amount reduced by 100
- State: Party proposal loses 5; amount reduced by 100
- Side effects: No replacement item identity created
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Unavailable stock does not remain in purchaser proposal
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: coop.trade@316-345
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [source/GameInterface/Services/Inventory/Handlers/TradeHandler.cs](../../source/GameInterface/Services/Inventory/Handlers/TradeHandler.cs)

## trade.006 Trade XP

Each entry retains authority and separate owner/observer observations in the CSV.

### trade.006.case-01

Trade XP / profitable sale

- Entry/action: Town/village trade screen / barter conversation; Perform trade xp specifically in the profitable sale context
- Preconditions: Isolated profitable sale context for trade xp
- Expected: Acceptance requirement for profitable sale: eligible XP/reward belongs to the player who completed the sale
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.006.case-02

Trade XP / nonprofitable sale

- Entry/action: Town/village trade screen / barter conversation; Perform trade xp specifically in the nonprofitable sale context
- Preconditions: Isolated nonprofitable sale context for trade xp
- Expected: Acceptance requirement for nonprofitable sale: eligible XP/reward belongs to the player who completed the sale
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.006.case-03

Trade XP / equipment sale

- Entry/action: Town/village trade screen / barter conversation; Perform trade xp specifically in the equipment sale context
- Preconditions: Isolated equipment sale context for trade xp
- Expected: Acceptance requirement for equipment sale: eligible XP/reward belongs to the player who completed the sale
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

## trade.007 Trade rumors

Each entry retains authority and separate owner/observer observations in the CSV.

### trade.007.case-01

Trade rumors / new rumor

- Entry/action: Town/village trade screen / barter conversation; Perform trade rumors specifically in the new rumor context
- Preconditions: Isolated new rumor context for trade rumors
- Expected: Acceptance requirement for new rumor: the relevant player's rumor data and UI agree
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.007.case-02

Trade rumors / changed market

- Entry/action: Town/village trade screen / barter conversation; Perform trade rumors specifically in the changed market context
- Preconditions: Isolated changed market context for trade rumors
- Expected: Acceptance requirement for changed market: the relevant player's rumor data and UI agree
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.007.case-03

Trade rumors / expired rumor

- Entry/action: Town/village trade screen / barter conversation; Perform trade rumors specifically in the expired rumor context
- Preconditions: Isolated expired rumor context for trade rumors
- Expected: Acceptance requirement for expired rumor: the relevant player's rumor data and UI agree
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

## trade.008 Barter gold

Each entry retains authority and separate owner/observer observations in the CSV.

### trade.008.case-01

Barter gold / one-sided

- Entry/action: Town/village trade screen / barter conversation; Perform barter gold specifically in the one-sided context
- Preconditions: Isolated one-sided context for barter gold
- Expected: Acceptance requirement for one-sided: accepted gold transfer balances both participating heroes
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.008.case-02

Barter gold / two-sided

- Entry/action: Town/village trade screen / barter conversation; Perform barter gold specifically in the two-sided context
- Preconditions: Isolated two-sided context for barter gold
- Expected: Acceptance requirement for two-sided: accepted gold transfer balances both participating heroes
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.008.case-03

Barter gold / cancel

- Entry/action: Town/village trade screen / barter conversation; Perform barter gold specifically in the cancel context
- Preconditions: Isolated cancel context for barter gold
- Expected: Acceptance requirement for cancel: accepted gold transfer balances both participating heroes
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

## trade.009 Barter items

Each entry retains authority and separate owner/observer observations in the CSV.

### trade.009.case-01

Barter items / stacks

- Entry/action: Town/village trade screen / barter conversation; Perform barter items specifically in the stacks context
- Preconditions: Isolated stacks context for barter items
- Expected: Acceptance requirement for stacks: accepted item transfer conserves identities and counts
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.009.case-02

Barter items / modifiers

- Entry/action: Town/village trade screen / barter conversation; Perform barter items specifically in the modifiers context
- Preconditions: Isolated modifiers context for barter items
- Expected: Acceptance requirement for modifiers: accepted item transfer conserves identities and counts
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.009.case-03

Barter items / insufficient quantity

- Entry/action: Town/village trade screen / barter conversation; Perform barter items specifically in the insufficient quantity context
- Preconditions: Isolated insufficient quantity context for barter items
- Expected: Acceptance requirement for insufficient quantity: accepted item transfer conserves identities and counts
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

## trade.010 Barter prisoners

Each entry retains authority and separate owner/observer observations in the CSV.

### trade.010.case-01

Barter prisoners / hero

- Entry/action: Town/village trade screen / barter conversation; Perform barter prisoners specifically in the hero context
- Preconditions: Isolated hero context for barter prisoners
- Expected: Acceptance requirement for hero: only accepted transferable captives change owner
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.010.case-02

Barter prisoners / ordinary prisoner

- Entry/action: Town/village trade screen / barter conversation; Perform barter prisoners specifically in the ordinary prisoner context
- Preconditions: Isolated ordinary prisoner context for barter prisoners
- Expected: Acceptance requirement for ordinary prisoner: only accepted transferable captives change owner
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.010.case-03

Barter prisoners / unavailable captive

- Entry/action: Town/village trade screen / barter conversation; Perform barter prisoners specifically in the unavailable captive context
- Preconditions: Isolated unavailable captive context for barter prisoners
- Expected: Acceptance requirement for unavailable captive: only accepted transferable captives change owner
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

## trade.011 Barter fief

Each entry retains authority and separate owner/observer observations in the CSV.

### trade.011.case-01

Barter fief / eligible

- Entry/action: Town/village trade screen / barter conversation; Perform barter fief specifically in the eligible context
- Preconditions: Isolated eligible context for barter fief
- Expected: Acceptance requirement for eligible: accepted ownership change triggers the required settlement/clan effects
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.011.case-02

Barter fief / restricted

- Entry/action: Town/village trade screen / barter conversation; Perform barter fief specifically in the restricted context
- Preconditions: Isolated restricted context for barter fief
- Expected: Acceptance requirement for restricted: accepted ownership change triggers the required settlement/clan effects
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.011.case-03

Barter fief / cancel

- Entry/action: Town/village trade screen / barter conversation; Perform barter fief specifically in the cancel context
- Preconditions: Isolated cancel context for barter fief
- Expected: Acceptance requirement for cancel: accepted ownership change triggers the required settlement/clan effects
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

## trade.012 Barter agreement

Each entry retains authority and separate owner/observer observations in the CSV.

### trade.012.case-01

Barter agreement / peace

- Entry/action: Town/village trade screen / barter conversation; Perform barter agreement specifically in the peace context
- Preconditions: Isolated peace context for barter agreement
- Expected: Acceptance requirement for peace: agreement side effects occur only after accepted terms
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.012.case-02

Barter agreement / marriage

- Entry/action: Town/village trade screen / barter conversation; Perform barter agreement specifically in the marriage context
- Preconditions: Isolated marriage context for barter agreement
- Expected: Acceptance requirement for marriage: agreement side effects occur only after accepted terms
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)

### trade.012.case-03

Barter agreement / join faction

- Entry/action: Town/village trade screen / barter conversation; Perform barter agreement specifically in the join faction context
- Preconditions: Isolated join faction context for barter agreement
- Expected: Acceptance requirement for join faction: agreement side effects occur only after accepted terms
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

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/Barters](../../source/GameInterface/Services/Barters), [source/GameInterface/Services/TownMarketDatas](../../source/GameInterface/Services/TownMarketDatas), [source/GameInterface/Services/VillageMarketDatas](../../source/GameInterface/Services/VillageMarketDatas)
