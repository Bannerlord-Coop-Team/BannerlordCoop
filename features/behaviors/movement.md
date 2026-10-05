# movement behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## movement.001 Move to map position

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.001.case-01

Move to map position / short travel

- Entry/action: Client campaign map; Perform move to map position specifically in the short travel context
- Preconditions: Isolated short travel context for move to map position
- Expected: Acceptance requirement for short travel: authoritative position reaches the selected reachable destination
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.001.case-02

Move to map position / long travel

- Entry/action: Client campaign map; Perform move to map position specifically in the long travel context
- Preconditions: Isolated long travel context for move to map position
- Expected: Acceptance requirement for long travel: authoritative position reaches the selected reachable destination
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.001.case-03

Move to map position / unreachable destination

- Entry/action: Client campaign map; Perform move to map position specifically in the unreachable destination context
- Preconditions: Isolated unreachable destination context for move to map position
- Expected: Acceptance requirement for unreachable destination: authoritative position reaches the selected reachable destination
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

## movement.002 Move to settlement

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.002.case-01

Move to settlement / town

- Entry/action: Client campaign map; Perform move to settlement specifically in the town context
- Preconditions: Isolated town context for move to settlement
- Expected: Acceptance requirement for town: party reaches the correct settlement and entry state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.002.case-02

Move to settlement / castle

- Entry/action: Client campaign map; Perform move to settlement specifically in the castle context
- Preconditions: Isolated castle context for move to settlement
- Expected: Acceptance requirement for castle: party reaches the correct settlement and entry state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.002.case-03

Move to settlement / village

- Entry/action: Client campaign map; Perform move to settlement specifically in the village context
- Preconditions: Isolated village context for move to settlement
- Expected: Acceptance requirement for village: party reaches the correct settlement and entry state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.002.case-04

Move to settlement / hostile settlement

- Entry/action: Client campaign map; Perform move to settlement specifically in the hostile settlement context
- Preconditions: Isolated hostile settlement context for move to settlement
- Expected: Acceptance requirement for hostile settlement: party reaches the correct settlement and entry state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

## movement.003 Follow another party

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.003.case-01

Follow another party / own companion

- Entry/action: Client campaign map; Perform follow another party specifically in the own companion context
- Preconditions: Isolated own companion context for follow another party
- Expected: Acceptance requirement for own companion: follow target identity remains stable as the target moves
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.003.case-02

Follow another party / allied party

- Entry/action: Client campaign map; Perform follow another party specifically in the allied party context
- Preconditions: Isolated allied party context for follow another party
- Expected: Acceptance requirement for allied party: follow target identity remains stable as the target moves
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.003.case-03

Follow another party / another player

- Entry/action: Client campaign map; Perform follow another party specifically in the another player context
- Preconditions: Isolated another player context for follow another party
- Expected: Acceptance requirement for another player: follow target identity remains stable as the target moves
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

## movement.004 Escort party

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.004.case-01

Escort party / escort begins

- Entry/action: Client campaign map; Perform escort party specifically in the escort begins context
- Preconditions: Isolated escort begins context for escort party
- Expected: Acceptance requirement for escort begins: escort behavior and resulting movement agree on all peers
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.004.case-02

Escort party / target changes

- Entry/action: Client campaign map; Perform escort party specifically in the target changes context
- Preconditions: Isolated target changes context for escort party
- Expected: Acceptance requirement for target changes: escort behavior and resulting movement agree on all peers
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.004.case-03

Escort party / escort ends

- Entry/action: Client campaign map; Perform escort party specifically in the escort ends context
- Preconditions: Isolated escort ends context for escort party
- Expected: Acceptance requirement for escort ends: escort behavior and resulting movement agree on all peers
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

## movement.005 Patrol map region

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.005.case-01

Patrol map region / patrol point

- Entry/action: Client campaign map; Perform patrol map region specifically in the patrol point context
- Preconditions: Isolated patrol point context for patrol map region
- Expected: Acceptance requirement for patrol point: party changes movement according to the authoritative patrol state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.005.case-02

Patrol map region / repeated AI tick

- Entry/action: Client campaign map; Perform patrol map region specifically in the repeated AI tick context
- Preconditions: Isolated repeated AI tick context for patrol map region
- Expected: Acceptance requirement for repeated AI tick: party changes movement according to the authoritative patrol state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

## movement.006 Flee another party

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.006.case-01

Flee another party / enemy approach

- Entry/action: Client campaign map; Perform flee another party specifically in the enemy approach context
- Preconditions: Isolated enemy approach context for flee another party
- Expected: Acceptance requirement for enemy approach: escape target and movement state converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.006.case-02

Flee another party / danger passes

- Entry/action: Client campaign map; Perform flee another party specifically in the danger passes context
- Preconditions: Isolated danger passes context for flee another party
- Expected: Acceptance requirement for danger passes: escape target and movement state converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

## movement.007 Wait on campaign map

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.007.case-01

Wait on campaign map / stationary

- Entry/action: Client campaign map; Perform wait on campaign map specifically in the stationary context
- Preconditions: Isolated stationary context for wait on campaign map
- Expected: Acceptance requirement for stationary: time passes under server policy and encounters interrupt correctly
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.007.case-02

Wait on campaign map / enemy encounter while waiting

- Entry/action: Client campaign map; Perform wait on campaign map specifically in the enemy encounter while waiting context
- Preconditions: Isolated enemy encounter while waiting context for wait on campaign map
- Expected: Acceptance requirement for enemy encounter while waiting: time passes under server policy and encounters interrupt correctly
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

## movement.008 Stop movement

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.008.case-01

Stop movement / manual stop

- Entry/action: Client campaign map; Perform stop movement specifically in the manual stop context
- Preconditions: Isolated manual stop context for stop movement
- Expected: Acceptance requirement for manual stop: party stops without a stale movement order being reapplied
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.008.case-02

Stop movement / target removed

- Entry/action: Client campaign map; Perform stop movement specifically in the target removed context
- Preconditions: Isolated target removed context for stop movement
- Expected: Acceptance requirement for target removed: party stops without a stale movement order being reapplied
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

