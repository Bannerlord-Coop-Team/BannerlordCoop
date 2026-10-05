# prisoners behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## prisoners.001 Capture hero

Each entry retains authority and separate owner/observer observations in the CSV.

### prisoners.001.case-01

Capture hero / battle

- Entry/action: Party/dungeon screen / captivity / prison break; Perform capture hero specifically in the battle context
- Preconditions: Isolated battle context for capture hero
- Expected: Acceptance requirement for battle: hero prisoner state and captor roster agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.001.case-02

Capture hero / surrender

- Entry/action: Party/dungeon screen / captivity / prison break; Perform capture hero specifically in the surrender context
- Preconditions: Isolated surrender context for capture hero
- Expected: Acceptance requirement for surrender: hero prisoner state and captor roster agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.001.case-03

Capture hero / authoritative action

- Entry/action: Party/dungeon screen / captivity / prison break; Perform capture hero specifically in the authoritative action context
- Preconditions: Isolated authoritative action context for capture hero
- Expected: Acceptance requirement for authoritative action: hero prisoner state and captor roster agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

## prisoners.002 Release hero prisoner

Each entry retains authority and separate owner/observer observations in the CSV.

### prisoners.002.case-01

Release hero prisoner / free

- Entry/action: Party/dungeon screen / captivity / prison break; Perform release hero prisoner specifically in the free context
- Preconditions: Isolated free context for release hero prisoner
- Expected: Acceptance requirement for free: prisoner leaves the captor once and state/location update
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.002.case-02

Release hero prisoner / barter

- Entry/action: Party/dungeon screen / captivity / prison break; Perform release hero prisoner specifically in the barter context
- Preconditions: Isolated barter context for release hero prisoner
- Expected: Acceptance requirement for barter: prisoner leaves the captor once and state/location update
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.002.case-03

Release hero prisoner / ransom

- Entry/action: Party/dungeon screen / captivity / prison break; Perform release hero prisoner specifically in the ransom context
- Preconditions: Isolated ransom context for release hero prisoner
- Expected: Acceptance requirement for ransom: prisoner leaves the captor once and state/location update
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.002.case-04

Release hero prisoner / peace

- Entry/action: Party/dungeon screen / captivity / prison break; Perform release hero prisoner specifically in the peace context
- Preconditions: Isolated peace context for release hero prisoner
- Expected: Acceptance requirement for peace: prisoner leaves the captor once and state/location update
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

## prisoners.003 Offer ransom

Each entry retains authority and separate owner/observer observations in the CSV.

### prisoners.003.case-01

Offer ransom / new offer

- Entry/action: Party/dungeon screen / captivity / prison break; Perform offer ransom specifically in the new offer context
- Preconditions: Isolated new offer context for offer ransom
- Expected: Acceptance requirement for new offer: gold and release occur only for accepted valid terms
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.003.case-02

Offer ransom / accepted

- Entry/action: Party/dungeon screen / captivity / prison break; Perform offer ransom specifically in the accepted context
- Preconditions: Isolated accepted context for offer ransom
- Expected: Acceptance requirement for accepted: gold and release occur only for accepted valid terms
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.003.case-03

Offer ransom / rejected

- Entry/action: Party/dungeon screen / captivity / prison break; Perform offer ransom specifically in the rejected context
- Preconditions: Isolated rejected context for offer ransom
- Expected: Acceptance requirement for rejected: gold and release occur only for accepted valid terms
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.003.case-04

Offer ransom / expired

- Entry/action: Party/dungeon screen / captivity / prison break; Perform offer ransom specifically in the expired context
- Preconditions: Isolated expired context for offer ransom
- Expected: Acceptance requirement for expired: gold and release occur only for accepted valid terms
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

## prisoners.004 Hero escapes captivity

Each entry retains authority and separate owner/observer observations in the CSV.

### prisoners.004.case-01

Hero escapes captivity / party prisoner

- Entry/action: Party/dungeon screen / captivity / prison break; Perform hero escapes captivity specifically in the party prisoner context
- Preconditions: Isolated party prisoner context for hero escapes captivity
- Expected: Acceptance requirement for party prisoner: released hero and subsequent party/location state converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.004.case-02

Hero escapes captivity / settlement prisoner

- Entry/action: Party/dungeon screen / captivity / prison break; Perform hero escapes captivity specifically in the settlement prisoner context
- Preconditions: Isolated settlement prisoner context for hero escapes captivity
- Expected: Acceptance requirement for settlement prisoner: released hero and subsequent party/location state converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.004.case-03

Hero escapes captivity / AI tick

- Entry/action: Party/dungeon screen / captivity / prison break; Perform hero escapes captivity specifically in the AI tick context
- Preconditions: Isolated AI tick context for hero escapes captivity
- Expected: Acceptance requirement for AI tick: released hero and subsequent party/location state converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

## prisoners.005 Player captured

Each entry retains authority and separate owner/observer observations in the CSV.

### prisoners.005.case-01

Player captured / battle defeat

- Entry/action: Party/dungeon screen / captivity / prison break; Perform player captured specifically in the battle defeat context
- Preconditions: Isolated battle defeat context for player captured
- Expected: Acceptance requirement for battle defeat: the intended player enters captivity without losing ownership mapping
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.005.case-02

Player captured / surrender

- Entry/action: Party/dungeon screen / captivity / prison break; Perform player captured specifically in the surrender context
- Preconditions: Isolated surrender context for player captured
- Expected: Acceptance requirement for surrender: the intended player enters captivity without losing ownership mapping
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

## prisoners.006 Player captivity progress

Each entry retains authority and separate owner/observer observations in the CSV.

### prisoners.006.case-01

Player captivity progress / wait

