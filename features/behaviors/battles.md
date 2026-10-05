# battles behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## battles.001 Attack enemy party

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.001.case-01

Attack enemy party / first encounter

- Entry/action: Campaign encounter / rendered battle mission; Perform attack enemy party specifically in the first encounter context
- Preconditions: Isolated first encounter context for attack enemy party
- Expected: Acceptance requirement for first encounter: correct registered parties enter the same authoritative map event
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.001.case-02

Attack enemy party / two clients

- Entry/action: Campaign encounter / rendered battle mission; Perform attack enemy party specifically in the two clients context
- Preconditions: Isolated two clients context for attack enemy party
- Expected: Acceptance requirement for two clients: correct registered parties enter the same authoritative map event
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.001.case-03

Attack enemy party / AI ally

- Entry/action: Campaign encounter / rendered battle mission; Perform attack enemy party specifically in the AI ally context
- Preconditions: Isolated AI ally context for attack enemy party
- Expected: Acceptance requirement for AI ally: correct registered parties enter the same authoritative map event
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.002 Join allied battle

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.002.case-01

Join allied battle / attacker

- Entry/action: Campaign encounter / rendered battle mission; Perform join allied battle specifically in the attacker context
- Preconditions: Isolated attacker context for join allied battle
- Expected: Acceptance requirement for attacker: joining party and player appear on the intended battle side
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.002.case-02

Join allied battle / defender

- Entry/action: Campaign encounter / rendered battle mission; Perform join allied battle specifically in the defender context
- Preconditions: Isolated defender context for join allied battle
- Expected: Acceptance requirement for defender: joining party and player appear on the intended battle side
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.002.case-03

Join allied battle / reinforcement

- Entry/action: Campaign encounter / rendered battle mission; Perform join allied battle specifically in the reinforcement context
- Preconditions: Isolated reinforcement context for join allied battle
- Expected: Acceptance requirement for reinforcement: joining party and player appear on the intended battle side
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.002.case-04

Join allied battle / late entry

- Entry/action: Campaign encounter / rendered battle mission; Perform join allied battle specifically in the late entry context
- Preconditions: Isolated late entry context for join allied battle
- Expected: Acceptance requirement for late entry: joining party and player appear on the intended battle side
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.003 Simulate battle

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.003.case-01

Simulate battle / attack

- Entry/action: Campaign encounter / rendered battle mission; Perform simulate battle specifically in the attack context
- Preconditions: Isolated attack context for simulate battle
- Expected: Acceptance requirement for attack: resulting losses/prisoners/rewards agree without rendered combat
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.003.case-02

Simulate battle / defense

- Entry/action: Campaign encounter / rendered battle mission; Perform simulate battle specifically in the defense context
- Preconditions: Isolated defense context for simulate battle
- Expected: Acceptance requirement for defense: resulting losses/prisoners/rewards agree without rendered combat
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.003.case-03

Simulate battle / participating player parties

- Entry/action: Campaign encounter / rendered battle mission; Perform simulate battle specifically in the participating player parties context
- Preconditions: Isolated participating player parties context for simulate battle
- Expected: Acceptance requirement for participating player parties: resulting losses/prisoners/rewards agree without rendered combat
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.004 Battle preparation

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.004.case-01

Battle preparation / deployment

- Entry/action: Campaign encounter / rendered battle mission; Perform battle preparation specifically in the deployment context
- Preconditions: Isolated deployment context for battle preparation
- Expected: Acceptance requirement for deployment: player and troops spawn on the correct battle team
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.004.case-02

Battle preparation / equipment

- Entry/action: Campaign encounter / rendered battle mission; Perform battle preparation specifically in the equipment context
- Preconditions: Isolated equipment context for battle preparation
- Expected: Acceptance requirement for equipment: player and troops spawn on the correct battle team
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.004.case-03

Battle preparation / side assignment

- Entry/action: Campaign encounter / rendered battle mission; Perform battle preparation specifically in the side assignment context
- Preconditions: Isolated side assignment context for battle preparation
- Expected: Acceptance requirement for side assignment: player and troops spawn on the correct battle team
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.005 Spawn troops

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.005.case-01

