# workshops behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## workshops.001 Purchase workshop

Each entry retains authority and separate owner/observer observations in the CSV.

### workshops.001.case-01

Purchase workshop / available seller

- Entry/action: Town workshop conversation / clan finance; Perform purchase workshop specifically in the available seller context
- Preconditions: Isolated available seller context for purchase workshop
- Expected: Acceptance requirement for available seller: payment and workshop owner change occur once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### workshops.001.case-02

Purchase workshop / ownership limit

- Entry/action: Town workshop conversation / clan finance; Perform purchase workshop specifically in the ownership limit context
- Preconditions: Isolated ownership limit context for purchase workshop
- Expected: Acceptance requirement for ownership limit: payment and workshop owner change occur once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### workshops.001.case-03

Purchase workshop / insufficient gold

- Entry/action: Town workshop conversation / clan finance; Perform purchase workshop specifically in the insufficient gold context
- Preconditions: Isolated insufficient gold context for purchase workshop
- Expected: Acceptance requirement for insufficient gold: payment and workshop owner change occur once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## workshops.002 Sell workshop

Each entry retains authority and separate owner/observer observations in the CSV.

### workshops.002.case-01

Sell workshop / owned

- Entry/action: Town workshop conversation / clan finance; Perform sell workshop specifically in the owned context
- Preconditions: Isolated owned context for sell workshop
- Expected: Acceptance requirement for owned: sale proceeds reach the intended owner and ownership updates
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### workshops.002.case-02

Sell workshop / unowned

- Entry/action: Town workshop conversation / clan finance; Perform sell workshop specifically in the unowned context
- Preconditions: Isolated unowned context for sell workshop
- Expected: Acceptance requirement for unowned: sale proceeds reach the intended owner and ownership updates
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### workshops.002.case-03

Sell workshop / ownership changed

- Entry/action: Town workshop conversation / clan finance; Perform sell workshop specifically in the ownership changed context
- Preconditions: Isolated ownership changed context for sell workshop
- Expected: Acceptance requirement for ownership changed: sale proceeds reach the intended owner and ownership updates
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## workshops.003 Change workshop production

Each entry retains authority and separate owner/observer observations in the CSV.

### workshops.003.case-01

Change workshop production / valid production type

- Entry/action: Town workshop conversation / clan finance; Perform change workshop production specifically in the valid production type context
- Preconditions: Isolated valid production type context for change workshop production
- Expected: Acceptance requirement for valid production type: production type and inventory/cost effects converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### workshops.003.case-02

Change workshop production / payment

- Entry/action: Town workshop conversation / clan finance; Perform change workshop production specifically in the payment context
- Preconditions: Isolated payment context for change workshop production
- Expected: Acceptance requirement for payment: production type and inventory/cost effects converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## workshops.004 Workshop production tick

Each entry retains authority and separate owner/observer observations in the CSV.

### workshops.004.case-01

Workshop production tick / inputs available

- Entry/action: Town workshop conversation / clan finance; Perform workshop production tick specifically in the inputs available context
- Preconditions: Isolated inputs available context for workshop production tick
- Expected: Acceptance requirement for inputs available: input consumption and output stock follow the authoritative tick
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### workshops.004.case-02

Workshop production tick / unavailable

- Entry/action: Town workshop conversation / clan finance; Perform workshop production tick specifically in the unavailable context
- Preconditions: Isolated unavailable context for workshop production tick
- Expected: Acceptance requirement for unavailable: input consumption and output stock follow the authoritative tick
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### workshops.004.case-03

Workshop production tick / storage

- Entry/action: Town workshop conversation / clan finance; Perform workshop production tick specifically in the storage context
- Preconditions: Isolated storage context for workshop production tick
- Expected: Acceptance requirement for storage: input consumption and output stock follow the authoritative tick
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## workshops.005 Workshop income

Each entry retains authority and separate owner/observer observations in the CSV.

### workshops.005.case-01

Workshop income / profitable

- Entry/action: Town workshop conversation / clan finance; Perform workshop income specifically in the profitable context
- Preconditions: Isolated profitable context for workshop income
- Expected: Acceptance requirement for profitable: income belongs to the correct owning hero/clan
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### workshops.005.case-02

Workshop income / unprofitable

- Entry/action: Town workshop conversation / clan finance; Perform workshop income specifically in the unprofitable context
- Preconditions: Isolated unprofitable context for workshop income
- Expected: Acceptance requirement for unprofitable: income belongs to the correct owning hero/clan
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### workshops.005.case-03

Workshop income / daily settlement

- Entry/action: Town workshop conversation / clan finance; Perform workshop income specifically in the daily settlement context
- Preconditions: Isolated daily settlement context for workshop income
- Expected: Acceptance requirement for daily settlement: income belongs to the correct owning hero/clan
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## workshops.006 Workshop destruction or confiscation

Each entry retains authority and separate owner/observer observations in the CSV.

### workshops.006.case-01

Workshop destruction or confiscation / war

- Entry/action: Town workshop conversation / clan finance; Perform workshop destruction or confiscation specifically in the war context
- Preconditions: Isolated war context for workshop destruction or confiscation
- Expected: Acceptance requirement for war: ownership and subsequent income stop/change consistently
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### workshops.006.case-02

Workshop destruction or confiscation / settlement capture

- Entry/action: Town workshop conversation / clan finance; Perform workshop destruction or confiscation specifically in the settlement capture context
- Preconditions: Isolated settlement capture context for workshop destruction or confiscation
- Expected: Acceptance requirement for settlement capture: ownership and subsequent income stop/change consistently
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### workshops.006.case-03

Workshop destruction or confiscation / lost ownership

- Entry/action: Town workshop conversation / clan finance; Perform workshop destruction or confiscation specifically in the lost ownership context
- Preconditions: Isolated lost ownership context for workshop destruction or confiscation
- Expected: Acceptance requirement for lost ownership: ownership and subsequent income stop/change consistently
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Workshops](../../source/GameInterface/Services/Workshops), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)