## movement.009 Encounter moving party

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.009.case-01

Encounter moving party / friendly

- Entry/action: Client campaign map; Perform encounter moving party specifically in the friendly context
- Preconditions: Isolated friendly context for encounter moving party
- Expected: Acceptance requirement for friendly: all peers agree on encounter participants and action availability
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.009.case-02

Encounter moving party / neutral

- Entry/action: Client campaign map; Perform encounter moving party specifically in the neutral context
- Preconditions: Isolated neutral context for encounter moving party
- Expected: Acceptance requirement for neutral: all peers agree on encounter participants and action availability
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.009.case-03

Encounter moving party / enemy

- Entry/action: Client campaign map; Perform encounter moving party specifically in the enemy context
- Preconditions: Isolated enemy context for encounter moving party
- Expected: Acceptance requirement for enemy: all peers agree on encounter participants and action availability
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.009.case-04

Encounter moving party / player party

- Entry/action: Client campaign map; Perform encounter moving party specifically in the player party context
- Preconditions: Isolated player party context for encounter moving party
- Expected: Acceptance requirement for player party: all peers agree on encounter participants and action availability
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### menu.encounter.taken_prisoner.taken_prisoner_continue.select

Select taken_prisoner.taken_prisoner_continue

- Entry/action: Client campaign menu taken_prisoner; Choose the exact registered option taken_prisoner_continue
- Preconditions: The real menu taken_prisoner is reached; option taken_prisoner_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_taken_prisoner_continue_on_condition; game_menu_taken_prisoner_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@111-111
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.defeated_and_taken_prisoner.taken_prisoner_continue.select

Select defeated_and_taken_prisoner.taken_prisoner_continue

- Entry/action: Client campaign menu defeated_and_taken_prisoner; Choose the exact registered option taken_prisoner_continue
- Preconditions: The real menu defeated_and_taken_prisoner is reached; option taken_prisoner_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_taken_prisoner_continue_on_condition; game_menu_taken_prisoner_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@113-113
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.join_encounter.join_encounter_help_attackers.select

Select join_encounter.join_encounter_help_attackers

- Entry/action: Client campaign menu join_encounter; Choose the exact registered option join_encounter_help_attackers
- Preconditions: The real menu join_encounter is reached; option join_encounter_help_attackers condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_join_encounter_help_attackers_on_condition; game_menu_join_encounter_help_attackers_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@116-116
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.join_encounter.join_encounter_help_defenders.select

Select join_encounter.join_encounter_help_defenders

- Entry/action: Client campaign menu join_encounter; Choose the exact registered option join_encounter_help_defenders
- Preconditions: The real menu join_encounter is reached; option join_encounter_help_defenders condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_join_encounter_help_defenders_on_condition; game_menu_join_encounter_help_defenders_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@117-117
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.join_encounter.join_encounter_abandon.select

Select join_encounter.join_encounter_abandon

- Entry/action: Client campaign menu join_encounter; Choose the exact registered option join_encounter_abandon
- Preconditions: The real menu join_encounter is reached; option join_encounter_abandon condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_join_encounter_abandon_army_on_condition; game_menu_encounter_abandon_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@118-118
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.join_sally_out.join_siege_event.select

Select join_sally_out.join_siege_event

- Entry/action: Client campaign menu join_sally_out; Choose the exact registered option join_siege_event
- Preconditions: The real menu join_sally_out is reached; option join_siege_event condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_join_sally_out_event_on_condition; game_menu_join_sally_out_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@129-129
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.join_sally_out.join_siege_event_break_in.select

Select join_sally_out.join_siege_event_break_in

- Entry/action: Client campaign menu join_sally_out; Choose the exact registered option join_siege_event_break_in
- Preconditions: The real menu join_sally_out is reached; option join_siege_event_break_in condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_stay_in_settlement_on_condition; game_menu_stay_in_settlement_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@130-130
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.menu_siege_strategies.menu_siege_strategies_break_out_from_gate.select

Select menu_siege_strategies.menu_siege_strategies_break_out_from_gate

- Entry/action: Client campaign menu menu_siege_strategies; Choose the exact registered option menu_siege_strategies_break_out_from_gate
- Preconditions: The real menu menu_siege_strategies is reached; option menu_siege_strategies_break_out_from_gate condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks menu_defender_siege_break_out_from_gate_on_condition; menu_defender_siege_break_out_from_gate_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@148-148
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.menu_siege_strategies.menu_siege_strategies_sally_out_from_gate.select

Select menu_siege_strategies.menu_siege_strategies_sally_out_from_gate

- Entry/action: Client campaign menu menu_siege_strategies; Choose the exact registered option menu_siege_strategies_sally_out_from_gate
- Preconditions: The real menu menu_siege_strategies is reached; option menu_siege_strategies_sally_out_from_gate condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks menu_sally_out_from_gate_on_condition; menu_sally_out_land_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@150-150
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.join_siege_event.join_siege_event.select

Select join_siege_event.join_siege_event

- Entry/action: Client campaign menu join_siege_event; Choose the exact registered option join_siege_event
- Preconditions: The real menu join_siege_event is reached; option join_siege_event condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_join_siege_event_on_condition; game_menu_join_siege_event_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@153-153
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.join_siege_event.attack_besiegers.select

Select join_siege_event.attack_besiegers

- Entry/action: Client campaign menu join_siege_event; Choose the exact registered option attack_besiegers
- Preconditions: The real menu join_siege_event is reached; option attack_besiegers condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks attack_besieger_side_on_condition; game_menu_join_encounter_help_defenders_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@154-154
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.join_siege_event.join_siege_event_break_in.select

Select join_siege_event.join_siege_event_break_in

- Entry/action: Client campaign menu join_siege_event; Choose the exact registered option join_siege_event_break_in
- Preconditions: The real menu join_siege_event is reached; option join_siege_event_break_in condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks break_in_to_help_defender_side_on_condition; game_menu_join_siege_event_on_defender_side_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@155-155
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.join_siege_event.join_encounter_leave.select