Spawn troops / initial wave

- Entry/action: Campaign encounter / rendered battle mission; Perform spawn troops specifically in the initial wave context
- Preconditions: Isolated initial wave context for spawn troops
- Expected: Acceptance requirement for initial wave: spawned/reserve counts conserve the authoritative roster
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.005.case-02

Spawn troops / reinforcements

- Entry/action: Campaign encounter / rendered battle mission; Perform spawn troops specifically in the reinforcements context
- Preconditions: Isolated reinforcements context for spawn troops
- Expected: Acceptance requirement for reinforcements: spawned/reserve counts conserve the authoritative roster
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.005.case-03

Spawn troops / wounded excluded

- Entry/action: Campaign encounter / rendered battle mission; Perform spawn troops specifically in the wounded excluded context
- Preconditions: Isolated wounded excluded context for spawn troops
- Expected: Acceptance requirement for wounded excluded: spawned/reserve counts conserve the authoritative roster
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.006 Spawn player hero

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.006.case-01

Spawn player hero / owner

- Entry/action: Campaign encounter / rendered battle mission; Perform spawn player hero specifically in the owner context
- Preconditions: Isolated owner context for spawn player hero
- Expected: Acceptance requirement for owner: one active agent represents each participating player hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.006.case-02

Spawn player hero / remote peer

- Entry/action: Campaign encounter / rendered battle mission; Perform spawn player hero specifically in the remote peer context
- Preconditions: Isolated remote peer context for spawn player hero
- Expected: Acceptance requirement for remote peer: one active agent represents each participating player hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.006.case-03

Spawn player hero / reconnect

- Entry/action: Campaign encounter / rendered battle mission; Perform spawn player hero specifically in the reconnect context
- Preconditions: Isolated reconnect context for spawn player hero
- Expected: Acceptance requirement for reconnect: one active agent represents each participating player hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.007 Agent movement

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.007.case-01

Agent movement / walk

- Entry/action: Campaign encounter / rendered battle mission; Perform agent movement specifically in the walk context
- Preconditions: Isolated walk context for agent movement
- Expected: Acceptance requirement for walk: remote agent position/animation follows the owned agent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.007.case-02

Agent movement / run

- Entry/action: Campaign encounter / rendered battle mission; Perform agent movement specifically in the run context
- Preconditions: Isolated run context for agent movement
- Expected: Acceptance requirement for run: remote agent position/animation follows the owned agent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.007.case-03

Agent movement / jump

- Entry/action: Campaign encounter / rendered battle mission; Perform agent movement specifically in the jump context
- Preconditions: Isolated jump context for agent movement
- Expected: Acceptance requirement for jump: remote agent position/animation follows the owned agent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.007.case-04

Agent movement / crouch

- Entry/action: Campaign encounter / rendered battle mission; Perform agent movement specifically in the crouch context
- Preconditions: Isolated crouch context for agent movement
- Expected: Acceptance requirement for crouch: remote agent position/animation follows the owned agent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.007.case-05

Agent movement / rotate

- Entry/action: Campaign encounter / rendered battle mission; Perform agent movement specifically in the rotate context
- Preconditions: Isolated rotate context for agent movement
- Expected: Acceptance requirement for rotate: remote agent position/animation follows the owned agent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.008 Weapon attack

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.008.case-01

Weapon attack / swing directions

- Entry/action: Campaign encounter / rendered battle mission; Perform weapon attack specifically in the swing directions context
- Preconditions: Isolated swing directions context for weapon attack
- Expected: Acceptance requirement for swing directions: remote clients observe the same accepted attack and resulting hit
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.008.case-02

Weapon attack / thrust

- Entry/action: Campaign encounter / rendered battle mission; Perform weapon attack specifically in the thrust context
- Preconditions: Isolated thrust context for weapon attack
- Expected: Acceptance requirement for thrust: remote clients observe the same accepted attack and resulting hit
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.008.case-03

