# tournaments behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## tournaments.001 Generate tournament

Each entry retains authority and separate owner/observer observations in the CSV.

### tournaments.001.case-01

Generate tournament / town availability

- Entry/action: Town arena / co-op tournament lobby; Perform generate tournament specifically in the town availability context
- Preconditions: Isolated town availability context for generate tournament
- Expected: Acceptance requirement for town availability: one town tournament becomes available without duplicate creation
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.001.case-02

Generate tournament / existing tournament

- Entry/action: Town arena / co-op tournament lobby; Perform generate tournament specifically in the existing tournament context
- Preconditions: Isolated existing tournament context for generate tournament
- Expected: Acceptance requirement for existing tournament: one town tournament becomes available without duplicate creation
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

## tournaments.002 Join tournament lobby

Each entry retains authority and separate owner/observer observations in the CSV.

### tournaments.002.case-01

Join tournament lobby / first client

- Entry/action: Town arena / co-op tournament lobby; Perform join tournament lobby specifically in the first client context
- Preconditions: Isolated first client context for join tournament lobby
- Expected: Acceptance requirement for first client: both clients reference one server session with distinct participants
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.002.case-02

Join tournament lobby / second client

- Entry/action: Town arena / co-op tournament lobby; Perform join tournament lobby specifically in the second client context
- Preconditions: Isolated second client context for join tournament lobby
- Expected: Acceptance requirement for second client: both clients reference one server session with distinct participants
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.002.case-03

Join tournament lobby / repeated join

- Entry/action: Town arena / co-op tournament lobby; Perform join tournament lobby specifically in the repeated join context
- Preconditions: Isolated repeated join context for join tournament lobby
- Expected: Acceptance requirement for repeated join: both clients reference one server session with distinct participants
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

## tournaments.003 Start tournament

Each entry retains authority and separate owner/observer observations in the CSV.

### tournaments.003.case-01

Start tournament / controller

- Entry/action: Town arena / co-op tournament lobby; Perform start tournament specifically in the controller context
- Preconditions: Isolated controller context for start tournament
- Expected: Acceptance requirement for controller: only a valid current start advances the session once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.003.case-02

Start tournament / noncontroller

- Entry/action: Town arena / co-op tournament lobby; Perform start tournament specifically in the noncontroller context
- Preconditions: Isolated noncontroller context for start tournament
- Expected: Acceptance requirement for noncontroller: only a valid current start advances the session once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.003.case-03

Start tournament / lobby not ready

- Entry/action: Town arena / co-op tournament lobby; Perform start tournament specifically in the lobby not ready context
- Preconditions: Isolated lobby not ready context for start tournament
- Expected: Acceptance requirement for lobby not ready: only a valid current start advances the session once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

## tournaments.004 Choose participate

Each entry retains authority and separate owner/observer observations in the CSV.

### tournaments.004.case-01

Choose participate / current match

- Entry/action: Town arena / co-op tournament lobby; Perform choose participate specifically in the current match context
- Preconditions: Isolated current match context for choose participate
- Expected: Acceptance requirement for current match: current participant choice is accepted once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.004.case-02

Choose participate / stale match

- Entry/action: Town arena / co-op tournament lobby; Perform choose participate specifically in the stale match context
- Preconditions: Isolated stale match context for choose participate
- Expected: Acceptance requirement for stale match: current participant choice is accepted once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.004.case-03

Choose participate / repeated choice

- Entry/action: Town arena / co-op tournament lobby; Perform choose participate specifically in the repeated choice context
- Preconditions: Isolated repeated choice context for choose participate
- Expected: Acceptance requirement for repeated choice: current participant choice is accepted once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

## tournaments.005 Choose spectate

Each entry retains authority and separate owner/observer observations in the CSV.

### tournaments.005.case-01

Choose spectate / eligible observer

- Entry/action: Town arena / co-op tournament lobby; Perform choose spectate specifically in the eligible observer context
- Preconditions: Isolated eligible observer context for choose spectate
- Expected: Acceptance requirement for eligible observer: client enters the observer path for the current match
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.005.case-02

Choose spectate / match underway

