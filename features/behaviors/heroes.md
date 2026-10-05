# heroes behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## heroes.001 Change hero gold

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.001.case-01

Change hero gold / reward

- Entry/action: Hero / companion / family interfaces; Perform change hero gold specifically in the reward context
- Preconditions: Isolated reward context for change hero gold
- Expected: Acceptance requirement for reward: hero gold changes once and agrees on both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.001.case-02

Change hero gold / purchase

- Entry/action: Hero / companion / family interfaces; Perform change hero gold specifically in the purchase context
- Preconditions: Isolated purchase context for change hero gold
- Expected: Acceptance requirement for purchase: hero gold changes once and agrees on both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.001.case-03

Change hero gold / transfer

- Entry/action: Hero / companion / family interfaces; Perform change hero gold specifically in the transfer context
- Preconditions: Isolated transfer context for change hero gold
- Expected: Acceptance requirement for transfer: hero gold changes once and agrees on both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.001.case-04

Change hero gold / loss

- Entry/action: Hero / companion / family interfaces; Perform change hero gold specifically in the loss context
- Preconditions: Isolated loss context for change hero gold
- Expected: Acceptance requirement for loss: hero gold changes once and agrees on both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.002 Change hero state

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.002.case-01

Change hero state / active

- Entry/action: Hero / companion / family interfaces; Perform change hero state specifically in the active context
- Preconditions: Isolated active context for change hero state
- Expected: Acceptance requirement for active: state transitions and campaign membership remain consistent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.002.case-02

Change hero state / wounded

- Entry/action: Hero / companion / family interfaces; Perform change hero state specifically in the wounded context
- Preconditions: Isolated wounded context for change hero state
- Expected: Acceptance requirement for wounded: state transitions and campaign membership remain consistent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.002.case-03

Change hero state / prisoner

- Entry/action: Hero / companion / family interfaces; Perform change hero state specifically in the prisoner context
- Preconditions: Isolated prisoner context for change hero state
- Expected: Acceptance requirement for prisoner: state transitions and campaign membership remain consistent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.002.case-04

Change hero state / dead

- Entry/action: Hero / companion / family interfaces; Perform change hero state specifically in the dead context
- Preconditions: Isolated dead context for change hero state
- Expected: Acceptance requirement for dead: state transitions and campaign membership remain consistent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.002.case-05

Change hero state / disabled

- Entry/action: Hero / companion / family interfaces; Perform change hero state specifically in the disabled context
- Preconditions: Isolated disabled context for change hero state
- Expected: Acceptance requirement for disabled: state transitions and campaign membership remain consistent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.003 Hire companion

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.003.case-01

Hire companion / tavern conversation

- Entry/action: Hero / companion / family interfaces; Perform hire companion specifically in the tavern conversation context
- Preconditions: Isolated tavern conversation context for hire companion
- Expected: Acceptance requirement for tavern conversation: hired hero joins the intended clan and party after payment
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.003.case-02

Hire companion / fee

- Entry/action: Hero / companion / family interfaces; Perform hire companion specifically in the fee context
- Preconditions: Isolated fee context for hire companion
- Expected: Acceptance requirement for fee: hired hero joins the intended clan and party after payment
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.003.case-03

Hire companion / party full

- Entry/action: Hero / companion / family interfaces; Perform hire companion specifically in the party full context
- Preconditions: Isolated party full context for hire companion
- Expected: Acceptance requirement for party full: hired hero joins the intended clan and party after payment
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.004 Dismiss companion

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.004.case-01

Dismiss companion / conversation

- Entry/action: Hero / companion / family interfaces; Perform dismiss companion specifically in the conversation context
- Preconditions: Isolated conversation context for dismiss companion
- Expected: Acceptance requirement for conversation: dismissed hero leaves only the owning player's clan/party
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.004.case-02

Dismiss companion / clan screen

