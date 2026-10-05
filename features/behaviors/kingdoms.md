# kingdoms behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## kingdoms.001 Create kingdom

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.001.case-01

Create kingdom / eligibility

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform create kingdom specifically in the eligibility context
- Preconditions: Isolated eligibility context for create kingdom
- Expected: Acceptance requirement for eligibility: one registered kingdom is created with intended leader and clan
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.001.case-02

Create kingdom / governor conversation

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform create kingdom specifically in the governor conversation context
- Preconditions: Isolated governor conversation context for create kingdom
- Expected: Acceptance requirement for governor conversation: one registered kingdom is created with intended leader and clan
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.001.case-03

Create kingdom / naming

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform create kingdom specifically in the naming context
- Preconditions: Isolated naming context for create kingdom
- Expected: Acceptance requirement for naming: one registered kingdom is created with intended leader and clan
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### king.independence

Kingdom / independence gate

- Entry/action: Kingdom screen / diplomacy / decision UI; Kingdom / independence gate
- Preconditions: Player faction already kingdom faction
- Expected: Creation false; independence explanation
- State: Creation false; independence explanation
- Side effects: Eligibility only
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Other failing explanations may also accumulate
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultKingdomCreationModel@22-54
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### king.tier

Kingdom / clan tier gate

- Entry/action: Kingdom screen / diplomacy / decision UI; Kingdom / clan tier gate
- Preconditions: Clan tier below 4
- Expected: Creation false; tier explanation
- State: Creation false; tier explanation
- Side effects: No kingdom created by eligibility method
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Tier exactly 4 clears this gate
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultKingdomCreationModel@22-54
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### king.fief

Kingdom / settlement gate

- Entry/action: Kingdom screen / diplomacy / decision UI; Kingdom / settlement gate
- Preconditions: No owned town or castle
- Expected: Creation false; settlement explanation
- State: Creation false; settlement explanation
- Side effects: Village ownership alone does not satisfy count
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: At least one town or castle required
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultKingdomCreationModel@22-54
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### king.troops

Kingdom / healthy troop gate

- Entry/action: Kingdom screen / diplomacy / decision UI; Kingdom / healthy troop gate
- Preconditions: Healthy clan war parties plus fief garrisons below 100
- Expected: Creation false; troop explanation
- State: Creation false; troop explanation
- Side effects: Wounded troops excluded by TotalHealthyCount
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Exactly 100 clears this guard
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultKingdomCreationModel@22-54
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### king.eligible

Kingdom / eligibility

- Entry/action: Kingdom screen / diplomacy / decision UI; Kingdom / eligibility
- Preconditions: Independent, tier at least 4, town/castle owned, counted healthy troops at least 100
- Expected: Model returns true
- State: Model returns true
- Side effects: No kingdom creation by this member
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Vanilla MainHero/PlayerClan context needs co-op owner routing
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.GameComponents.DefaultKingdomCreationModel@22-54
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## kingdoms.002 Dissolve kingdom

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.002.case-01

Dissolve kingdom / valid conditions

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform dissolve kingdom specifically in the valid conditions context
- Preconditions: Isolated valid conditions context for dissolve kingdom
- Expected: Acceptance requirement for valid conditions: membership and stance links reach the correct final state
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.002.case-02.1

Dissolve kingdom / remaining clans

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform dissolve kingdom specifically in the remaining clans context
- Preconditions: Isolated remaining clans context for dissolve kingdom
- Expected: Acceptance requirement for remaining clans: membership and stance links reach the correct final state
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.002.case-02.2

Dissolve kingdom / remaining fiefs

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform dissolve kingdom specifically in the remaining fiefs context
- Preconditions: Isolated remaining fiefs context for dissolve kingdom
- Expected: Acceptance requirement for remaining fiefs: membership and stance links reach the correct final state
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.003 Declare war

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.003.case-01

Declare war / proposal

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform declare war specifically in the proposal context
- Preconditions: Isolated proposal context for declare war
- Expected: Acceptance requirement for proposal: all affected factions share the authoritative war stance
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.003.case-02

Declare war / decision

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform declare war specifically in the decision context
- Preconditions: Isolated decision context for declare war
- Expected: Acceptance requirement for decision: all affected factions share the authoritative war stance
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.003.case-03

Declare war / direct action

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform declare war specifically in the direct action context
- Preconditions: Isolated direct action context for declare war
- Expected: Acceptance requirement for direct action: all affected factions share the authoritative war stance
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.004 Make peace

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.004.case-01