Select join_siege_event.join_encounter_leave

- Entry/action: Client campaign menu join_siege_event; Choose the exact registered option join_encounter_leave
- Preconditions: The real menu join_siege_event is reached; option join_encounter_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_join_encounter_leave_on_condition; break_in_leave_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@156-156
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.siege_attacker_left.siege_attacker_left_return_to_settlement.select

Select siege_attacker_left.siege_attacker_left_return_to_settlement

- Entry/action: Client campaign menu siege_attacker_left; Choose the exact registered option siege_attacker_left_return_to_settlement
- Preconditions: The real menu siege_attacker_left is reached; option siege_attacker_left_return_to_settlement condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_siege_attacker_left_return_to_settlement_on_condition; game_menu_siege_attacker_left_return_to_settlement_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@158-158
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.siege_attacker_defeated.siege_attacker_defeated_return_to_settlement.select

Select siege_attacker_defeated.siege_attacker_defeated_return_to_settlement

- Entry/action: Client campaign menu siege_attacker_defeated; Choose the exact registered option siege_attacker_defeated_return_to_settlement
- Preconditions: The real menu siege_attacker_defeated is reached; option siege_attacker_defeated_return_to_settlement condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_siege_attacker_left_return_to_settlement_on_condition; game_menu_siege_attacker_left_return_to_settlement_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@164-164
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter.continue_preparations.select

Select encounter.continue_preparations

- Entry/action: Client campaign menu encounter; Choose the exact registered option continue_preparations
- Preconditions: The real menu encounter is reached; option continue_preparations condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_besiege_continue_siege_on_condition; game_menu_town_besiege_continue_siege_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@170-170
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter.village_raid_action.select

Select encounter.village_raid_action

- Entry/action: Client campaign menu encounter; Choose the exact registered option village_raid_action
- Preconditions: The real menu encounter is reached; option village_raid_action condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_hostile_action_on_condition; game_menu_village_raid_no_resist_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@171-171
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter.village_force_volunteer_action.select

Select encounter.village_force_volunteer_action

- Entry/action: Client campaign menu encounter; Choose the exact registered option village_force_volunteer_action
- Preconditions: The real menu encounter is reached; option village_force_volunteer_action condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_hostile_action_on_condition; game_menu_village_force_volunteers_no_resist_loot_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@172-172
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter.village_force_supplies_action.select

Select encounter.village_force_supplies_action

- Entry/action: Client campaign menu encounter; Choose the exact registered option village_force_supplies_action
- Preconditions: The real menu encounter is reached; option village_force_supplies_action condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_hostile_action_on_condition; game_menu_village_force_supplies_no_resist_loot_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@173-173
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter.attack.select

Select encounter.attack

- Entry/action: Client campaign menu encounter; Choose the exact registered option attack
- Preconditions: The real menu encounter is reached; option attack condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_encounter_attack_on_condition; game_menu_encounter_attack_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@174-174
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter.capture_the_enemy.select

Select encounter.capture_the_enemy

- Entry/action: Client campaign menu encounter; Choose the exact registered option capture_the_enemy
- Preconditions: The real menu encounter is reached; option capture_the_enemy condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_encounter_capture_the_enemy_on_condition; game_menu_capture_the_enemy_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@175-175
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter.str_order_attack.select

Select encounter.str_order_attack

- Entry/action: Client campaign menu encounter; Choose the exact registered option str_order_attack
- Preconditions: The real menu encounter is reached; option str_order_attack condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_encounter_order_attack_on_condition; game_menu_encounter_order_attack_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@176-176
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter.surrender.select

Select encounter.surrender

- Entry/action: Client campaign menu encounter; Choose the exact registered option surrender
- Preconditions: The real menu encounter is reached; option surrender condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_encounter_surrender_on_condition; game_menu_encounter_surrender_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@181-181
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter.leave.select

Select encounter.leave

- Entry/action: Client campaign menu encounter; Choose the exact registered option leave
- Preconditions: The real menu encounter is reached; option leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_encounter_leave_on_condition; game_menu_encounter_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@182-182
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter.abandon_army.select

Select encounter.abandon_army

- Entry/action: Client campaign menu encounter; Choose the exact registered option abandon_army
- Preconditions: The real menu encounter is reached; option abandon_army condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_encounter_abandon_army_on_condition; game_menu_encounter_abandon_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@183-183
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter.go_back_to_settlement.select

Select encounter.go_back_to_settlement

- Entry/action: Client campaign menu encounter; Choose the exact registered option go_back_to_settlement
- Preconditions: The real menu encounter is reached; option go_back_to_settlement condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_sally_out_go_back_to_settlement_on_condition; game_menu_sally_out_go_back_to_settlement_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@184-184
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.army_encounter.army_talk_to_leader.select

Select army_encounter.army_talk_to_leader

- Entry/action: Client campaign menu army_encounter; Choose the exact registered option army_talk_to_leader
- Preconditions: The real menu army_encounter is reached; option army_talk_to_leader condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_army_talk_to_leader_on_condition; game_menu_army_talk_to_leader_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@186-186
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.army_encounter.army_talk_to_other_members.select

Select army_encounter.army_talk_to_other_members

- Entry/action: Client campaign menu army_encounter; Choose the exact registered option army_talk_to_other_members
- Preconditions: The real menu army_encounter is reached; option army_talk_to_other_members condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_army_talk_to_other_members_on_condition; game_menu_army_talk_to_other_members_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@187-187
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.army_encounter.army_join_army.select

Select army_encounter.army_join_army

- Entry/action: Client campaign menu army_encounter; Choose the exact registered option army_join_army
- Preconditions: The real menu army_encounter is reached; option army_join_army condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_army_join_on_condition; game_menu_army_join_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@188-188
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.army_encounter.army_attack_army.select

Select army_encounter.army_attack_army