Weapon attack / weapon mode

- Entry/action: Campaign encounter / rendered battle mission; Perform weapon attack specifically in the weapon mode context
- Preconditions: Isolated weapon mode context for weapon attack
- Expected: Acceptance requirement for weapon mode: remote clients observe the same accepted attack and resulting hit
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.009 Block attack

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.009.case-01

Block attack / directional weapon block

- Entry/action: Campaign encounter / rendered battle mission; Perform block attack specifically in the directional weapon block context
- Preconditions: Isolated directional weapon block context for block attack
- Expected: Acceptance requirement for directional weapon block: block/damage outcome agrees across the battle
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.009.case-02

Block attack / shield block

- Entry/action: Campaign encounter / rendered battle mission; Perform block attack specifically in the shield block context
- Preconditions: Isolated shield block context for block attack
- Expected: Acceptance requirement for shield block: block/damage outcome agrees across the battle
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.010 Kick or shield bash

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.010.case-01

Kick or shield bash / hit

- Entry/action: Campaign encounter / rendered battle mission; Perform kick or shield bash specifically in the hit context
- Preconditions: Isolated hit context for kick or shield bash
- Expected: Acceptance requirement for hit: impulse/stun and damage outcomes converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.010.case-02

Kick or shield bash / miss

- Entry/action: Campaign encounter / rendered battle mission; Perform kick or shield bash specifically in the miss context
- Preconditions: Isolated miss context for kick or shield bash
- Expected: Acceptance requirement for miss: impulse/stun and damage outcomes converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.010.case-03

Kick or shield bash / interrupted

- Entry/action: Campaign encounter / rendered battle mission; Perform kick or shield bash specifically in the interrupted context
- Preconditions: Isolated interrupted context for kick or shield bash
- Expected: Acceptance requirement for interrupted: impulse/stun and damage outcomes converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.011 Ranged attack

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.011.case-01

Ranged attack / bow

- Entry/action: Campaign encounter / rendered battle mission; Perform ranged attack specifically in the bow context
- Preconditions: Isolated bow context for ranged attack
- Expected: Acceptance requirement for bow: projectile and resulting damage are applied once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.011.case-02

Ranged attack / crossbow

- Entry/action: Campaign encounter / rendered battle mission; Perform ranged attack specifically in the crossbow context
- Preconditions: Isolated crossbow context for ranged attack
- Expected: Acceptance requirement for crossbow: projectile and resulting damage are applied once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.011.case-03

Ranged attack / throwing

- Entry/action: Campaign encounter / rendered battle mission; Perform ranged attack specifically in the throwing context
- Preconditions: Isolated throwing context for ranged attack
- Expected: Acceptance requirement for throwing: projectile and resulting damage are applied once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.011.case-04.1

Ranged attack / projectile miss

- Entry/action: Campaign encounter / rendered battle mission; Perform ranged attack specifically in the projectile miss context
- Preconditions: Isolated projectile miss context for ranged attack
- Expected: Acceptance requirement for projectile miss: projectile and resulting damage are applied once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.011.case-04.2

Ranged attack / projectile hit

- Entry/action: Campaign encounter / rendered battle mission; Perform ranged attack specifically in the projectile hit context
- Preconditions: Isolated projectile hit context for ranged attack
- Expected: Acceptance requirement for projectile hit: projectile and resulting damage are applied once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.012 Switch equipment

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.012.case-01

Switch equipment / weapon slot

- Entry/action: Campaign encounter / rendered battle mission; Perform switch equipment specifically in the weapon slot context
- Preconditions: Isolated weapon slot context for switch equipment
- Expected: Acceptance requirement for weapon slot: equipment identity and ammo state agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.012.case-02

Switch equipment / pickup

- Entry/action: Campaign encounter / rendered battle mission; Perform switch equipment specifically in the pickup context
- Preconditions: Isolated pickup context for switch equipment
- Expected: Acceptance requirement for pickup: equipment identity and ammo state agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.012.case-03

Switch equipment / drop