- Entry/action: Party/dungeon screen / captivity / prison break; Perform player captivity progress specifically in the wait context
- Preconditions: Isolated wait context for player captivity progress
- Expected: Acceptance requirement for wait: captivity UI and authoritative state remain consistent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.006.case-02

Player captivity progress / transfer captor

- Entry/action: Party/dungeon screen / captivity / prison break; Perform player captivity progress specifically in the transfer captor context
- Preconditions: Isolated transfer captor context for player captivity progress
- Expected: Acceptance requirement for transfer captor: captivity UI and authoritative state remain consistent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.006.case-03

Player captivity progress / escape opportunity

- Entry/action: Party/dungeon screen / captivity / prison break; Perform player captivity progress specifically in the escape opportunity context
- Preconditions: Isolated escape opportunity context for player captivity progress
- Expected: Acceptance requirement for escape opportunity: captivity UI and authoritative state remain consistent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

## prisoners.007 Player released from captivity

Each entry retains authority and separate owner/observer observations in the CSV.

### prisoners.007.case-01

Player released from captivity / ransom

- Entry/action: Party/dungeon screen / captivity / prison break; Perform player released from captivity specifically in the ransom context
- Preconditions: Isolated ransom context for player released from captivity
- Expected: Acceptance requirement for ransom: controlled party recovery produces one registered valid party
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.007.case-02

Player released from captivity / escape

- Entry/action: Party/dungeon screen / captivity / prison break; Perform player released from captivity specifically in the escape context
- Preconditions: Isolated escape context for player released from captivity
- Expected: Acceptance requirement for escape: controlled party recovery produces one registered valid party
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.007.case-03

Player released from captivity / captor defeated

- Entry/action: Party/dungeon screen / captivity / prison break; Perform player released from captivity specifically in the captor defeated context
- Preconditions: Isolated captor defeated context for player released from captivity
- Expected: Acceptance requirement for captor defeated: controlled party recovery produces one registered valid party
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

## prisoners.008 Prison break

Each entry retains authority and separate owner/observer observations in the CSV.

### prisoners.008.case-01

Prison break / start

- Entry/action: Party/dungeon screen / captivity / prison break; Perform prison break specifically in the start context
- Preconditions: Isolated start context for prison break
- Expected: Acceptance requirement for start: prisoners and mission/campaign consequences reflect the real outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.008.case-02

Prison break / guards

- Entry/action: Party/dungeon screen / captivity / prison break; Perform prison break specifically in the guards context
- Preconditions: Isolated guards context for prison break
- Expected: Acceptance requirement for guards: prisoners and mission/campaign consequences reflect the real outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.008.case-03

Prison break / rescue

- Entry/action: Party/dungeon screen / captivity / prison break; Perform prison break specifically in the rescue context
- Preconditions: Isolated rescue context for prison break
- Expected: Acceptance requirement for rescue: prisoners and mission/campaign consequences reflect the real outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.008.case-04

Prison break / success

- Entry/action: Party/dungeon screen / captivity / prison break; Perform prison break specifically in the success context
- Preconditions: Isolated success context for prison break
- Expected: Acceptance requirement for success: prisoners and mission/campaign consequences reflect the real outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.008.case-05

Prison break / failure

- Entry/action: Party/dungeon screen / captivity / prison break; Perform prison break specifically in the failure context
- Preconditions: Isolated failure context for prison break
- Expected: Acceptance requirement for failure: prisoners and mission/campaign consequences reflect the real outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

## prisoners.009 Prisoner conformity

Each entry retains authority and separate owner/observer observations in the CSV.

### prisoners.009.case-01

Prisoner conformity / daily tick

- Entry/action: Party/dungeon screen / captivity / prison break; Perform prisoner conformity specifically in the daily tick context
- Preconditions: Isolated daily tick context for prisoner conformity
- Expected: Acceptance requirement for daily tick: conformity and recruitable count agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.009.case-02

Prisoner conformity / troop type

- Entry/action: Party/dungeon screen / captivity / prison break; Perform prisoner conformity specifically in the troop type context
- Preconditions: Isolated troop type context for prisoner conformity
- Expected: Acceptance requirement for troop type: conformity and recruitable count agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.009.case-03

Prisoner conformity / recruit eligibility

- Entry/action: Party/dungeon screen / captivity / prison break; Perform prisoner conformity specifically in the recruit eligibility context
- Preconditions: Isolated recruit eligibility context for prisoner conformity
- Expected: Acceptance requirement for recruit eligibility: conformity and recruitable count agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

## prisoners.010 Prisoner limit

Each entry retains authority and separate owner/observer observations in the CSV.

### prisoners.010.case-01

Prisoner limit / within cap

- Entry/action: Party/dungeon screen / captivity / prison break; Perform prisoner limit specifically in the within cap context
- Preconditions: Isolated within cap context for prisoner limit
- Expected: Acceptance requirement for within cap: UI and resulting prisoner losses follow the current model
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.010.case-02

Prisoner limit / exceeded

- Entry/action: Party/dungeon screen / captivity / prison break; Perform prisoner limit specifically in the exceeded context
- Preconditions: Isolated exceeded context for prisoner limit
- Expected: Acceptance requirement for exceeded: UI and resulting prisoner losses follow the current model
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)

### prisoners.010.case-03

Prisoner limit / escape

- Entry/action: Party/dungeon screen / captivity / prison break; Perform prisoner limit specifically in the escape context
- Preconditions: Isolated escape context for prisoner limit
- Expected: Acceptance requirement for escape: UI and resulting prisoner losses follow the current model
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/PlayerCaptivityService](../../source/GameInterface/Services/PlayerCaptivityService), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Actions](../../source/GameInterface/Services/Actions), [source/GameInterface/Services/Party](../../source/GameInterface/Services/Party)
