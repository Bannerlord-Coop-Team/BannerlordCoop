# sessions behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## sessions.001 Start authoritative server

Each entry retains authority and separate owner/observer observations in the CSV.

### sessions.001.case-01

Start authoritative server / server launch

- Entry/action: Coop menu / connection workflow; Perform start authoritative server specifically in the server launch context
- Preconditions: Isolated server launch context for start authoritative server
- Expected: Acceptance requirement for server launch: server campaign reaches readiness without consuming a client slot
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.001.case-02

Start authoritative server / no player hero/party on host

- Entry/action: Coop menu / connection workflow; Perform start authoritative server specifically in the no player hero/party on host context
- Preconditions: Isolated no player hero/party on host context for start authoritative server
- Expected: Acceptance requirement for no player hero/party on host: server campaign reaches readiness without consuming a client slot
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

## sessions.002 Join a server

Each entry retains authority and separate owner/observer observations in the CSV.

### sessions.002.case-01

Join a server / server address

- Entry/action: Coop menu / connection workflow; Perform join a server specifically in the server address context
- Preconditions: Isolated server address context for join a server
- Expected: Acceptance requirement for server address: client obtains its own registered hero and party
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.002.case-02

Join a server / Steam selection

- Entry/action: Coop menu / connection workflow; Perform join a server specifically in the Steam selection context
- Preconditions: Isolated Steam selection context for join a server
- Expected: Acceptance requirement for Steam selection: client obtains its own registered hero and party
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.002.case-03

Join a server / explicit join

- Entry/action: Coop menu / connection workflow; Perform join a server specifically in the explicit join context
- Preconditions: Isolated explicit join context for join a server
- Expected: Acceptance requirement for explicit join: client obtains its own registered hero and party
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

## sessions.003 Password admission

Each entry retains authority and separate owner/observer observations in the CSV.

### sessions.003.case-01

Password admission / correct

- Entry/action: Coop menu / connection workflow; Perform password admission specifically in the correct context
- Preconditions: Isolated correct context for password admission
- Expected: Acceptance requirement for correct: admission decision agrees with configured server password
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.003.case-02

Password admission / incorrect

- Entry/action: Coop menu / connection workflow; Perform password admission specifically in the incorrect context
- Preconditions: Isolated incorrect context for password admission
- Expected: Acceptance requirement for incorrect: admission decision agrees with configured server password
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.003.case-03

Password admission / changed during reconnect

- Entry/action: Coop menu / connection workflow; Perform password admission specifically in the changed during reconnect context
- Preconditions: Isolated changed during reconnect context for password admission
- Expected: Acceptance requirement for changed during reconnect: admission decision agrees with configured server password
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

## sessions.004 Module admission

Each entry retains authority and separate owner/observer observations in the CSV.

### sessions.004.case-01

Module admission / matching modules

- Entry/action: Coop menu / connection workflow; Perform module admission specifically in the matching modules context
- Preconditions: Isolated matching modules context for module admission
- Expected: Acceptance requirement for matching modules: mismatch is reported before incompatible campaign state is applied
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.004.case-02

Module admission / mismatched module set or version

- Entry/action: Coop menu / connection workflow; Perform module admission specifically in the mismatched module set or version context
- Preconditions: Isolated mismatched module set or version context for module admission
- Expected: Acceptance requirement for mismatched module set or version: mismatch is reported before incompatible campaign state is applied
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

## sessions.005 Save transfer

Each entry retains authority and separate owner/observer observations in the CSV.

### sessions.005.case-01

Save transfer / initial join

- Entry/action: Coop menu / connection workflow; Perform save transfer specifically in the initial join context
- Preconditions: Isolated initial join context for save transfer
- Expected: Acceptance requirement for initial join: client loads the server campaign identity before gameplay becomes ready
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.005.case-02

Save transfer / interrupted transfer

- Entry/action: Coop menu / connection workflow; Perform save transfer specifically in the interrupted transfer context
- Preconditions: Isolated interrupted transfer context for save transfer
- Expected: Acceptance requirement for interrupted transfer: client loads the server campaign identity before gameplay becomes ready
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.005.case-03

Save transfer / late join

- Entry/action: Coop menu / connection workflow; Perform save transfer specifically in the late join context
- Preconditions: Isolated late join context for save transfer
- Expected: Acceptance requirement for late join: client loads the server campaign identity before gameplay becomes ready
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

## sessions.006 Player identity

Each entry retains authority and separate owner/observer observations in the CSV.

### sessions.006.case-01

Player identity / new player

- Entry/action: Coop menu / connection workflow; Perform player identity specifically in the new player context
- Preconditions: Isolated new player context for player identity
- Expected: Acceptance requirement for new player: the same player resolves to the intended hero and party after rejoin
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.006.case-02

Player identity / existing saved registration

- Entry/action: Coop menu / connection workflow; Perform player identity specifically in the existing saved registration context
- Preconditions: Isolated existing saved registration context for player identity
- Expected: Acceptance requirement for existing saved registration: the same player resolves to the intended hero and party after rejoin
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

## sessions.007 Second client admission

Each entry retains authority and separate owner/observer observations in the CSV.

### sessions.007.case-01

Second client admission / two different platform identities