- Entry/action: Campaign encounter / rendered battle mission; Perform switch equipment specifically in the drop context
- Preconditions: Isolated drop context for switch equipment
- Expected: Acceptance requirement for drop: equipment identity and ammo state agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.012.case-04

Switch equipment / depleted ammunition

- Entry/action: Campaign encounter / rendered battle mission; Perform switch equipment specifically in the depleted ammunition context
- Preconditions: Isolated depleted ammunition context for switch equipment
- Expected: Acceptance requirement for depleted ammunition: equipment identity and ammo state agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.013 Mount or dismount

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.013.case-01

Mount or dismount / owned mount

- Entry/action: Campaign encounter / rendered battle mission; Perform mount or dismount specifically in the owned mount context
- Preconditions: Isolated owned mount context for mount or dismount
- Expected: Acceptance requirement for owned mount: rider/mount ownership and attachment match on both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.013.case-02

Mount or dismount / shared mount

- Entry/action: Campaign encounter / rendered battle mission; Perform mount or dismount specifically in the shared mount context
- Preconditions: Isolated shared mount context for mount or dismount
- Expected: Acceptance requirement for shared mount: rider/mount ownership and attachment match on both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.013.case-03

Mount or dismount / dead mount

- Entry/action: Campaign encounter / rendered battle mission; Perform mount or dismount specifically in the dead mount context
- Preconditions: Isolated dead mount context for mount or dismount
- Expected: Acceptance requirement for dead mount: rider/mount ownership and attachment match on both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.014 Horse damage

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.014.case-01

Horse damage / charge

- Entry/action: Campaign encounter / rendered battle mission; Perform horse damage specifically in the charge context
- Preconditions: Isolated charge context for horse damage
- Expected: Acceptance requirement for charge: health and mounted state converge with authoritative casualty reporting
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.014.case-02

Horse damage / projectile

- Entry/action: Campaign encounter / rendered battle mission; Perform horse damage specifically in the projectile context
- Preconditions: Isolated projectile context for horse damage
- Expected: Acceptance requirement for projectile: health and mounted state converge with authoritative casualty reporting
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.014.case-03

Horse damage / death

- Entry/action: Campaign encounter / rendered battle mission; Perform horse damage specifically in the death context
- Preconditions: Isolated death context for horse damage
- Expected: Acceptance requirement for death: health and mounted state converge with authoritative casualty reporting
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.014.case-04

Horse damage / rider fall

- Entry/action: Campaign encounter / rendered battle mission; Perform horse damage specifically in the rider fall context
- Preconditions: Isolated rider fall context for horse damage
- Expected: Acceptance requirement for rider fall: health and mounted state converge with authoritative casualty reporting
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.015 Troop health

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.015.case-01

Troop health / damage

- Entry/action: Campaign encounter / rendered battle mission; Perform troop health specifically in the damage context
- Preconditions: Isolated damage context for troop health
- Expected: Acceptance requirement for damage: each health update belongs to the correct troop/reserve entry
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.015.case-02

Troop health / wound

- Entry/action: Campaign encounter / rendered battle mission; Perform troop health specifically in the wound context
- Preconditions: Isolated wound context for troop health
- Expected: Acceptance requirement for wound: each health update belongs to the correct troop/reserve entry
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.015.case-03

Troop health / kill

- Entry/action: Campaign encounter / rendered battle mission; Perform troop health specifically in the kill context
- Preconditions: Isolated kill context for troop health
- Expected: Acceptance requirement for kill: each health update belongs to the correct troop/reserve entry
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.015.case-04

Troop health / collection during battle

- Entry/action: Campaign encounter / rendered battle mission; Perform troop health specifically in the collection during battle context
- Preconditions: Isolated collection during battle context for troop health
- Expected: Acceptance requirement for collection during battle: each health update belongs to the correct troop/reserve entry
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.016 Player health

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.016.case-01

Player health / damage

- Entry/action: Campaign encounter / rendered battle mission; Perform player health specifically in the damage context
- Preconditions: Isolated damage context for player health
- Expected: Acceptance requirement for damage: owning and observing clients agree on health/outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.016.case-02