- Entry/action: Client campaign menu army_encounter; Choose the exact registered option army_attack_army
- Preconditions: The real menu army_encounter is reached; option army_attack_army condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_army_attack_on_condition; game_menu_army_attack_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@189-189
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.army_encounter.army_leave.select

Select army_encounter.army_leave

- Entry/action: Client campaign menu army_encounter; Choose the exact registered option army_leave
- Preconditions: The real menu army_encounter is reached; option army_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_army_leave_on_condition; army_encounter_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@190-190
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.game_menu_army_talk_to_other_members.game_menu_army_talk_to_other_members_item.select

Select game_menu_army_talk_to_other_members.game_menu_army_talk_to_other_members_item

- Entry/action: Client campaign menu game_menu_army_talk_to_other_members; Choose the exact registered option game_menu_army_talk_to_other_members_item
- Preconditions: The real menu game_menu_army_talk_to_other_members is reached; option game_menu_army_talk_to_other_members_item condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_army_talk_to_other_members_item_on_condition; game_menu_army_talk_to_other_members_item_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@192-192
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.game_menu_army_talk_to_other_members.game_menu_army_talk_to_other_members_back.select

Select game_menu_army_talk_to_other_members.game_menu_army_talk_to_other_members_back

- Entry/action: Client campaign menu game_menu_army_talk_to_other_members; Choose the exact registered option game_menu_army_talk_to_other_members_back
- Preconditions: The real menu game_menu_army_talk_to_other_members is reached; option game_menu_army_talk_to_other_members_back condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_army_talk_to_other_members_back_on_condition; game_menu_army_talk_to_other_members_back_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@193-193
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.try_to_get_away.try_to_get_away_accept.select

Select try_to_get_away.try_to_get_away_accept

- Entry/action: Client campaign menu try_to_get_away; Choose the exact registered option try_to_get_away_accept
- Preconditions: The real menu try_to_get_away is reached; option try_to_get_away_accept condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_try_to_get_away_accept_on_condition; game_menu_encounter_leave_your_soldiers_behind_accept_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@195-195
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.try_to_get_away_debrief.try_to_get_away_continue.select

Select try_to_get_away_debrief.try_to_get_away_continue

- Entry/action: Client campaign menu try_to_get_away_debrief; Choose the exact registered option try_to_get_away_continue
- Preconditions: The real menu try_to_get_away_debrief is reached; option try_to_get_away_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_try_to_get_away_continue_on_condition; game_menu_try_to_get_away_end; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@201-201
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.town_outside.approach_gates.select

Select town_outside.approach_gates

- Entry/action: Client campaign menu town_outside; Choose the exact registered option approach_gates
- Preconditions: The real menu town_outside is reached; option approach_gates condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_outside_approach_gates_on_condition; game_menu_town_outside_approach_gates_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@205-205
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.town_outside.town_disguise_yourself.select

Select town_outside.town_disguise_yourself

- Entry/action: Client campaign menu town_outside; Choose the exact registered option town_disguise_yourself
- Preconditions: The real menu town_outside is reached; option town_disguise_yourself condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_disguise_yourself_on_condition; game_menu_town_initial_disguise_yourself_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@206-206
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.town_outside.town_besiege.select

Select town_outside.town_besiege

- Entry/action: Client campaign menu town_outside; Choose the exact registered option town_besiege
- Preconditions: The real menu town_outside is reached; option town_besiege condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_town_besiege_on_condition; game_menu_town_town_besiege_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@207-207
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.town_outside.town_enter_cheat.select

Select town_outside.town_enter_cheat

- Entry/action: Client campaign menu town_outside; Choose the exact registered option town_enter_cheat
- Preconditions: The real menu town_outside is reached; option town_enter_cheat condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_outside_cheat_enter_on_condition; game_menu_town_outside_enter_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@208-208
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.town_outside.town_outside_leave.select

Select town_outside.town_outside_leave

- Entry/action: Client campaign menu town_outside; Choose the exact registered option town_outside_leave
- Preconditions: The real menu town_outside is reached; option town_outside_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_leave_on_condition; game_menu_castle_outside_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@209-209
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.disguise_first_time.continue.select

Select disguise_first_time.continue

- Entry/action: Client campaign menu disguise_first_time; Choose the exact registered option continue
- Preconditions: The real menu disguise_first_time is reached; option continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks launch_mission_on_condition; launch_disguise_mission; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@216-216
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.settlement_player_unconscious_when_disguise_contact_not_set.continue.select

Select settlement_player_unconscious_when_disguise_contact_not_set.continue

- Entry/action: Client campaign menu settlement_player_unconscious_when_disguise_contact_not_set; Choose the exact registered option continue
- Preconditions: The real menu settlement_player_unconscious_when_disguise_contact_not_set is reached; option continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks mno_sneak_caught_surrender_on_condition; game_menu_captivity_castle_taken_prisoner_cont_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@227-227
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.disguise_not_first_time.quick_sneak.select

Select disguise_not_first_time.quick_sneak

- Entry/action: Client campaign menu disguise_not_first_time; Choose the exact registered option quick_sneak
- Preconditions: The real menu disguise_not_first_time is reached; option quick_sneak condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_disguise_yourself_on_condition; game_menu_town_disguise_yourself_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@229-229
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.disguise_not_first_time.take_a_walk.select

Select disguise_not_first_time.take_a_walk

- Entry/action: Client campaign menu disguise_not_first_time; Choose the exact registered option take_a_walk
- Preconditions: The real menu disguise_not_first_time is reached; option take_a_walk condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks launch_mission_on_condition; launch_disguise_mission; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@230-230
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.settlement_player_run_away_when_disguise.continue_back.select

Select settlement_player_run_away_when_disguise.continue_back

- Entry/action: Client campaign menu settlement_player_run_away_when_disguise; Choose the exact registered option continue_back
- Preconditions: The real menu settlement_player_run_away_when_disguise is reached; option continue_back condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks menu_sneak_into_town_succeeded_continue_on_condition; escape_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@236-236
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.menu_sneak_into_town_succeeded.str_continue.select