- Entry/action: Hero / companion / family interfaces; Perform dismiss companion specifically in the clan screen context
- Preconditions: Isolated clan screen context for dismiss companion
- Expected: Acceptance requirement for clan screen: dismissed hero leaves only the owning player's clan/party
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.005 Reassign companion

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.005.case-01

Reassign companion / party

- Entry/action: Hero / companion / family interfaces; Perform reassign companion specifically in the party context
- Preconditions: Isolated party context for reassign companion
- Expected: Acceptance requirement for party: old assignment is removed and new assignment becomes authoritative
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.005.case-02

Reassign companion / governor

- Entry/action: Hero / companion / family interfaces; Perform reassign companion specifically in the governor context
- Preconditions: Isolated governor context for reassign companion
- Expected: Acceptance requirement for governor: old assignment is removed and new assignment becomes authoritative
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.005.case-03

Reassign companion / formation captain

- Entry/action: Hero / companion / family interfaces; Perform reassign companion specifically in the formation captain context
- Preconditions: Isolated formation captain context for reassign companion
- Expected: Acceptance requirement for formation captain: old assignment is removed and new assignment becomes authoritative
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.006 Hero location

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.006.case-01

Hero location / party

- Entry/action: Hero / companion / family interfaces; Perform hero location specifically in the party context
- Preconditions: Isolated party context for hero location
- Expected: Acceptance requirement for party: hero appears in the correct campaign and mission location
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.006.case-02

Hero location / settlement

- Entry/action: Hero / companion / family interfaces; Perform hero location specifically in the settlement context
- Preconditions: Isolated settlement context for hero location
- Expected: Acceptance requirement for settlement: hero appears in the correct campaign and mission location
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.006.case-03

Hero location / location scene

- Entry/action: Hero / companion / family interfaces; Perform hero location specifically in the location scene context
- Preconditions: Isolated location scene context for hero location
- Expected: Acceptance requirement for location scene: hero appears in the correct campaign and mission location
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.007 Hero relationship

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.007.case-01

Hero relationship / positive action

- Entry/action: Hero / companion / family interfaces; Perform hero relationship specifically in the positive action context
- Preconditions: Isolated positive action context for hero relationship
- Expected: Acceptance requirement for positive action: relationship updates reach the intended player hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.007.case-02

Hero relationship / negative action

- Entry/action: Hero / companion / family interfaces; Perform hero relationship specifically in the negative action context
- Preconditions: Isolated negative action context for hero relationship
- Expected: Acceptance requirement for negative action: relationship updates reach the intended player hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.007.case-03

Hero relationship / owner-specific reward

- Entry/action: Hero / companion / family interfaces; Perform hero relationship specifically in the owner-specific reward context
- Preconditions: Isolated owner-specific reward context for hero relationship
- Expected: Acceptance requirement for owner-specific reward: relationship updates reach the intended player hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.008 Court hero

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.008.case-01

Court hero / dialogue stages

- Entry/action: Hero / companion / family interfaces; Perform court hero specifically in the dialogue stages context
- Preconditions: Isolated dialogue stages context for court hero
- Expected: Acceptance requirement for dialogue stages: romance progress belongs to the correct player and partner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.008.case-02.1

Court hero / persuasion success

- Entry/action: Hero / companion / family interfaces; Perform court hero specifically in the persuasion success context
- Preconditions: Isolated persuasion success context for court hero
- Expected: Acceptance requirement for persuasion success: romance progress belongs to the correct player and partner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.008.case-02.2

Court hero / persuasion failure

- Entry/action: Hero / companion / family interfaces; Perform court hero specifically in the persuasion failure context
- Preconditions: Isolated persuasion failure context for court hero
- Expected: Acceptance requirement for persuasion failure: romance progress belongs to the correct player and partner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.009 Marriage proposal

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.009.case-01

Marriage proposal / eligible