Player health / incapacitation

- Entry/action: Campaign encounter / rendered battle mission; Perform player health specifically in the incapacitation context
- Preconditions: Isolated incapacitation context for player health
- Expected: Acceptance requirement for incapacitation: owning and observing clients agree on health/outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.016.case-03

Player health / healing if permitted

- Entry/action: Campaign encounter / rendered battle mission; Perform player health specifically in the healing if permitted context
- Preconditions: Isolated healing if permitted context for player health
- Expected: Acceptance requirement for healing if permitted: owning and observing clients agree on health/outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.017 Formation captain

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.017.case-01

Formation captain / player

- Entry/action: Campaign encounter / rendered battle mission; Perform formation captain specifically in the player context
- Preconditions: Isolated player context for formation captain
- Expected: Acceptance requirement for player: formation authority and bonuses follow the eligible captain
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.017.case-02

Formation captain / AI hero

- Entry/action: Campaign encounter / rendered battle mission; Perform formation captain specifically in the AI hero context
- Preconditions: Isolated AI hero context for formation captain
- Expected: Acceptance requirement for AI hero: formation authority and bonuses follow the eligible captain
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.017.case-03

Formation captain / leader lost

- Entry/action: Campaign encounter / rendered battle mission; Perform formation captain specifically in the leader lost context
- Preconditions: Isolated leader lost context for formation captain
- Expected: Acceptance requirement for leader lost: formation authority and bonuses follow the eligible captain
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.018 Formation selection

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.018.case-01

Formation selection / infantry

- Entry/action: Campaign encounter / rendered battle mission; Perform formation selection specifically in the infantry context
- Preconditions: Isolated infantry context for formation selection
- Expected: Acceptance requirement for infantry: only selected owned formations receive the order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.018.case-02

Formation selection / ranged

- Entry/action: Campaign encounter / rendered battle mission; Perform formation selection specifically in the ranged context
- Preconditions: Isolated ranged context for formation selection
- Expected: Acceptance requirement for ranged: only selected owned formations receive the order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.018.case-03

Formation selection / cavalry

- Entry/action: Campaign encounter / rendered battle mission; Perform formation selection specifically in the cavalry context
- Preconditions: Isolated cavalry context for formation selection
- Expected: Acceptance requirement for cavalry: only selected owned formations receive the order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.018.case-04

Formation selection / multiple groups

- Entry/action: Campaign encounter / rendered battle mission; Perform formation selection specifically in the multiple groups context
- Preconditions: Isolated multiple groups context for formation selection
- Expected: Acceptance requirement for multiple groups: only selected owned formations receive the order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.019 Formation movement order

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.019.case-01

Formation movement order / move

- Entry/action: Campaign encounter / rendered battle mission; Perform formation movement order specifically in the move context
- Preconditions: Isolated move context for formation movement order
- Expected: Acceptance requirement for move: troops follow the current valid order on the correct team
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.019.case-02

Formation movement order / follow

- Entry/action: Campaign encounter / rendered battle mission; Perform formation movement order specifically in the follow context
- Preconditions: Isolated follow context for formation movement order
- Expected: Acceptance requirement for follow: troops follow the current valid order on the correct team
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.019.case-03

Formation movement order / charge

- Entry/action: Campaign encounter / rendered battle mission; Perform formation movement order specifically in the charge context
- Preconditions: Isolated charge context for formation movement order
- Expected: Acceptance requirement for charge: troops follow the current valid order on the correct team
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.019.case-04

Formation movement order / advance

- Entry/action: Campaign encounter / rendered battle mission; Perform formation movement order specifically in the advance context
- Preconditions: Isolated advance context for formation movement order
- Expected: Acceptance requirement for advance: troops follow the current valid order on the correct team
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.019.case-05

Formation movement order / retreat

- Entry/action: Campaign encounter / rendered battle mission; Perform formation movement order specifically in the retreat context
- Preconditions: Isolated retreat context for formation movement order
- Expected: Acceptance requirement for retreat: troops follow the current valid order on the correct team
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.019.case-06

