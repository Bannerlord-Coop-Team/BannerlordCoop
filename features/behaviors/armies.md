# armies behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## armies.001 Create army

Each entry retains authority and separate owner/observer observations in the CSV.

### armies.001.case-01

Create army / leader eligible

- Entry/action: Campaign army UI / map; Perform create army specifically in the leader eligible context
- Preconditions: Isolated leader eligible context for create army
- Expected: Acceptance requirement for leader eligible: one registered army owns the intended leader and invited parties
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.001.case-02

Create army / influence

- Entry/action: Campaign army UI / map; Perform create army specifically in the influence context
- Preconditions: Isolated influence context for create army
- Expected: Acceptance requirement for influence: one registered army owns the intended leader and invited parties
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.001.case-03

Create army / available parties

- Entry/action: Campaign army UI / map; Perform create army specifically in the available parties context
- Preconditions: Isolated available parties context for create army
- Expected: Acceptance requirement for available parties: one registered army owns the intended leader and invited parties
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

## armies.002 Invite party to army

Each entry retains authority and separate owner/observer observations in the CSV.

### armies.002.case-01

Invite party to army / eligible

- Entry/action: Campaign army UI / map; Perform invite party to army specifically in the eligible context
- Preconditions: Isolated eligible context for invite party to army
- Expected: Acceptance requirement for eligible: accepted party joins the intended army once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.002.case-02

Invite party to army / already attached

- Entry/action: Campaign army UI / map; Perform invite party to army specifically in the already attached context
- Preconditions: Isolated already attached context for invite party to army
- Expected: Acceptance requirement for already attached: accepted party joins the intended army once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.002.case-03

Invite party to army / other player

- Entry/action: Campaign army UI / map; Perform invite party to army specifically in the other player context
- Preconditions: Isolated other player context for invite party to army
- Expected: Acceptance requirement for other player: accepted party joins the intended army once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

## armies.003 Join army

Each entry retains authority and separate owner/observer observations in the CSV.

### armies.003.case-01

Join army / client request

- Entry/action: Campaign army UI / map; Perform join army specifically in the client request context
- Preconditions: Isolated client request context for join army
- Expected: Acceptance requirement for client request: party attachment and army membership agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.003.case-02

Join army / automatic AI arrival

- Entry/action: Campaign army UI / map; Perform join army specifically in the automatic AI arrival context
- Preconditions: Isolated automatic AI arrival context for join army
- Expected: Acceptance requirement for automatic AI arrival: party attachment and army membership agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

## armies.004 Leave army

Each entry retains authority and separate owner/observer observations in the CSV.

### armies.004.case-01

Leave army / voluntary

- Entry/action: Campaign army UI / map; Perform leave army specifically in the voluntary context
- Preconditions: Isolated voluntary context for leave army
- Expected: Acceptance requirement for voluntary: party detaches from the intended army and can move independently
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.004.case-02

Leave army / battle

- Entry/action: Campaign army UI / map; Perform leave army specifically in the battle context
- Preconditions: Isolated battle context for leave army
- Expected: Acceptance requirement for battle: party detaches from the intended army and can move independently
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.004.case-03

Leave army / disband

- Entry/action: Campaign army UI / map; Perform leave army specifically in the disband context
- Preconditions: Isolated disband context for leave army
- Expected: Acceptance requirement for disband: party detaches from the intended army and can move independently
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

## armies.005 Army cohesion

Each entry retains authority and separate owner/observer observations in the CSV.

### armies.005.case-01

Army cohesion / daily decay

- Entry/action: Campaign army UI / map; Perform army cohesion specifically in the daily decay context
- Preconditions: Isolated daily decay context for army cohesion
- Expected: Acceptance requirement for daily decay: cohesion/influence change once on the authoritative side
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.005.case-02

Army cohesion / spend influence

- Entry/action: Campaign army UI / map; Perform army cohesion specifically in the spend influence context
- Preconditions: Isolated spend influence context for army cohesion
- Expected: Acceptance requirement for spend influence: cohesion/influence change once on the authoritative side
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.005.case-03

Army cohesion / leader change

- Entry/action: Campaign army UI / map; Perform army cohesion specifically in the leader change context
- Preconditions: Isolated leader change context for army cohesion
- Expected: Acceptance requirement for leader change: cohesion/influence change once on the authoritative side
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

## armies.006 Army supplies

Each entry retains authority and separate owner/observer observations in the CSV.

### armies.006.case-01