Select menu_sneak_into_town_succeeded.str_continue

- Entry/action: Client campaign menu menu_sneak_into_town_succeeded; Choose the exact registered option str_continue
- Preconditions: The real menu menu_sneak_into_town_succeeded is reached; option str_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks menu_sneak_into_town_succeeded_continue_on_condition; menu_sneak_into_town_succeeded_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@238-238
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.menu_sneak_into_town_caught.mno_sneak_caught_surrender.select

Select menu_sneak_into_town_caught.mno_sneak_caught_surrender

- Entry/action: Client campaign menu menu_sneak_into_town_caught; Choose the exact registered option mno_sneak_caught_surrender
- Preconditions: The real menu menu_sneak_into_town_caught is reached; option mno_sneak_caught_surrender condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks mno_sneak_caught_surrender_on_condition; mno_sneak_caught_surrender_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@240-240
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.menu_captivity_castle_taken_prisoner.mno_sneak_caught_surrender.select

Select menu_captivity_castle_taken_prisoner.mno_sneak_caught_surrender

- Entry/action: Client campaign menu menu_captivity_castle_taken_prisoner; Choose the exact registered option mno_sneak_caught_surrender
- Preconditions: The real menu menu_captivity_castle_taken_prisoner is reached; option mno_sneak_caught_surrender condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_captivity_castle_taken_prisoner_cont_on_condition; game_menu_captivity_castle_taken_prisoner_cont_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@242-242
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.menu_captivity_castle_taken_prisoner.cheat_continue.select

Select menu_captivity_castle_taken_prisoner.cheat_continue

- Entry/action: Client campaign menu menu_captivity_castle_taken_prisoner; Choose the exact registered option cheat_continue
- Preconditions: The real menu menu_captivity_castle_taken_prisoner is reached; option cheat_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_captivity_taken_prisoner_cheat_on_condition; game_menu_captivity_taken_prisoner_cheat_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@243-243
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.fortification_crime_rating.fortification_crime_rating_continue.select

Select fortification_crime_rating.fortification_crime_rating_continue

- Entry/action: Client campaign menu fortification_crime_rating; Choose the exact registered option fortification_crime_rating_continue
- Preconditions: The real menu fortification_crime_rating is reached; option fortification_crime_rating_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_fortification_high_crime_rating_continue_on_condition; game_menu_fortification_high_crime_rating_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@245-245
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.army_left_settlement_due_to_war_declaration.army_left_settlement_due_to_war_declaration_continue.select

Select army_left_settlement_due_to_war_declaration.army_left_settlement_due_to_war_declaration_continue

- Entry/action: Client campaign menu army_left_settlement_due_to_war_declaration; Choose the exact registered option army_left_settlement_due_to_war_declaration_continue
- Preconditions: The real menu army_left_settlement_due_to_war_declaration is reached; option army_left_settlement_due_to_war_declaration_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_army_left_settlement_due_to_war_on_condition; game_menu_army_left_settlement_due_to_war_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@247-247
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.castle_outside.approach_gates.select

Select castle_outside.approach_gates

- Entry/action: Client campaign menu castle_outside; Choose the exact registered option approach_gates
- Preconditions: The real menu castle_outside is reached; option approach_gates condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_outside_approach_gates_on_condition; game_menu_castle_outside_approach_gates_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@249-249
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.castle_outside.town_besiege.select

Select castle_outside.town_besiege

- Entry/action: Client campaign menu castle_outside; Choose the exact registered option town_besiege
- Preconditions: The real menu castle_outside is reached; option town_besiege condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_town_besiege_on_condition; game_menu_town_town_besiege_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@250-250
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.castle_outside.town_outside_leave.select

Select castle_outside.town_outside_leave

- Entry/action: Client campaign menu castle_outside; Choose the exact registered option town_outside_leave
- Preconditions: The real menu castle_outside is reached; option town_outside_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_leave_on_condition; game_menu_castle_outside_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@251-251
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.town_guard.request_meeting_commander.select

Select town_guard.request_meeting_commander

- Entry/action: Client campaign menu town_guard; Choose the exact registered option request_meeting_commander
- Preconditions: The real menu town_guard is reached; option request_meeting_commander condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_request_meeting_someone_on_condition; game_menu_request_meeting_someone_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@253-253
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.town_guard.guard_discuss_criminal_surrender.select

Select town_guard.guard_discuss_criminal_surrender

- Entry/action: Client campaign menu town_guard; Choose the exact registered option guard_discuss_criminal_surrender
- Preconditions: The real menu town_guard is reached; option guard_discuss_criminal_surrender condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks outside_menu_criminal_on_condition; outside_menu_criminal_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@254-254
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.town_guard.guard_back.select

Select town_guard.guard_back

- Entry/action: Client campaign menu town_guard; Choose the exact registered option guard_back
- Preconditions: The real menu town_guard is reached; option guard_back condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_leave_on_condition; game_menu_town_guard_back_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@255-255
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.castle_guard.request_shelter.select

Select castle_guard.request_shelter

- Entry/action: Client campaign menu castle_guard; Choose the exact registered option request_shelter
- Preconditions: The real menu castle_guard is reached; option request_shelter condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_town_guard_request_shelter_on_condition; game_menu_request_entry_to_castle_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@257-257
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.castle_guard.request_meeting_commander.select

Select castle_guard.request_meeting_commander

- Entry/action: Client campaign menu castle_guard; Choose the exact registered option request_meeting_commander
- Preconditions: The real menu castle_guard is reached; option request_meeting_commander condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_request_meeting_someone_on_condition; game_menu_request_meeting_someone_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@258-258
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.castle_guard.guard_back.select

Select castle_guard.guard_back

- Entry/action: Client campaign menu castle_guard; Choose the exact registered option guard_back
- Preconditions: The real menu castle_guard is reached; option guard_back condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_leave_on_condition; game_menu_town_guard_back_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@259-259
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.castle_enter_bribe.castle_bribe_pay.select

Select castle_enter_bribe.castle_bribe_pay