Formation movement order / hold

- Entry/action: Campaign encounter / rendered battle mission; Perform formation movement order specifically in the hold context
- Preconditions: Isolated hold context for formation movement order
- Expected: Acceptance requirement for hold: troops follow the current valid order on the correct team
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.020 Formation arrangement

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.020.case-01

Formation arrangement / line

- Entry/action: Campaign encounter / rendered battle mission; Perform formation arrangement specifically in the line context
- Preconditions: Isolated line context for formation arrangement
- Expected: Acceptance requirement for line: actual eligible formation arrangement matches the selected order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.020.case-02

Formation arrangement / shield wall

- Entry/action: Campaign encounter / rendered battle mission; Perform formation arrangement specifically in the shield wall context
- Preconditions: Isolated shield wall context for formation arrangement
- Expected: Acceptance requirement for shield wall: actual eligible formation arrangement matches the selected order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.020.case-03

Formation arrangement / loose

- Entry/action: Campaign encounter / rendered battle mission; Perform formation arrangement specifically in the loose context
- Preconditions: Isolated loose context for formation arrangement
- Expected: Acceptance requirement for loose: actual eligible formation arrangement matches the selected order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.020.case-04

Formation arrangement / square

- Entry/action: Campaign encounter / rendered battle mission; Perform formation arrangement specifically in the square context
- Preconditions: Isolated square context for formation arrangement
- Expected: Acceptance requirement for square: actual eligible formation arrangement matches the selected order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.020.case-05

Formation arrangement / circle

- Entry/action: Campaign encounter / rendered battle mission; Perform formation arrangement specifically in the circle context
- Preconditions: Isolated circle context for formation arrangement
- Expected: Acceptance requirement for circle: actual eligible formation arrangement matches the selected order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.020.case-06

Formation arrangement / column

- Entry/action: Campaign encounter / rendered battle mission; Perform formation arrangement specifically in the column context
- Preconditions: Isolated column context for formation arrangement
- Expected: Acceptance requirement for column: actual eligible formation arrangement matches the selected order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.021 Formation firing order

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.021.case-01

Formation firing order / hold fire

- Entry/action: Campaign encounter / rendered battle mission; Perform formation firing order specifically in the hold fire context
- Preconditions: Isolated hold fire context for formation firing order
- Expected: Acceptance requirement for hold fire: ranged troops obey the accepted order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.021.case-02

Formation firing order / fire at will

- Entry/action: Campaign encounter / rendered battle mission; Perform formation firing order specifically in the fire at will context
- Preconditions: Isolated fire at will context for formation firing order
- Expected: Acceptance requirement for fire at will: ranged troops obey the accepted order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.022 Formation mount order

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.022.case-01

Formation mount order / mount

- Entry/action: Campaign encounter / rendered battle mission; Perform formation mount order specifically in the mount context
- Preconditions: Isolated mount context for formation mount order
- Expected: Acceptance requirement for mount: eligible troops change mounted state consistently
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.022.case-02

Formation mount order / dismount

- Entry/action: Campaign encounter / rendered battle mission; Perform formation mount order specifically in the dismount context
- Preconditions: Isolated dismount context for formation mount order
- Expected: Acceptance requirement for dismount: eligible troops change mounted state consistently
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.022.case-03

Formation mount order / unavailable mounts

- Entry/action: Campaign encounter / rendered battle mission; Perform formation mount order specifically in the unavailable mounts context
- Preconditions: Isolated unavailable mounts context for formation mount order
- Expected: Acceptance requirement for unavailable mounts: eligible troops change mounted state consistently
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.023 Reinforcement wave

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.023.case-01

Reinforcement wave / initial supply exhausted

- Entry/action: Campaign encounter / rendered battle mission; Perform reinforcement wave specifically in the initial supply exhausted context
- Preconditions: Isolated initial supply exhausted context for reinforcement wave
- Expected: Acceptance requirement for initial supply exhausted: new agents consume the correct reserve entries once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.023.case-02