- Entry/action: Hero / companion / family interfaces; Perform marriage proposal specifically in the eligible context
- Preconditions: Isolated eligible context for marriage proposal
- Expected: Acceptance requirement for eligible: marriage and clan changes occur once after accepted terms
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.009.case-02

Marriage proposal / ineligible

- Entry/action: Hero / companion / family interfaces; Perform marriage proposal specifically in the ineligible context
- Preconditions: Isolated ineligible context for marriage proposal
- Expected: Acceptance requirement for ineligible: marriage and clan changes occur once after accepted terms
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.009.case-03

Marriage proposal / barter payment

- Entry/action: Hero / companion / family interfaces; Perform marriage proposal specifically in the barter payment context
- Preconditions: Isolated barter payment context for marriage proposal
- Expected: Acceptance requirement for barter payment: marriage and clan changes occur once after accepted terms
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.010 Marriage offer response

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.010.case-01

Marriage offer response / accept

- Entry/action: Hero / companion / family interfaces; Perform marriage offer response specifically in the accept context
- Preconditions: Isolated accept context for marriage offer response
- Expected: Acceptance requirement for accept: only the addressed player's response affects the offer
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.010.case-02

Marriage offer response / reject

- Entry/action: Hero / companion / family interfaces; Perform marriage offer response specifically in the reject context
- Preconditions: Isolated reject context for marriage offer response
- Expected: Acceptance requirement for reject: only the addressed player's response affects the offer
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.010.case-03

Marriage offer response / expired offer

- Entry/action: Hero / companion / family interfaces; Perform marriage offer response specifically in the expired offer context
- Preconditions: Isolated expired offer context for marriage offer response
- Expected: Acceptance requirement for expired offer: only the addressed player's response affects the offer
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.011 Pregnancy and childbirth

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.011.case-01

Pregnancy and childbirth / daily tick

- Entry/action: Hero / companion / family interfaces; Perform pregnancy and childbirth specifically in the daily tick context
- Preconditions: Isolated daily tick context for pregnancy and childbirth
- Expected: Acceptance requirement for daily tick: parentage and new hero identity converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.011.case-02

Pregnancy and childbirth / parent identity

- Entry/action: Hero / companion / family interfaces; Perform pregnancy and childbirth specifically in the parent identity context
- Preconditions: Isolated parent identity context for pregnancy and childbirth
- Expected: Acceptance requirement for parent identity: parentage and new hero identity converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.011.case-03

Pregnancy and childbirth / newborn registration

- Entry/action: Hero / companion / family interfaces; Perform pregnancy and childbirth specifically in the newborn registration context
- Preconditions: Isolated newborn registration context for pregnancy and childbirth
- Expected: Acceptance requirement for newborn registration: parentage and new hero identity converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.012 Child education

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.012.case-01

Child education / education stage

- Entry/action: Hero / companion / family interfaces; Perform child education specifically in the education stage context
- Preconditions: Isolated education stage context for child education
- Expected: Acceptance requirement for education stage: selected development affects the intended child
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.012.case-02

Child education / choice

- Entry/action: Hero / companion / family interfaces; Perform child education specifically in the choice context
- Preconditions: Isolated choice context for child education
- Expected: Acceptance requirement for choice: selected development affects the intended child
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.012.case-03

Child education / deferred choice

- Entry/action: Hero / companion / family interfaces; Perform child education specifically in the deferred choice context
- Preconditions: Isolated deferred choice context for child education
- Expected: Acceptance requirement for deferred choice: selected development affects the intended child
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.013 Hero aging

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.013.case-01

Hero aging / campaign time

- Entry/action: Hero / companion / family interfaces; Perform hero aging specifically in the campaign time context
- Preconditions: Isolated campaign time context for hero aging
- Expected: Acceptance requirement for campaign time: age-dependent state agrees after the authoritative time advance
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.013.case-02

Hero aging / age milestone