- Entry/action: Town arena / co-op tournament lobby; Perform choose spectate specifically in the match underway context
- Preconditions: Isolated match underway context for choose spectate
- Expected: Acceptance requirement for match underway: client enters the observer path for the current match
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

## tournaments.006 Tournament betting

Each entry retains authority and separate owner/observer observations in the CSV.

### tournaments.006.case-01

Tournament betting / valid amount

- Entry/action: Town arena / co-op tournament lobby; Perform tournament betting specifically in the valid amount context
- Preconditions: Isolated valid amount context for tournament betting
- Expected: Acceptance requirement for valid amount: accepted bet consumes funds once and uses the current round
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.006.case-02

Tournament betting / insufficient funds

- Entry/action: Town arena / co-op tournament lobby; Perform tournament betting specifically in the insufficient funds context
- Preconditions: Isolated insufficient funds context for tournament betting
- Expected: Acceptance requirement for insufficient funds: accepted bet consumes funds once and uses the current round
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.006.case-03

Tournament betting / repeated bet

- Entry/action: Town arena / co-op tournament lobby; Perform tournament betting specifically in the repeated bet context
- Preconditions: Isolated repeated bet context for tournament betting
- Expected: Acceptance requirement for repeated bet: accepted bet consumes funds once and uses the current round
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

## tournaments.007 Tournament match combat

Each entry retains authority and separate owner/observer observations in the CSV.

### tournaments.007.case-01

Tournament match combat / team identity

- Entry/action: Town arena / co-op tournament lobby; Perform tournament match combat specifically in the team identity context
- Preconditions: Isolated team identity context for tournament match combat
- Expected: Acceptance requirement for team identity: both clients observe the same round participants and outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.007.case-02

Tournament match combat / mount

- Entry/action: Town arena / co-op tournament lobby; Perform tournament match combat specifically in the mount context
- Preconditions: Isolated mount context for tournament match combat
- Expected: Acceptance requirement for mount: both clients observe the same round participants and outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.007.case-03

Tournament match combat / casualties

- Entry/action: Town arena / co-op tournament lobby; Perform tournament match combat specifically in the casualties context
- Preconditions: Isolated casualties context for tournament match combat
- Expected: Acceptance requirement for casualties: both clients observe the same round participants and outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.007.case-04

Tournament match combat / victory

- Entry/action: Town arena / co-op tournament lobby; Perform tournament match combat specifically in the victory context
- Preconditions: Isolated victory context for tournament match combat
- Expected: Acceptance requirement for victory: both clients observe the same round participants and outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

## tournaments.008 Tournament progression

Each entry retains authority and separate owner/observer observations in the CSV.

### tournaments.008.case-01

Tournament progression / round win

- Entry/action: Town arena / co-op tournament lobby; Perform tournament progression specifically in the round win context
- Preconditions: Isolated round win context for tournament progression
- Expected: Acceptance requirement for round win: bracket and phase advance once from the authoritative outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.008.case-02

Tournament progression / loss

- Entry/action: Town arena / co-op tournament lobby; Perform tournament progression specifically in the loss context
- Preconditions: Isolated loss context for tournament progression
- Expected: Acceptance requirement for loss: bracket and phase advance once from the authoritative outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.008.case-03

Tournament progression / final round

- Entry/action: Town arena / co-op tournament lobby; Perform tournament progression specifically in the final round context
- Preconditions: Isolated final round context for tournament progression
- Expected: Acceptance requirement for final round: bracket and phase advance once from the authoritative outcome
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

## tournaments.009 Tournament reward

Each entry retains authority and separate owner/observer observations in the CSV.

### tournaments.009.case-01

Tournament reward / prize item

- Entry/action: Town arena / co-op tournament lobby; Perform tournament reward specifically in the prize item context
- Preconditions: Isolated prize item context for tournament reward
- Expected: Acceptance requirement for prize item: reward reaches the actual winning player's party/clan
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.009.case-02

Tournament reward / gold

- Entry/action: Town arena / co-op tournament lobby; Perform tournament reward specifically in the gold context
- Preconditions: Isolated gold context for tournament reward
- Expected: Acceptance requirement for gold: reward reaches the actual winning player's party/clan
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.009.case-03

Tournament reward / renown