Reinforcement wave / reserve remains

- Entry/action: Campaign encounter / rendered battle mission; Perform reinforcement wave specifically in the reserve remains context
- Preconditions: Isolated reserve remains context for reinforcement wave
- Expected: Acceptance requirement for reserve remains: new agents consume the correct reserve entries once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.024 Battle retreat

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.024.case-01

Battle retreat / player retreat

- Entry/action: Campaign encounter / rendered battle mission; Perform battle retreat specifically in the player retreat context
- Preconditions: Isolated player retreat context for battle retreat
- Expected: Acceptance requirement for player retreat: casualty accounting and returned rosters conserve troop counts
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.024.case-02

Battle retreat / troop rout

- Entry/action: Campaign encounter / rendered battle mission; Perform battle retreat specifically in the troop rout context
- Preconditions: Isolated troop rout context for battle retreat
- Expected: Acceptance requirement for troop rout: casualty accounting and returned rosters conserve troop counts
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.024.case-03

Battle retreat / wounded survivor

- Entry/action: Campaign encounter / rendered battle mission; Perform battle retreat specifically in the wounded survivor context
- Preconditions: Isolated wounded survivor context for battle retreat
- Expected: Acceptance requirement for wounded survivor: casualty accounting and returned rosters conserve troop counts
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.025 Battle authority handoff

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.025.case-01

Battle authority handoff / pacer leaves

- Entry/action: Campaign encounter / rendered battle mission; Perform battle authority handoff specifically in the pacer leaves context
- Preconditions: Isolated pacer leaves context for battle authority handoff
- Expected: Acceptance requirement for pacer leaves: remaining peers retain one current authority and valid battle state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.025.case-02

Battle authority handoff / reconnect

- Entry/action: Campaign encounter / rendered battle mission; Perform battle authority handoff specifically in the reconnect context
- Preconditions: Isolated reconnect context for battle authority handoff
- Expected: Acceptance requirement for reconnect: remaining peers retain one current authority and valid battle state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.025.case-03

Battle authority handoff / pending spawn

- Entry/action: Campaign encounter / rendered battle mission; Perform battle authority handoff specifically in the pending spawn context
- Preconditions: Isolated pending spawn context for battle authority handoff
- Expected: Acceptance requirement for pending spawn: remaining peers retain one current authority and valid battle state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.026 Battle completion

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.026.case-01

Battle completion / victory

- Entry/action: Campaign encounter / rendered battle mission; Perform battle completion specifically in the victory context
- Preconditions: Isolated victory context for battle completion
- Expected: Acceptance requirement for victory: map event finalizes once with consistent surviving rosters
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.026.case-02

Battle completion / defeat

- Entry/action: Campaign encounter / rendered battle mission; Perform battle completion specifically in the defeat context
- Preconditions: Isolated defeat context for battle completion
- Expected: Acceptance requirement for defeat: map event finalizes once with consistent surviving rosters
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.026.case-03

Battle completion / draw

- Entry/action: Campaign encounter / rendered battle mission; Perform battle completion specifically in the draw context
- Preconditions: Isolated draw context for battle completion
- Expected: Acceptance requirement for draw: map event finalizes once with consistent surviving rosters
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.026.case-04

Battle completion / all participants removed

- Entry/action: Campaign encounter / rendered battle mission; Perform battle completion specifically in the all participants removed context
- Preconditions: Isolated all participants removed context for battle completion
- Expected: Acceptance requirement for all participants removed: map event finalizes once with consistent surviving rosters
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.027 Battle loot

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.027.case-01

Battle loot / winner

- Entry/action: Campaign encounter / rendered battle mission; Perform battle loot specifically in the winner context
- Preconditions: Isolated winner context for battle loot
- Expected: Acceptance requirement for winner: accepted loot reaches intended player inventories
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.027.case-02

Battle loot / participant contribution

- Entry/action: Campaign encounter / rendered battle mission; Perform battle loot specifically in the participant contribution context
- Preconditions: Isolated participant contribution context for battle loot
- Expected: Acceptance requirement for participant contribution: accepted loot reaches intended player inventories
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.027.case-03