Make peace / proposal

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform make peace specifically in the proposal context
- Preconditions: Isolated proposal context for make peace
- Expected: Acceptance requirement for proposal: peace state and accepted payments converge
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.004.case-02

Make peace / decision

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform make peace specifically in the decision context
- Preconditions: Isolated decision context for make peace
- Expected: Acceptance requirement for decision: peace state and accepted payments converge
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.004.case-03

Make peace / tribute terms

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform make peace specifically in the tribute terms context
- Preconditions: Isolated tribute terms context for make peace
- Expected: Acceptance requirement for tribute terms: peace state and accepted payments converge
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.005 Form alliance

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.005.case-01

Form alliance / proposal

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform form alliance specifically in the proposal context
- Preconditions: Isolated proposal context for form alliance
- Expected: Acceptance requirement for proposal: agreement state changes once under current eligibility rules
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.005.case-02

Form alliance / accept

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform form alliance specifically in the accept context
- Preconditions: Isolated accept context for form alliance
- Expected: Acceptance requirement for accept: agreement state changes once under current eligibility rules
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.005.case-03

Form alliance / reject

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform form alliance specifically in the reject context
- Preconditions: Isolated reject context for form alliance
- Expected: Acceptance requirement for reject: agreement state changes once under current eligibility rules
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.005.case-04

Form alliance / existing alliance

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform form alliance specifically in the existing alliance context
- Preconditions: Isolated existing alliance context for form alliance
- Expected: Acceptance requirement for existing alliance: agreement state changes once under current eligibility rules
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.006 End alliance

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.006.case-01

End alliance / expiry

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform end alliance specifically in the expiry context
- Preconditions: Isolated expiry context for end alliance
- Expected: Acceptance requirement for expiry: stance/agreement state and downstream UI agree
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.006.case-02

End alliance / war

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform end alliance specifically in the war context
- Preconditions: Isolated war context for end alliance
- Expected: Acceptance requirement for war: stance/agreement state and downstream UI agree
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.006.case-03

End alliance / explicit action

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform end alliance specifically in the explicit action context
- Preconditions: Isolated explicit action context for end alliance
- Expected: Acceptance requirement for explicit action: stance/agreement state and downstream UI agree
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.007 Create trade agreement

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.007.case-01

Create trade agreement / proposal

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform create trade agreement specifically in the proposal context
- Preconditions: Isolated proposal context for create trade agreement
- Expected: Acceptance requirement for proposal: agreement and economic consequences reach affected factions
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.007.case-02

Create trade agreement / acceptance

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform create trade agreement specifically in the acceptance context
- Preconditions: Isolated acceptance context for create trade agreement
- Expected: Acceptance requirement for acceptance: agreement and economic consequences reach affected factions
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.007.case-03

Create trade agreement / expiration

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform create trade agreement specifically in the expiration context
- Preconditions: Isolated expiration context for create trade agreement
- Expected: Acceptance requirement for expiration: agreement and economic consequences reach affected factions
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.008 Call ally to war

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.008.case-01

Call ally to war / propose

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform call ally to war specifically in the propose context
- Preconditions: Isolated propose context for call ally to war
- Expected: Acceptance requirement for propose: war entry follows only an accepted current call
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.008.case-02

Call ally to war / ally decision

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform call ally to war specifically in the ally decision context
- Preconditions: Isolated ally decision context for call ally to war
- Expected: Acceptance requirement for ally decision: war entry follows only an accepted current call
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.008.case-03.1

Call ally to war / accept

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform call ally to war specifically in the accept context
- Preconditions: Isolated accept context for call ally to war
- Expected: Acceptance requirement for accept: war entry follows only an accepted current call
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.008.case-03.2

Call ally to war / reject

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform call ally to war specifically in the reject context
- Preconditions: Isolated reject context for call ally to war
- Expected: Acceptance requirement for reject: war entry follows only an accepted current call
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.009 Pay tribute

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.009.case-01

Pay tribute / daily tick

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform pay tribute specifically in the daily tick context
- Preconditions: Isolated daily tick context for pay tribute
- Expected: Acceptance requirement for daily tick: payer and payee accounting reflects the active agreement
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.009.case-02

Pay tribute / changed terms

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform pay tribute specifically in the changed terms context
- Preconditions: Isolated changed terms context for pay tribute
- Expected: Acceptance requirement for changed terms: payer and payee accounting reflects the active agreement
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.009.case-03

Pay tribute / peace ends

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform pay tribute specifically in the peace ends context
- Preconditions: Isolated peace ends context for pay tribute
- Expected: Acceptance requirement for peace ends: payer and payee accounting reflects the active agreement
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.010 Propose kingdom policy

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.010.case-01