- Entry/action: Client campaign menu castle_enter_bribe; Choose the exact registered option castle_bribe_pay
- Preconditions: The real menu castle_enter_bribe is reached; option castle_bribe_pay condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_castle_enter_bribe_pay_bribe_on_condition; game_menu_castle_enter_bribe_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@261-261
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.menu_castle_entry_granted.str_continue.select

Select menu_castle_entry_granted.str_continue

- Entry/action: Client campaign menu menu_castle_entry_granted; Choose the exact registered option str_continue
- Preconditions: The real menu menu_castle_entry_granted is reached; option str_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_request_entry_to_castle_approved_continue_on_condition; game_request_entry_to_castle_approved_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@267-267
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.menu_castle_entry_denied.str_continue.select

Select menu_castle_entry_denied.str_continue

- Entry/action: Client campaign menu menu_castle_entry_denied; Choose the exact registered option str_continue
- Preconditions: The real menu menu_castle_entry_denied is reached; option str_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks null; game_request_entry_to_castle_rejected_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@269-269
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.request_meeting.request_meeting_with.select

Select request_meeting.request_meeting_with

- Entry/action: Client campaign menu request_meeting; Choose the exact registered option request_meeting_with
- Preconditions: The real menu request_meeting is reached; option request_meeting_with condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_request_meeting_with_on_condition; game_menu_request_meeting_with_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@271-271
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.request_meeting.meeting_town_leave.select

Select request_meeting.meeting_town_leave

- Entry/action: Client campaign menu request_meeting; Choose the exact registered option meeting_town_leave
- Preconditions: The real menu request_meeting is reached; option meeting_town_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_meeting_town_leave_on_condition; game_menu_request_meeting_town_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@272-272
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.request_meeting.meeting_castle_leave.select

Select request_meeting.meeting_castle_leave

- Entry/action: Client campaign menu request_meeting; Choose the exact registered option meeting_castle_leave
- Preconditions: The real menu request_meeting is reached; option meeting_castle_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_meeting_castle_leave_on_condition; game_menu_request_meeting_castle_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@273-273
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.request_meeting_with_besiegers.request_meeting_with.select

Select request_meeting_with_besiegers.request_meeting_with

- Entry/action: Client campaign menu request_meeting_with_besiegers; Choose the exact registered option request_meeting_with
- Preconditions: The real menu request_meeting_with_besiegers is reached; option request_meeting_with condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_request_meeting_with_besiegers_on_condition; game_menu_request_meeting_with_besiegers_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@275-275
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.request_meeting_with_besiegers.request_meeting_town_leave.select

Select request_meeting_with_besiegers.request_meeting_town_leave

- Entry/action: Client campaign menu request_meeting_with_besiegers; Choose the exact registered option request_meeting_town_leave
- Preconditions: The real menu request_meeting_with_besiegers is reached; option request_meeting_town_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_meeting_town_leave_on_condition; game_menu_request_meeting_town_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@276-276
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.request_meeting_with_besiegers.request_meeting_castle_leave.select

Select request_meeting_with_besiegers.request_meeting_castle_leave

- Entry/action: Client campaign menu request_meeting_with_besiegers; Choose the exact registered option request_meeting_castle_leave
- Preconditions: The real menu request_meeting_with_besiegers is reached; option request_meeting_castle_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_meeting_castle_leave_on_condition; game_menu_request_meeting_castle_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@277-277
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.village_loot_complete.continue.select

Select village_loot_complete.continue

- Entry/action: Client campaign menu village_loot_complete; Choose the exact registered option continue
- Preconditions: The real menu village_loot_complete is reached; option continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_village_loot_complete_continue_on_condition; game_menu_village_loot_complete_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@280-280
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.raid_interrupted.continue.select

Select raid_interrupted.continue

- Entry/action: Client campaign menu raid_interrupted; Choose the exact registered option continue
- Preconditions: The real menu raid_interrupted is reached; option continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_raid_interrupted_continue_on_condition; game_menu_raid_interrupted_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@282-282
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter_interrupted.encounter_interrupted_help_attackers.select

Select encounter_interrupted.encounter_interrupted_help_attackers

- Entry/action: Client campaign menu encounter_interrupted; Choose the exact registered option encounter_interrupted_help_attackers
- Preconditions: The real menu encounter_interrupted is reached; option encounter_interrupted_help_attackers condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_join_encounter_help_attackers_on_condition; game_menu_join_encounter_help_attackers_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@284-284
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter_interrupted.encounter_interrupted_help_defenders.select

Select encounter_interrupted.encounter_interrupted_help_defenders

- Entry/action: Client campaign menu encounter_interrupted; Choose the exact registered option encounter_interrupted_help_defenders
- Preconditions: The real menu encounter_interrupted is reached; option encounter_interrupted_help_defenders condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_join_encounter_help_defenders_on_condition; game_menu_join_encounter_help_defenders_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@285-285
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter_interrupted.leave.select

Select encounter_interrupted.leave

- Entry/action: Client campaign menu encounter_interrupted; Choose the exact registered option leave
- Preconditions: The real menu encounter_interrupted is reached; option leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_encounter_interrupted_leave_on_condition; game_menu_encounter_interrupted_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@286-286
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter_interrupted_siege_preparations.encounter_interrupted_siege_preparations_join_defend.select

Select encounter_interrupted_siege_preparations.encounter_interrupted_siege_preparations_join_defend

- Entry/action: Client campaign menu encounter_interrupted_siege_preparations; Choose the exact registered option encounter_interrupted_siege_preparations_join_defend
- Preconditions: The real menu encounter_interrupted_siege_preparations is reached; option encounter_interrupted_siege_preparations_join_defend condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_encounter_interrupted_siege_preparations_join_defend_on_condition; game_menu_encounter_interrupted_siege_preparations_join_defend_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@288-288
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter_interrupted_siege_preparations.encounter_interrupted_siege_preparations_break_out_of_town.select

Select encounter_interrupted_siege_preparations.encounter_interrupted_siege_preparations_break_out_of_town

