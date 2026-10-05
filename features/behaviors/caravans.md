# caravans behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## caravans.001 Create caravan

Each entry retains authority and separate owner/observer observations in the CSV.

### caravans.001.case-01

Create caravan / leader choice

- Entry/action: Notable conversation / clan parties / campaign map; Perform create caravan specifically in the leader choice context
- Preconditions: Isolated leader choice context for create caravan
- Expected: Acceptance requirement for leader choice: one registered caravan is created for the intended owner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### caravans.001.case-02.1

Create caravan / normal escort

- Entry/action: Notable conversation / clan parties / campaign map; Perform create caravan specifically in the normal escort context
- Preconditions: Isolated normal escort context for create caravan
- Expected: Acceptance requirement for normal escort: one registered caravan is created for the intended owner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### caravans.001.case-02.2

Create caravan / strong escort

- Entry/action: Notable conversation / clan parties / campaign map; Perform create caravan specifically in the strong escort context
- Preconditions: Isolated strong escort context for create caravan
- Expected: Acceptance requirement for strong escort: one registered caravan is created for the intended owner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### caravans.001.case-03

Create caravan / fee

- Entry/action: Notable conversation / clan parties / campaign map; Perform create caravan specifically in the fee context
- Preconditions: Isolated fee context for create caravan
- Expected: Acceptance requirement for fee: one registered caravan is created for the intended owner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## caravans.002 Caravan trading

Each entry retains authority and separate owner/observer observations in the CSV.

### caravans.002.case-01

Caravan trading / visit market

- Entry/action: Notable conversation / clan parties / campaign map; Perform caravan trading specifically in the visit market context
- Preconditions: Isolated visit market context for caravan trading
- Expected: Acceptance requirement for visit market: caravan stock, funds and owner income follow the authoritative transaction
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### caravans.002.case-02

Caravan trading / buy

- Entry/action: Notable conversation / clan parties / campaign map; Perform caravan trading specifically in the buy context
- Preconditions: Isolated buy context for caravan trading
- Expected: Acceptance requirement for buy: caravan stock, funds and owner income follow the authoritative transaction
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### caravans.002.case-03

Caravan trading / sell

- Entry/action: Notable conversation / clan parties / campaign map; Perform caravan trading specifically in the sell context
- Preconditions: Isolated sell context for caravan trading
- Expected: Acceptance requirement for sell: caravan stock, funds and owner income follow the authoritative transaction
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## caravans.003 Caravan movement

Each entry retains authority and separate owner/observer observations in the CSV.

### caravans.003.case-01

Caravan movement / destination choice

- Entry/action: Notable conversation / clan parties / campaign map; Perform caravan movement specifically in the destination choice context
- Preconditions: Isolated destination choice context for caravan movement
- Expected: Acceptance requirement for destination choice: all peers observe the same caravan identity and relevant position
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### caravans.003.case-02

Caravan movement / route

- Entry/action: Notable conversation / clan parties / campaign map; Perform caravan movement specifically in the route context
- Preconditions: Isolated route context for caravan movement
- Expected: Acceptance requirement for route: all peers observe the same caravan identity and relevant position
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### caravans.003.case-03

Caravan movement / threat response

- Entry/action: Notable conversation / clan parties / campaign map; Perform caravan movement specifically in the threat response context
- Preconditions: Isolated threat response context for caravan movement
- Expected: Acceptance requirement for threat response: all peers observe the same caravan identity and relevant position
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## caravans.004 Caravan capture

Each entry retains authority and separate owner/observer observations in the CSV.

### caravans.004.case-01

Caravan capture / battle defeat

- Entry/action: Notable conversation / clan parties / campaign map; Perform caravan capture specifically in the battle defeat context
- Preconditions: Isolated battle defeat context for caravan capture
- Expected: Acceptance requirement for battle defeat: caravan removal and leader captivity converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### caravans.004.case-02

Caravan capture / prisoners

- Entry/action: Notable conversation / clan parties / campaign map; Perform caravan capture specifically in the prisoners context
- Preconditions: Isolated prisoners context for caravan capture
- Expected: Acceptance requirement for prisoners: caravan removal and leader captivity converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### caravans.004.case-03

Caravan capture / lost inventory

- Entry/action: Notable conversation / clan parties / campaign map; Perform caravan capture specifically in the lost inventory context
- Preconditions: Isolated lost inventory context for caravan capture
- Expected: Acceptance requirement for lost inventory: caravan removal and leader captivity converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## caravans.005 Disband caravan

Each entry retains authority and separate owner/observer observations in the CSV.

### caravans.005.case-01

Disband caravan / owner action

- Entry/action: Notable conversation / clan parties / campaign map; Perform disband caravan specifically in the owner action context
- Preconditions: Isolated owner action context for disband caravan
- Expected: Acceptance requirement for owner action: caravan and owner accounting reach the intended final state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### caravans.005.case-02

Disband caravan / travelling

- Entry/action: Notable conversation / clan parties / campaign map; Perform disband caravan specifically in the travelling context
- Preconditions: Isolated travelling context for disband caravan
- Expected: Acceptance requirement for travelling: caravan and owner accounting reach the intended final state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### caravans.005.case-03

Disband caravan / in settlement

- Entry/action: Notable conversation / clan parties / campaign map; Perform disband caravan specifically in the in settlement context
- Preconditions: Isolated in settlement context for disband caravan
- Expected: Acceptance requirement for in settlement: caravan and owner accounting reach the intended final state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

## caravans.006 Caravan replacement after loss

Each entry retains authority and separate owner/observer observations in the CSV.

### caravans.006.case-01

Caravan replacement after loss / old leader captive

- Entry/action: Notable conversation / clan parties / campaign map; Perform caravan replacement after loss specifically in the old leader captive context
- Preconditions: Isolated old leader captive context for caravan replacement after loss
- Expected: Acceptance requirement for old leader captive: replacement does not reuse a live party identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)

### caravans.006.case-02

Caravan replacement after loss / new leader

- Entry/action: Notable conversation / clan parties / campaign map; Perform caravan replacement after loss specifically in the new leader context
- Preconditions: Isolated new leader context for caravan replacement after loss
- Expected: Acceptance requirement for new leader: replacement does not reuse a live party identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Caravans](../../source/GameInterface/Services/Caravans), [source/GameInterface/Services/PartyComponents](../../source/GameInterface/Services/PartyComponents), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties)