- Entry/action: Town arena / co-op tournament lobby; Perform tournament reward specifically in the renown context
- Preconditions: Isolated renown context for tournament reward
- Expected: Acceptance requirement for renown: reward reaches the actual winning player's party/clan
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.009.case-04

Tournament reward / winner identity

- Entry/action: Town arena / co-op tournament lobby; Perform tournament reward specifically in the winner identity context
- Preconditions: Isolated winner identity context for tournament reward
- Expected: Acceptance requirement for winner identity: reward reaches the actual winning player's party/clan
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

## tournaments.010 Leave tournament

Each entry retains authority and separate owner/observer observations in the CSV.

### tournaments.010.case-01

Leave tournament / preparation

- Entry/action: Town arena / co-op tournament lobby; Perform leave tournament specifically in the preparation context
- Preconditions: Isolated preparation context for leave tournament
- Expected: Acceptance requirement for preparation: session membership and remaining client state remain consistent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.010.case-02

Leave tournament / match

- Entry/action: Town arena / co-op tournament lobby; Perform leave tournament specifically in the match context
- Preconditions: Isolated match context for leave tournament
- Expected: Acceptance requirement for match: session membership and remaining client state remain consistent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.010.case-03

Leave tournament / completed

- Entry/action: Town arena / co-op tournament lobby; Perform leave tournament specifically in the completed context
- Preconditions: Isolated completed context for leave tournament
- Expected: Acceptance requirement for completed: session membership and remaining client state remain consistent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.010.case-04

Leave tournament / disconnect

- Entry/action: Town arena / co-op tournament lobby; Perform leave tournament specifically in the disconnect context
- Preconditions: Isolated disconnect context for leave tournament
- Expected: Acceptance requirement for disconnect: session membership and remaining client state remain consistent
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

## tournaments.011 Tournament spectator disconnect

Each entry retains authority and separate owner/observer observations in the CSV.

### tournaments.011.case-01.1

Tournament spectator disconnect / pacer departure

- Entry/action: Town arena / co-op tournament lobby; Perform tournament spectator disconnect specifically in the pacer departure context
- Preconditions: Isolated pacer departure context for tournament spectator disconnect
- Expected: Acceptance requirement for pacer departure: remaining players do not become stranded in a completed/stale mission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.011.case-01.2

Tournament spectator disconnect / observer departure

- Entry/action: Town arena / co-op tournament lobby; Perform tournament spectator disconnect specifically in the observer departure context
- Preconditions: Isolated observer departure context for tournament spectator disconnect
- Expected: Acceptance requirement for observer departure: remaining players do not become stranded in a completed/stale mission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.011.case-02

Tournament spectator disconnect / remaining players

- Entry/action: Town arena / co-op tournament lobby; Perform tournament spectator disconnect specifically in the remaining players context
- Preconditions: Isolated remaining players context for tournament spectator disconnect
- Expected: Acceptance requirement for remaining players: remaining players do not become stranded in a completed/stale mission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

## tournaments.012 Tournament fixture restoration

Each entry retains authority and separate owner/observer observations in the CSV.

### tournaments.012.case-01

Tournament fixture restoration / normal completion

- Entry/action: Town arena / co-op tournament lobby; Perform tournament fixture restoration specifically in the normal completion context
- Preconditions: Isolated normal completion context for tournament fixture restoration
- Expected: Acceptance requirement for normal completion: owned fixture restores its captured baseline only after the session ends
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.012.case-02

Tournament fixture restoration / abort

- Entry/action: Town arena / co-op tournament lobby; Perform tournament fixture restoration specifically in the abort context
- Preconditions: Isolated abort context for tournament fixture restoration
- Expected: Acceptance requirement for abort: owned fixture restores its captured baseline only after the session ends
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)

### tournaments.012.case-03

Tournament fixture restoration / failed setup

- Entry/action: Town arena / co-op tournament lobby; Perform tournament fixture restoration specifically in the failed setup context
- Preconditions: Isolated failed setup context for tournament fixture restoration
- Expected: Acceptance requirement for failed setup: owned fixture restores its captured baseline only after the session ends
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Tournaments](../../source/GameInterface/Services/Tournaments), [source/Missions](../../source/Missions), [source/E2E.Tests](../../source/E2E.Tests)