- Entry/action: Client campaign menu encounter_interrupted_siege_preparations; Choose the exact registered option encounter_interrupted_siege_preparations_break_out_of_town
- Preconditions: The real menu encounter_interrupted_siege_preparations is reached; option encounter_interrupted_siege_preparations_break_out_of_town condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_encounter_interrupted_siege_preparations_break_out_of_town_on_condition; game_menu_encounter_interrupted_break_out_of_town_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@289-289
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter_interrupted_siege_preparations.encounter_interrupted_siege_preparations_leave_town.select

Select encounter_interrupted_siege_preparations.encounter_interrupted_siege_preparations_leave_town

- Entry/action: Client campaign menu encounter_interrupted_siege_preparations; Choose the exact registered option encounter_interrupted_siege_preparations_leave_town
- Preconditions: The real menu encounter_interrupted_siege_preparations is reached; option encounter_interrupted_siege_preparations_leave_town condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_encounter_interrupted_siege_preparations_leave_town_on_condition; game_menu_encounter_interrupted_leave_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@290-290
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.encounter_interrupted_raid_started.encounter_interrupted_raid_started_leave.select

Select encounter_interrupted_raid_started.encounter_interrupted_raid_started_leave

- Entry/action: Client campaign menu encounter_interrupted_raid_started; Choose the exact registered option encounter_interrupted_raid_started_leave
- Preconditions: The real menu encounter_interrupted_raid_started is reached; option encounter_interrupted_raid_started_leave condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks game_menu_encounter_interrupted_by_raid_continue_on_condition; game_menu_encounter_interrupted_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@292-292
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.continue_siege_after_attack.continue_siege.select

Select continue_siege_after_attack.continue_siege

- Entry/action: Client campaign menu continue_siege_after_attack; Choose the exact registered option continue_siege
- Preconditions: The real menu continue_siege_after_attack is reached; option continue_siege condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks continue_siege_after_attack_on_condition; continue_siege_after_attack_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@294-294
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.continue_siege_after_attack.leave_siege.select

Select continue_siege_after_attack.leave_siege

- Entry/action: Client campaign menu continue_siege_after_attack; Choose the exact registered option leave_siege
- Preconditions: The real menu continue_siege_after_attack is reached; option leave_siege condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks leave_siege_after_attack_on_condition; leave_siege_after_attack_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@295-295
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.continue_siege_after_attack.leave_army.select

Select continue_siege_after_attack.leave_army

- Entry/action: Client campaign menu continue_siege_after_attack; Choose the exact registered option leave_army
- Preconditions: The real menu continue_siege_after_attack is reached; option leave_army condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks leave_army_after_attack_on_condition; leave_army_after_attack_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@296-296
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.town_caught_by_guards.town_caught_by_guards_criminal_outside_menu_give_yourself_up.select

Select town_caught_by_guards.town_caught_by_guards_criminal_outside_menu_give_yourself_up

- Entry/action: Client campaign menu town_caught_by_guards; Choose the exact registered option town_caught_by_guards_criminal_outside_menu_give_yourself_up
- Preconditions: The real menu town_caught_by_guards is reached; option town_caught_by_guards_criminal_outside_menu_give_yourself_up condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks outside_menu_criminal_on_condition; caught_outside_menu_criminal_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@298-298
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.town_caught_by_guards.town_caught_by_guards_enemy_outside_menu_give_yourself_up.select

Select town_caught_by_guards.town_caught_by_guards_enemy_outside_menu_give_yourself_up

- Entry/action: Client campaign menu town_caught_by_guards; Choose the exact registered option town_caught_by_guards_enemy_outside_menu_give_yourself_up
- Preconditions: The real menu town_caught_by_guards is reached; option town_caught_by_guards_enemy_outside_menu_give_yourself_up condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks caught_outside_menu_enemy_on_condition; caught_outside_menu_enemy_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@299-299
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.break_in_menu.break_in_menu_accept.select

Select break_in_menu.break_in_menu_accept

- Entry/action: Client campaign menu break_in_menu; Choose the exact registered option break_in_menu_accept
- Preconditions: The real menu break_in_menu is reached; option break_in_menu_accept condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks break_in_menu_accept_on_condition; break_in_menu_accept_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@301-301
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.break_in_menu.break_in_menu_reject.select

Select break_in_menu.break_in_menu_reject

- Entry/action: Client campaign menu break_in_menu; Choose the exact registered option break_in_menu_reject
- Preconditions: The real menu break_in_menu is reached; option break_in_menu_reject condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks break_in_menu_reject_on_condition; break_in_menu_reject_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@302-302
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.break_in_debrief_menu.break_in_debrief_continue.select

Select break_in_debrief_menu.break_in_debrief_continue

- Entry/action: Client campaign menu break_in_debrief_menu; Choose the exact registered option break_in_debrief_continue
- Preconditions: The real menu break_in_debrief_menu is reached; option break_in_debrief_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks continue_on_condition; break_in_debrief_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@304-304
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.break_out_menu.break_out_menu_accept.select

Select break_out_menu.break_out_menu_accept

- Entry/action: Client campaign menu break_out_menu; Choose the exact registered option break_out_menu_accept
- Preconditions: The real menu break_out_menu is reached; option break_out_menu_accept condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks break_out_menu_accept_on_condition; break_out_menu_accept_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@306-306
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.break_out_menu.break_out_menu_reject.select

Select break_out_menu.break_out_menu_reject

- Entry/action: Client campaign menu break_out_menu; Choose the exact registered option break_out_menu_reject
- Preconditions: The real menu break_out_menu is reached; option break_out_menu_reject condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks break_out_menu_reject_on_condition; break_out_menu_reject_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@307-307
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

### menu.encounter.break_out_debrief_menu.break_out_debrief_continue.select

Select break_out_debrief_menu.break_out_debrief_continue