Army supplies / food sharing

- Entry/action: Campaign army UI / map; Perform army supplies specifically in the food sharing context
- Preconditions: Isolated food sharing context for army supplies
- Expected: Acceptance requirement for food sharing: food and shortage effects agree for participating parties
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.006.case-02

Army supplies / shortages

- Entry/action: Campaign army UI / map; Perform army supplies specifically in the shortages context
- Preconditions: Isolated shortages context for army supplies
- Expected: Acceptance requirement for shortages: food and shortage effects agree for participating parties
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.006.case-03

Army supplies / party loss

- Entry/action: Campaign army UI / map; Perform army supplies specifically in the party loss context
- Preconditions: Isolated party loss context for army supplies
- Expected: Acceptance requirement for party loss: food and shortage effects agree for participating parties
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

## armies.007 Army objective

Each entry retains authority and separate owner/observer observations in the CSV.

### armies.007.case-01

Army objective / attack

- Entry/action: Campaign army UI / map; Perform army objective specifically in the attack context
- Preconditions: Isolated attack context for army objective
- Expected: Acceptance requirement for attack: member movement follows the current authoritative army order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.007.case-02

Army objective / defend

- Entry/action: Campaign army UI / map; Perform army objective specifically in the defend context
- Preconditions: Isolated defend context for army objective
- Expected: Acceptance requirement for defend: member movement follows the current authoritative army order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.007.case-03

Army objective / besiege

- Entry/action: Campaign army UI / map; Perform army objective specifically in the besiege context
- Preconditions: Isolated besiege context for army objective
- Expected: Acceptance requirement for besiege: member movement follows the current authoritative army order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.007.case-04

Army objective / patrol

- Entry/action: Campaign army UI / map; Perform army objective specifically in the patrol context
- Preconditions: Isolated patrol context for army objective
- Expected: Acceptance requirement for patrol: member movement follows the current authoritative army order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

## armies.008 Army encounter

Each entry retains authority and separate owner/observer observations in the CSV.

### armies.008.case-01

Army encounter / field battle

- Entry/action: Campaign army UI / map; Perform army encounter specifically in the field battle context
- Preconditions: Isolated field battle context for army encounter
- Expected: Acceptance requirement for field battle: all participating parties share the correct map event sides
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.008.case-02

Army encounter / siege defense

- Entry/action: Campaign army UI / map; Perform army encounter specifically in the siege defense context
- Preconditions: Isolated siege defense context for army encounter
- Expected: Acceptance requirement for siege defense: all participating parties share the correct map event sides
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.008.case-03

Army encounter / relief

- Entry/action: Campaign army UI / map; Perform army encounter specifically in the relief context
- Preconditions: Isolated relief context for army encounter
- Expected: Acceptance requirement for relief: all participating parties share the correct map event sides
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

## armies.009 Disband army

Each entry retains authority and separate owner/observer observations in the CSV.

### armies.009.case-01

Disband army / leader action

- Entry/action: Campaign army UI / map; Perform disband army specifically in the leader action context
- Preconditions: Isolated leader action context for disband army
- Expected: Acceptance requirement for leader action: all members detach without retaining a dead army reference
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.009.case-02

Disband army / cohesion loss

- Entry/action: Campaign army UI / map; Perform disband army specifically in the cohesion loss context
- Preconditions: Isolated cohesion loss context for disband army
- Expected: Acceptance requirement for cohesion loss: all members detach without retaining a dead army reference
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.009.case-03

Disband army / leader removed

- Entry/action: Campaign army UI / map; Perform disband army specifically in the leader removed context
- Preconditions: Isolated leader removed context for disband army
- Expected: Acceptance requirement for leader removed: all members detach without retaining a dead army reference
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

## armies.010 Army leader replacement

Each entry retains authority and separate owner/observer observations in the CSV.

### armies.010.case-01

Army leader replacement / leader removed

- Entry/action: Campaign army UI / map; Perform army leader replacement specifically in the leader removed context
- Preconditions: Isolated leader removed context for army leader replacement
- Expected: Acceptance requirement for leader removed: new leader and resulting orders agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)

### armies.010.case-02

Army leader replacement / eligible survivor

- Entry/action: Campaign army UI / map; Perform army leader replacement specifically in the eligible survivor context
- Preconditions: Isolated eligible survivor context for army leader replacement
- Expected: Acceptance requirement for eligible survivor: new leader and resulting orders agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Armies](../../source/GameInterface/Services/Armies), [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents)