- Entry/action: Coop menu / connection workflow; Perform second client admission specifically in the two different platform identities context
- Preconditions: Isolated two different platform identities context for second client admission
- Expected: Acceptance requirement for two different platform identities: two clients are admitted with distinct player parties
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.007.case-02

Second client admission / repeated join

- Entry/action: Coop menu / connection workflow; Perform second client admission specifically in the repeated join context
- Preconditions: Isolated repeated join context for second client admission
- Expected: Acceptance requirement for repeated join: two clients are admitted with distinct player parties
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

## sessions.008 Disconnect player

Each entry retains authority and separate owner/observer observations in the CSV.

### sessions.008.case-01

Disconnect player / normal leave

- Entry/action: Coop menu / connection workflow; Perform disconnect player specifically in the normal leave context
- Preconditions: Isolated normal leave context for disconnect player
- Expected: Acceptance requirement for normal leave: remaining peers observe the intended player/party state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.008.case-02

Disconnect player / lost connection

- Entry/action: Coop menu / connection workflow; Perform disconnect player specifically in the lost connection context
- Preconditions: Isolated lost connection context for disconnect player
- Expected: Acceptance requirement for lost connection: remaining peers observe the intended player/party state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.008.case-03

Disconnect player / crash

- Entry/action: Coop menu / connection workflow; Perform disconnect player specifically in the crash context
- Preconditions: Isolated crash context for disconnect player
- Expected: Acceptance requirement for crash: remaining peers observe the intended player/party state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

## sessions.009 Reconnect player

Each entry retains authority and separate owner/observer observations in the CSV.

### sessions.009.case-01

Reconnect player / same campaign

- Entry/action: Coop menu / connection workflow; Perform reconnect player specifically in the same campaign context
- Preconditions: Isolated same campaign context for reconnect player
- Expected: Acceptance requirement for same campaign: rejoining client converges without duplicate registered world objects
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.009.case-02

Reconnect player / while other client continues

- Entry/action: Coop menu / connection workflow; Perform reconnect player specifically in the while other client continues context
- Preconditions: Isolated while other client continues context for reconnect player
- Expected: Acceptance requirement for while other client continues: rejoining client converges without duplicate registered world objects
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

## sessions.010 Join during campaign action

Each entry retains authority and separate owner/observer observations in the CSV.

### sessions.010.case-01

Join during campaign action / AI movement

- Entry/action: Coop menu / connection workflow; Perform join during campaign action specifically in the AI movement context
- Preconditions: Isolated AI movement context for join during campaign action
- Expected: Acceptance requirement for AI movement: initial state and subsequent updates have a consistent order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.010.case-02

Join during campaign action / settlement change

- Entry/action: Coop menu / connection workflow; Perform join during campaign action specifically in the settlement change context
- Preconditions: Isolated settlement change context for join during campaign action
- Expected: Acceptance requirement for settlement change: initial state and subsequent updates have a consistent order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.010.case-03

Join during campaign action / ongoing encounter

- Entry/action: Coop menu / connection workflow; Perform join during campaign action specifically in the ongoing encounter context
- Preconditions: Isolated ongoing encounter context for join during campaign action
- Expected: Acceptance requirement for ongoing encounter: initial state and subsequent updates have a consistent order
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

## sessions.011 Server shutdown

Each entry retains authority and separate owner/observer observations in the CSV.

### sessions.011.case-01

Server shutdown / idle

- Entry/action: Coop menu / connection workflow; Perform server shutdown specifically in the idle context
- Preconditions: Isolated idle context for server shutdown
- Expected: Acceptance requirement for idle: owned shutdown retains the expected save and terminates owned processes
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.011.case-02

Server shutdown / clients connected

- Entry/action: Coop menu / connection workflow; Perform server shutdown specifically in the clients connected context
- Preconditions: Isolated clients connected context for server shutdown
- Expected: Acceptance requirement for clients connected: owned shutdown retains the expected save and terminates owned processes
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.011.case-03

Server shutdown / pending save

- Entry/action: Coop menu / connection workflow; Perform server shutdown specifically in the pending save context
- Preconditions: Isolated pending save context for server shutdown
- Expected: Acceptance requirement for pending save: owned shutdown retains the expected save and terminates owned processes
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

## sessions.012 Connection error presentation

Each entry retains authority and separate owner/observer observations in the CSV.

### sessions.012.case-01

Connection error presentation / unreachable server

- Entry/action: Coop menu / connection workflow; Perform connection error presentation specifically in the unreachable server context
- Preconditions: Isolated unreachable server context for connection error presentation
- Expected: Acceptance requirement for unreachable server: visible reason agrees with the returned failure and readiness state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.012.case-02

Connection error presentation / rejected join

- Entry/action: Coop menu / connection workflow; Perform connection error presentation specifically in the rejected join context
- Preconditions: Isolated rejected join context for connection error presentation
- Expected: Acceptance requirement for rejected join: visible reason agrees with the returned failure and readiness state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)

### sessions.012.case-03

Connection error presentation / partial startup

- Entry/action: Coop menu / connection workflow; Perform connection error presentation specifically in the partial startup context
- Preconditions: Isolated partial startup context for connection error presentation
- Expected: Acceptance requirement for partial startup: visible reason agrees with the returned failure and readiness state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/Coop.Core/Client](../../source/Coop.Core/Client), [source/Coop.Core/Server](../../source/Coop.Core/Server), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs)