Propose kingdom policy / new

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform propose kingdom policy specifically in the new context
- Preconditions: Isolated new context for propose kingdom policy
- Expected: Acceptance requirement for new: a valid current decision is registered or rejected with reason
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.010.case-02

Propose kingdom policy / already active

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform propose kingdom policy specifically in the already active context
- Preconditions: Isolated already active context for propose kingdom policy
- Expected: Acceptance requirement for already active: a valid current decision is registered or rejected with reason
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.010.case-03

Propose kingdom policy / insufficient influence

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform propose kingdom policy specifically in the insufficient influence context
- Preconditions: Isolated insufficient influence context for propose kingdom policy
- Expected: Acceptance requirement for insufficient influence: a valid current decision is registered or rejected with reason
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.011 Revoke kingdom policy

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.011.case-01

Revoke kingdom policy / active policy

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform revoke kingdom policy specifically in the active policy context
- Preconditions: Isolated active policy context for revoke kingdom policy
- Expected: Acceptance requirement for active policy: accepted outcome removes the correct policy
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.011.case-02

Revoke kingdom policy / invalid policy

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform revoke kingdom policy specifically in the invalid policy context
- Preconditions: Isolated invalid policy context for revoke kingdom policy
- Expected: Acceptance requirement for invalid policy: accepted outcome removes the correct policy
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.012 Allocate settlement

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.012.case-01

Allocate settlement / preliminary claimant

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform allocate settlement specifically in the preliminary claimant context
- Preconditions: Isolated preliminary claimant context for allocate settlement
- Expected: Acceptance requirement for preliminary claimant: accepted decision assigns the intended eligible clan
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.012.case-02

Allocate settlement / final claimant

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform allocate settlement specifically in the final claimant context
- Preconditions: Isolated final claimant context for allocate settlement
- Expected: Acceptance requirement for final claimant: accepted decision assigns the intended eligible clan
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.012.case-03

Allocate settlement / owner changed

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform allocate settlement specifically in the owner changed context
- Preconditions: Isolated owner changed context for allocate settlement
- Expected: Acceptance requirement for owner changed: accepted decision assigns the intended eligible clan
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.013 Elect ruler

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.013.case-01

Elect ruler / leader death

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform elect ruler specifically in the leader death context
- Preconditions: Isolated leader death context for elect ruler
- Expected: Acceptance requirement for leader death: one eligible leader becomes authoritative
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.013.case-02

Elect ruler / candidates

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform elect ruler specifically in the candidates context
- Preconditions: Isolated candidates context for elect ruler
- Expected: Acceptance requirement for candidates: one eligible leader becomes authoritative
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.013.case-03

Elect ruler / outcome

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform elect ruler specifically in the outcome context
- Preconditions: Isolated outcome context for elect ruler
- Expected: Acceptance requirement for outcome: one eligible leader becomes authoritative
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.014 Expel clan

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.014.case-01

Expel clan / proposal

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform expel clan specifically in the proposal context
- Preconditions: Isolated proposal context for expel clan
- Expected: Acceptance requirement for proposal: accepted expulsion changes clan membership and side effects
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.014.case-02

Expel clan / influence

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform expel clan specifically in the influence context
- Preconditions: Isolated influence context for expel clan
- Expected: Acceptance requirement for influence: accepted expulsion changes clan membership and side effects
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.014.case-03

Expel clan / voting

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform expel clan specifically in the voting context
- Preconditions: Isolated voting context for expel clan
- Expected: Acceptance requirement for voting: accepted expulsion changes clan membership and side effects
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.015 Submit decision vote

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.015.case-01

Submit decision vote / support

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform submit decision vote specifically in the support context
- Preconditions: Isolated support context for submit decision vote
- Expected: Acceptance requirement for support: vote belongs to the player's clan and current decision round
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.015.case-02

Submit decision vote / oppose

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform submit decision vote specifically in the oppose context
- Preconditions: Isolated oppose context for submit decision vote
- Expected: Acceptance requirement for oppose: vote belongs to the player's clan and current decision round
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.015.case-03

Submit decision vote / abstain

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform submit decision vote specifically in the abstain context
- Preconditions: Isolated abstain context for submit decision vote
- Expected: Acceptance requirement for abstain: vote belongs to the player's clan and current decision round
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.015.case-04

Submit decision vote / influence strength

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform submit decision vote specifically in the influence strength context
- Preconditions: Isolated influence strength context for submit decision vote
- Expected: Acceptance requirement for influence strength: vote belongs to the player's clan and current decision round
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.016 Update decision vote

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.016.case-01