Battle loot / abandoned loot

- Entry/action: Campaign encounter / rendered battle mission; Perform battle loot specifically in the abandoned loot context
- Preconditions: Isolated abandoned loot context for battle loot
- Expected: Acceptance requirement for abandoned loot: accepted loot reaches intended player inventories
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.028 Battle prisoners

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.028.case-01

Battle prisoners / hero capture

- Entry/action: Campaign encounter / rendered battle mission; Perform battle prisoners specifically in the hero capture context
- Preconditions: Isolated hero capture context for battle prisoners
- Expected: Acceptance requirement for hero capture: prisoner identities and rosters agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.028.case-02

Battle prisoners / troop capture

- Entry/action: Campaign encounter / rendered battle mission; Perform battle prisoners specifically in the troop capture context
- Preconditions: Isolated troop capture context for battle prisoners
- Expected: Acceptance requirement for troop capture: prisoner identities and rosters agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.028.case-03

Battle prisoners / escaped hero

- Entry/action: Campaign encounter / rendered battle mission; Perform battle prisoners specifically in the escaped hero context
- Preconditions: Isolated escaped hero context for battle prisoners
- Expected: Acceptance requirement for escaped hero: prisoner identities and rosters agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.029 Post-battle relations/rewards

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.029.case-01

Post-battle relations/rewards / renown

- Entry/action: Campaign encounter / rendered battle mission; Perform post-battle relations/rewards specifically in the renown context
- Preconditions: Isolated renown context for post-battle relations/rewards
- Expected: Acceptance requirement for renown: each reward/side effect targets its actual eligible owner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.029.case-02

Post-battle relations/rewards / influence

- Entry/action: Campaign encounter / rendered battle mission; Perform post-battle relations/rewards specifically in the influence context
- Preconditions: Isolated influence context for post-battle relations/rewards
- Expected: Acceptance requirement for influence: each reward/side effect targets its actual eligible owner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.029.case-03

Post-battle relations/rewards / skill XP

- Entry/action: Campaign encounter / rendered battle mission; Perform post-battle relations/rewards specifically in the skill XP context
- Preconditions: Isolated skill XP context for post-battle relations/rewards
- Expected: Acceptance requirement for skill XP: each reward/side effect targets its actual eligible owner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.029.case-04

Post-battle relations/rewards / quest progress

- Entry/action: Campaign encounter / rendered battle mission; Perform post-battle relations/rewards specifically in the quest progress context
- Preconditions: Isolated quest progress context for post-battle relations/rewards
- Expected: Acceptance requirement for quest progress: each reward/side effect targets its actual eligible owner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## battles.030 Mission exit

Each entry retains authority and separate owner/observer observations in the CSV.

### battles.030.case-01

Mission exit / normal

- Entry/action: Campaign encounter / rendered battle mission; Perform mission exit specifically in the normal context
- Preconditions: Isolated normal context for mission exit
- Expected: Acceptance requirement for normal: client returns to the correct campaign state with no retained live mission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.030.case-02

Mission exit / retreat

- Entry/action: Campaign encounter / rendered battle mission; Perform mission exit specifically in the retreat context
- Preconditions: Isolated retreat context for mission exit
- Expected: Acceptance requirement for retreat: client returns to the correct campaign state with no retained live mission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.030.case-03

Mission exit / crash

- Entry/action: Campaign encounter / rendered battle mission; Perform mission exit specifically in the crash context
- Preconditions: Isolated crash context for mission exit
- Expected: Acceptance requirement for crash: client returns to the correct campaign state with no retained live mission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### battles.030.case-04

Mission exit / completed battle

- Entry/action: Campaign encounter / rendered battle mission; Perform mission exit specifically in the completed battle context
- Preconditions: Isolated completed battle context for mission exit
- Expected: Acceptance requirement for completed battle: client returns to the correct campaign state with no retained live mission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions/Battles](../../source/Missions/Battles), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)