- Entry/action: Hero / companion / family interfaces; Perform hero aging specifically in the age milestone context
- Preconditions: Isolated age milestone context for hero aging
- Expected: Acceptance requirement for age milestone: age-dependent state agrees after the authoritative time advance
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.014 Hero death

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.014.case-01

Hero death / battle death

- Entry/action: Hero / companion / family interfaces; Perform hero death specifically in the battle death context
- Preconditions: Isolated battle death context for hero death
- Expected: Acceptance requirement for battle death: death state, relationships and succession side effects converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.014.case-02

Hero death / execution

- Entry/action: Hero / companion / family interfaces; Perform hero death specifically in the execution context
- Preconditions: Isolated execution context for hero death
- Expected: Acceptance requirement for execution: death state, relationships and succession side effects converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.014.case-03

Hero death / aging

- Entry/action: Hero / companion / family interfaces; Perform hero death specifically in the aging context
- Preconditions: Isolated aging context for hero death
- Expected: Acceptance requirement for aging: death state, relationships and succession side effects converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.015 Hero execution

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.015.case-01

Hero execution / permitted prisoner

- Entry/action: Hero / companion / family interfaces; Perform hero execution specifically in the permitted prisoner context
- Preconditions: Isolated permitted prisoner context for hero execution
- Expected: Acceptance requirement for permitted prisoner: accepted execution affects the selected prisoner once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.015.case-02

Hero execution / cancelled confirmation

- Entry/action: Hero / companion / family interfaces; Perform hero execution specifically in the cancelled confirmation context
- Preconditions: Isolated cancelled confirmation context for hero execution
- Expected: Acceptance requirement for cancelled confirmation: accepted execution affects the selected prisoner once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.016 Retirement and heir

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.016.case-01

Retirement and heir / retire

- Entry/action: Hero / companion / family interfaces; Perform retirement and heir specifically in the retire context
- Preconditions: Isolated retire context for retirement and heir
- Expected: Acceptance requirement for retire: controlled player identity transfers to the intended eligible heir
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.016.case-02

Retirement and heir / hero death

- Entry/action: Hero / companion / family interfaces; Perform retirement and heir specifically in the hero death context
- Preconditions: Isolated hero death context for retirement and heir
- Expected: Acceptance requirement for hero death: controlled player identity transfers to the intended eligible heir
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.016.case-03

Retirement and heir / heir selection

- Entry/action: Hero / companion / family interfaces; Perform retirement and heir specifically in the heir selection context
- Preconditions: Isolated heir selection context for retirement and heir
- Expected: Acceptance requirement for heir selection: controlled player identity transfers to the intended eligible heir
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

## heroes.017 Hero notifications

Each entry retains authority and separate owner/observer observations in the CSV.

### heroes.017.case-01

Hero notifications / skills

- Entry/action: Hero / companion / family interfaces; Perform hero notifications specifically in the skills context
- Preconditions: Isolated skills context for hero notifications
- Expected: Acceptance requirement for skills: notification refers to the affected hero and appears in the proper client context
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.017.case-02

Hero notifications / marriage

- Entry/action: Hero / companion / family interfaces; Perform hero notifications specifically in the marriage context
- Preconditions: Isolated marriage context for hero notifications
- Expected: Acceptance requirement for marriage: notification refers to the affected hero and appears in the proper client context
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.017.case-03

Hero notifications / death

- Entry/action: Hero / companion / family interfaces; Perform hero notifications specifically in the death context
- Preconditions: Isolated death context for hero notifications
- Expected: Acceptance requirement for death: notification refers to the affected hero and appears in the proper client context
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)

### heroes.017.case-04

Hero notifications / clan change

- Entry/action: Hero / companion / family interfaces; Perform hero notifications specifically in the clan change context
- Preconditions: Isolated clan change context for hero notifications
- Expected: Acceptance requirement for clan change: notification refers to the affected hero and appears in the proper client context
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Companions](../../source/GameInterface/Services/Companions), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions)