Update decision vote / change strength

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform update decision vote specifically in the change strength context
- Preconditions: Isolated change strength context for update decision vote
- Expected: Acceptance requirement for change strength: one current vote replaces the prior vote under round rules
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.016.case-02

Update decision vote / repeated click

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform update decision vote specifically in the repeated click context
- Preconditions: Isolated repeated click context for update decision vote
- Expected: Acceptance requirement for repeated click: one current vote replaces the prior vote under round rules
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.016.case-03

Update decision vote / late vote

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform update decision vote specifically in the late vote context
- Preconditions: Isolated late vote context for update decision vote
- Expected: Acceptance requirement for late vote: one current vote replaces the prior vote under round rules
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.017 Decision deadline

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.017.case-01

Decision deadline / before

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform decision deadline specifically in the before context
- Preconditions: Isolated before context for decision deadline
- Expected: Acceptance requirement for before: round closes once and late requests do not reopen it
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.017.case-02

Decision deadline / at

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform decision deadline specifically in the at context
- Preconditions: Isolated at context for decision deadline
- Expected: Acceptance requirement for at: round closes once and late requests do not reopen it
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.017.case-03

Decision deadline / after expiration

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform decision deadline specifically in the after expiration context
- Preconditions: Isolated after expiration context for decision deadline
- Expected: Acceptance requirement for after expiration: round closes once and late requests do not reopen it
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.018 Resolve decision

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.018.case-01

Resolve decision / accepted outcome

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform resolve decision specifically in the accepted outcome context
- Preconditions: Isolated accepted outcome context for resolve decision
- Expected: Acceptance requirement for accepted outcome: outcome is applied once and pending UI clears
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.018.case-02

Resolve decision / rejected outcome

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform resolve decision specifically in the rejected outcome context
- Preconditions: Isolated rejected outcome context for resolve decision
- Expected: Acceptance requirement for rejected outcome: outcome is applied once and pending UI clears
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.018.case-03

Resolve decision / removed decision

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform resolve decision specifically in the removed decision context
- Preconditions: Isolated removed decision context for resolve decision
- Expected: Acceptance requirement for removed decision: outcome is applied once and pending UI clears
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.019 Decision presentation

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.019.case-01

Decision presentation / waiting

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform decision presentation specifically in the waiting context
- Preconditions: Isolated waiting context for decision presentation
- Expected: Acceptance requirement for waiting: client UI agrees with the same round/session identity
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.019.case-02

Decision presentation / can vote

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform decision presentation specifically in the can vote context
- Preconditions: Isolated can vote context for decision presentation
- Expected: Acceptance requirement for can vote: client UI agrees with the same round/session identity
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.019.case-03

Decision presentation / resolved

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform decision presentation specifically in the resolved context
- Preconditions: Isolated resolved context for decision presentation
- Expected: Acceptance requirement for resolved: client UI agrees with the same round/session identity
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

## kingdoms.020 Concurrent proposals

Each entry retains authority and separate owner/observer observations in the CSV.

### kingdoms.020.case-01

Concurrent proposals / same subject

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform concurrent proposals specifically in the same subject context
- Preconditions: Isolated same subject context for concurrent proposals
- Expected: Acceptance requirement for same subject: only valid current decisions reach an authoritative outcome
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.020.case-02.1

Concurrent proposals / conflicting war proposal

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform concurrent proposals specifically in the conflicting war proposal context
- Preconditions: Isolated conflicting war proposal context for concurrent proposals
- Expected: Acceptance requirement for conflicting war proposal: only valid current decisions reach an authoritative outcome
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.020.case-02.2

Concurrent proposals / conflicting peace proposal

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform concurrent proposals specifically in the conflicting peace proposal context
- Preconditions: Isolated conflicting peace proposal context for concurrent proposals
- Expected: Acceptance requirement for conflicting peace proposal: only valid current decisions reach an authoritative outcome
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)

### kingdoms.020.case-03

Concurrent proposals / decision removed

- Entry/action: Kingdom screen / diplomacy / decision UI; Perform concurrent proposals specifically in the decision removed context
- Preconditions: Isolated decision removed context for concurrent proposals
- Expected: Acceptance requirement for decision removed: only valid current decisions reach an authoritative outcome
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

Sources: [source/GameInterface/Services/Kingdoms](../../source/GameInterface/Services/Kingdoms), [source/GameInterface/Services/Alliances](../../source/GameInterface/Services/Alliances), [source/GameInterface/Services/StanceLinks](../../source/GameInterface/Services/StanceLinks), [source/Coop.Core/Server/Services/Kingdoms](../../source/Coop.Core/Server/Services/Kingdoms)