- Entry/action: Client campaign menu break_out_debrief_menu; Choose the exact registered option break_out_debrief_continue
- Preconditions: The real menu break_out_debrief_menu is reached; option break_out_debrief_continue condition/callback and module/access gates require interpretation
- Expected: Installed registration names callbacks continue_on_condition; break_out_debrief_continue_on_consequence; enabled state and consequence are not established by the registration
- State: Resolve exactly this option consequence; null callbacks may leave work in the condition or another UI path
- Side effects: Trace the condition and consequence independently, including any UI entry, event, resource or party change
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Hidden, disabled, denied access and repeated choice remain distinct cases; registration alone proves none
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: definition-inspected; runtime unrun; support unverified
- Source span: TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior@309-309
- Remaining: Exact option declaration inspected; callback/caller outcomes, co-op routing, UI behavior and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json), [features/baseline/installed.json](../../features/baseline/installed.json)

## movement.010 Map movement speed

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.010.case-01

Map movement speed / food

- Entry/action: Client campaign map; Perform map movement speed specifically in the food context
- Preconditions: Isolated food context for map movement speed
- Expected: Acceptance requirement for food: observed travel rate follows the applicable model and current party state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.010.case-02

Map movement speed / morale

- Entry/action: Client campaign map; Perform map movement speed specifically in the morale context
- Preconditions: Isolated morale context for map movement speed
- Expected: Acceptance requirement for morale: observed travel rate follows the applicable model and current party state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.010.case-03

Map movement speed / terrain

- Entry/action: Client campaign map; Perform map movement speed specifically in the terrain context
- Preconditions: Isolated terrain context for map movement speed
- Expected: Acceptance requirement for terrain: observed travel rate follows the applicable model and current party state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.010.case-04.1

Map movement speed / day

- Entry/action: Client campaign map; Perform map movement speed specifically in the day context
- Preconditions: Isolated day context for map movement speed
- Expected: Acceptance requirement for day: observed travel rate follows the applicable model and current party state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.010.case-04.2

Map movement speed / night

- Entry/action: Client campaign map; Perform map movement speed specifically in the night context
- Preconditions: Isolated night context for map movement speed
- Expected: Acceptance requirement for night: observed travel rate follows the applicable model and current party state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.010.case-05

Map movement speed / load

- Entry/action: Client campaign map; Perform map movement speed specifically in the load context
- Preconditions: Isolated load context for map movement speed
- Expected: Acceptance requirement for load: observed travel rate follows the applicable model and current party state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

## movement.011 Map visibility

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.011.case-01

Map visibility / nearby

- Entry/action: Client campaign map; Perform map visibility specifically in the nearby context
- Preconditions: Isolated nearby context for map visibility
- Expected: Acceptance requirement for nearby: each player sees the permitted authoritative party state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.011.case-02

Map visibility / outside sight

- Entry/action: Client campaign map; Perform map visibility specifically in the outside sight context
- Preconditions: Isolated outside sight context for map visibility
- Expected: Acceptance requirement for outside sight: each player sees the permitted authoritative party state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.011.case-03

Map visibility / hidden party

- Entry/action: Client campaign map; Perform map visibility specifically in the hidden party context
- Preconditions: Isolated hidden party context for map visibility
- Expected: Acceptance requirement for hidden party: each player sees the permitted authoritative party state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.011.case-04

Map visibility / late join

- Entry/action: Client campaign map; Perform map visibility specifically in the late join context
- Preconditions: Isolated late join context for map visibility
- Expected: Acceptance requirement for late join: each player sees the permitted authoritative party state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

## movement.012 Map tracks

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.012.case-01

Map tracks / track creation

- Entry/action: Client campaign map; Perform map tracks specifically in the track creation context
- Preconditions: Isolated track creation context for map tracks
- Expected: Acceptance requirement for track creation: track identity and presentation match the observed campaign activity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.012.case-02

Map tracks / age

- Entry/action: Client campaign map; Perform map tracks specifically in the age context
- Preconditions: Isolated age context for map tracks
- Expected: Acceptance requirement for age: track identity and presentation match the observed campaign activity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.012.case-03

Map tracks / followed party

- Entry/action: Client campaign map; Perform map tracks specifically in the followed party context
- Preconditions: Isolated followed party context for map tracks
- Expected: Acceptance requirement for followed party: track identity and presentation match the observed campaign activity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

## movement.013 Settlement exit

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.013.case-01

Settlement exit / ordinary leave

- Entry/action: Client campaign map; Perform settlement exit specifically in the ordinary leave context
- Preconditions: Isolated ordinary leave context for settlement exit
- Expected: Acceptance requirement for ordinary leave: party exits the correct settlement and can move afterward
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.013.case-02

Settlement exit / hostile escape

- Entry/action: Client campaign map; Perform settlement exit specifically in the hostile escape context
- Preconditions: Isolated hostile escape context for settlement exit
- Expected: Acceptance requirement for hostile escape: party exits the correct settlement and can move afterward
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.013.case-03

Settlement exit / menu transition

- Entry/action: Client campaign map; Perform settlement exit specifically in the menu transition context
- Preconditions: Isolated menu transition context for settlement exit
- Expected: Acceptance requirement for menu transition: party exits the correct settlement and can move afterward
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

## movement.014 Destroy movement target

Each entry retains authority and separate owner/observer observations in the CSV.

### movement.014.case-01

Destroy movement target / target party removed

- Entry/action: Client campaign map; Perform destroy movement target specifically in the target party removed context
- Preconditions: Isolated target party removed context for destroy movement target
- Expected: Acceptance requirement for target party removed: movement releases the dead target without registry lookup failure
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)

### movement.014.case-02

Destroy movement target / settlement context changes

- Entry/action: Client campaign map; Perform destroy movement target specifically in the settlement context changes context
- Preconditions: Isolated settlement context changes context for destroy movement target
- Expected: Acceptance requirement for settlement context changes: movement releases the dead target without registry lookup failure
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MobileParties](../../source/GameInterface/Services/MobileParties), [source/GameInterface/Services/MobilePartyAIs](../../source/GameInterface/Services/MobilePartyAIs), [source/GameInterface/Services/MapTracks](../../source/GameInterface/Services/MapTracks)
