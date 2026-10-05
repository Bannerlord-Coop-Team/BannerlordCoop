# sieges behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## sieges.001 Begin siege

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.001.case-01

Begin siege / town

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform begin siege specifically in the town context
- Preconditions: Isolated town context for begin siege
- Expected: Acceptance requirement for town: one registered siege event links correct besieger and settlement
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.001.case-02

Begin siege / castle

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform begin siege specifically in the castle context
- Preconditions: Isolated castle context for begin siege
- Expected: Acceptance requirement for castle: one registered siege event links correct besieger and settlement
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.001.case-03

Begin siege / army leader

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform begin siege specifically in the army leader context
- Preconditions: Isolated army leader context for begin siege
- Expected: Acceptance requirement for army leader: one registered siege event links correct besieger and settlement
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.001.case-04

Begin siege / lone party

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform begin siege specifically in the lone party context
- Preconditions: Isolated lone party context for begin siege
- Expected: Acceptance requirement for lone party: one registered siege event links correct besieger and settlement
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.002 Join besieger camp

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.002.case-01

Join besieger camp / leader

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform join besieger camp specifically in the leader context
- Preconditions: Isolated leader context for join besieger camp
- Expected: Acceptance requirement for leader: party joins the correct siege side and observes shared progress
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.002.case-02

Join besieger camp / army member

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform join besieger camp specifically in the army member context
- Preconditions: Isolated army member context for join besieger camp
- Expected: Acceptance requirement for army member: party joins the correct siege side and observes shared progress
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.002.case-03

Join besieger camp / second player

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform join besieger camp specifically in the second player context
- Preconditions: Isolated second player context for join besieger camp
- Expected: Acceptance requirement for second player: party joins the correct siege side and observes shared progress
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.003 Leave siege

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.003.case-01

Leave siege / leader departure

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform leave siege specifically in the leader departure context
- Preconditions: Isolated leader departure context for leave siege
- Expected: Acceptance requirement for leader departure: siege persists or ends according to remaining authoritative participants
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.003.case-02

Leave siege / nonleader departure

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform leave siege specifically in the nonleader departure context
- Preconditions: Isolated nonleader departure context for leave siege
- Expected: Acceptance requirement for nonleader departure: siege persists or ends according to remaining authoritative participants
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.003.case-03

Leave siege / final besieger

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform leave siege specifically in the final besieger context
- Preconditions: Isolated final besieger context for leave siege
- Expected: Acceptance requirement for final besieger: siege persists or ends according to remaining authoritative participants
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.004 Build siege camp

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.004.case-01

Build siege camp / progress

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform build siege camp specifically in the progress context
- Preconditions: Isolated progress context for build siege camp
- Expected: Acceptance requirement for progress: camp readiness agrees across peers
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.004.case-02

Build siege camp / interruption

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform build siege camp specifically in the interruption context
- Preconditions: Isolated interruption context for build siege camp
- Expected: Acceptance requirement for interruption: camp readiness agrees across peers
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.004.case-03

Build siege camp / settlement relieved

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform build siege camp specifically in the settlement relieved context
- Preconditions: Isolated settlement relieved context for build siege camp
- Expected: Acceptance requirement for settlement relieved: camp readiness agrees across peers
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.005 Queue siege engine

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.005.case-01

Queue siege engine / ram

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform queue siege engine specifically in the ram context
- Preconditions: Isolated ram context for queue siege engine
- Expected: Acceptance requirement for ram: selected valid engine enters the correct construction slot
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.005.case-02

Queue siege engine / tower

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform queue siege engine specifically in the tower context
- Preconditions: Isolated tower context for queue siege engine
- Expected: Acceptance requirement for tower: selected valid engine enters the correct construction slot
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.005.case-03

Queue siege engine / catapult

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform queue siege engine specifically in the catapult context
- Preconditions: Isolated catapult context for queue siege engine
- Expected: Acceptance requirement for catapult: selected valid engine enters the correct construction slot
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.005.case-04

Queue siege engine / ballista

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform queue siege engine specifically in the ballista context
- Preconditions: Isolated ballista context for queue siege engine
- Expected: Acceptance requirement for ballista: selected valid engine enters the correct construction slot
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.005.case-05

Queue siege engine / trebuchet where available

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform queue siege engine specifically in the trebuchet where available context
- Preconditions: Isolated trebuchet where available context for queue siege engine
- Expected: Acceptance requirement for trebuchet where available: selected valid engine enters the correct construction slot
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.006 Construct siege engine

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.006.case-01

Construct siege engine / progress

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform construct siege engine specifically in the progress context
- Preconditions: Isolated progress context for construct siege engine
- Expected: Acceptance requirement for progress: engine identity and progress agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.006.case-02

Construct siege engine / completion

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform construct siege engine specifically in the completion context
- Preconditions: Isolated completion context for construct siege engine
- Expected: Acceptance requirement for completion: engine identity and progress agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.006.case-03

Construct siege engine / destroyed during build

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform construct siege engine specifically in the destroyed during build context
- Preconditions: Isolated destroyed during build context for construct siege engine
- Expected: Acceptance requirement for destroyed during build: engine identity and progress agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.007 Move engine to reserve

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.007.case-01

Move engine to reserve / active slot

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform move engine to reserve specifically in the active slot context
- Preconditions: Isolated active slot context for move engine to reserve
- Expected: Acceptance requirement for active slot: one engine occupies the intended slot/reserve
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.007.case-02

Move engine to reserve / reserved slot

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform move engine to reserve specifically in the reserved slot context
- Preconditions: Isolated reserved slot context for move engine to reserve
- Expected: Acceptance requirement for reserved slot: one engine occupies the intended slot/reserve
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.007.case-03

Move engine to reserve / replacement

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform move engine to reserve specifically in the replacement context
- Preconditions: Isolated replacement context for move engine to reserve
- Expected: Acceptance requirement for replacement: one engine occupies the intended slot/reserve
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.008 Siege bombardment

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.008.case-01

Siege bombardment / engine hit

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform siege bombardment specifically in the engine hit context
- Preconditions: Isolated engine hit context for siege bombardment
- Expected: Acceptance requirement for engine hit: health and construction/destruction effects converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.008.case-02

Siege bombardment / wall damage

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform siege bombardment specifically in the wall damage context
- Preconditions: Isolated wall damage context for siege bombardment
- Expected: Acceptance requirement for wall damage: health and construction/destruction effects converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.008.case-03

Siege bombardment / engine destroyed

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform siege bombardment specifically in the engine destroyed context
- Preconditions: Isolated engine destroyed context for siege bombardment
- Expected: Acceptance requirement for engine destroyed: health and construction/destruction effects converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.009 Wall breach

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.009.case-01

Wall breach / unbreached

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform wall breach specifically in the unbreached context
- Preconditions: Isolated unbreached context for wall breach
- Expected: Acceptance requirement for unbreached: mission defenses reflect authoritative wall state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.009.case-02

Wall breach / one breach

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform wall breach specifically in the one breach context
- Preconditions: Isolated one breach context for wall breach
- Expected: Acceptance requirement for one breach: mission defenses reflect authoritative wall state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.009.case-03

Wall breach / multiple breaches

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform wall breach specifically in the multiple breaches context
- Preconditions: Isolated multiple breaches context for wall breach
- Expected: Acceptance requirement for multiple breaches: mission defenses reflect authoritative wall state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.010 Siege supply shortage

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.010.case-01

Siege supply shortage / food exhausted

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform siege supply shortage specifically in the food exhausted context
- Preconditions: Isolated food exhausted context for siege supply shortage
- Expected: Acceptance requirement for food exhausted: food and troop attrition agree without double daily losses
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.010.case-02

Siege supply shortage / garrison starvation

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform siege supply shortage specifically in the garrison starvation context
- Preconditions: Isolated garrison starvation context for siege supply shortage
- Expected: Acceptance requirement for garrison starvation: food and troop attrition agree without double daily losses
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.010.case-03

Siege supply shortage / militia

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform siege supply shortage specifically in the militia context
- Preconditions: Isolated militia context for siege supply shortage
- Expected: Acceptance requirement for militia: food and troop attrition agree without double daily losses
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.011 Lead siege assault

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.011.case-01

Lead siege assault / ready

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform lead siege assault specifically in the ready context
- Preconditions: Isolated ready context for lead siege assault
- Expected: Acceptance requirement for ready: all participating clients enter the correct assault map event
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.011.case-02

Lead siege assault / not ready

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform lead siege assault specifically in the not ready context
- Preconditions: Isolated not ready context for lead siege assault
- Expected: Acceptance requirement for not ready: all participating clients enter the correct assault map event
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.011.case-03

Lead siege assault / attacking army

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform lead siege assault specifically in the attacking army context
- Preconditions: Isolated attacking army context for lead siege assault
- Expected: Acceptance requirement for attacking army: all participating clients enter the correct assault map event
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.012 Defend siege

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.012.case-01

Defend siege / garrison

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform defend siege specifically in the garrison context
- Preconditions: Isolated garrison context for defend siege
- Expected: Acceptance requirement for garrison: defender roster and battle side contain the intended participants
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.012.case-02

Defend siege / militia

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform defend siege specifically in the militia context
- Preconditions: Isolated militia context for defend siege
- Expected: Acceptance requirement for militia: defender roster and battle side contain the intended participants
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.012.case-03

Defend siege / player party

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform defend siege specifically in the player party context
- Preconditions: Isolated player party context for defend siege
- Expected: Acceptance requirement for player party: defender roster and battle side contain the intended participants
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.012.case-04

Defend siege / relief army

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform defend siege specifically in the relief army context
- Preconditions: Isolated relief army context for defend siege
- Expected: Acceptance requirement for relief army: defender roster and battle side contain the intended participants
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.013 Sally out

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.013.case-01

Sally out / eligible defenders

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform sally out specifically in the eligible defenders context
- Preconditions: Isolated eligible defenders context for sally out
- Expected: Acceptance requirement for eligible defenders: accepted sally creates the correct encounter and returns state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.013.case-02

Sally out / blocked action

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform sally out specifically in the blocked action context
- Preconditions: Isolated blocked action context for sally out
- Expected: Acceptance requirement for blocked action: accepted sally creates the correct encounter and returns state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.014 Siege ambush

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.014.case-01

Siege ambush / launch

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform siege ambush specifically in the launch context
- Preconditions: Isolated launch context for siege ambush
- Expected: Acceptance requirement for launch: engine losses and returned party state agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.014.case-02

Siege ambush / destroy engines

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform siege ambush specifically in the destroy engines context
- Preconditions: Isolated destroy engines context for siege ambush
- Expected: Acceptance requirement for destroy engines: engine losses and returned party state agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.014.case-03

Siege ambush / retreat

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform siege ambush specifically in the retreat context
- Preconditions: Isolated retreat context for siege ambush
- Expected: Acceptance requirement for retreat: engine losses and returned party state agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.014.case-04

Siege ambush / completion

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform siege ambush specifically in the completion context
- Preconditions: Isolated completion context for siege ambush
- Expected: Acceptance requirement for completion: engine losses and returned party state agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.015 Relieve siege

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.015.case-01

Relieve siege / attack besiegers

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform relieve siege specifically in the attack besiegers context
- Preconditions: Isolated attack besiegers context for relieve siege
- Expected: Acceptance requirement for attack besiegers: siege lifecycle and army attachments converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.015.case-02

Relieve siege / join defense

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform relieve siege specifically in the join defense context
- Preconditions: Isolated join defense context for relieve siege
- Expected: Acceptance requirement for join defense: siege lifecycle and army attachments converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.015.case-03

Relieve siege / besieger defeat

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform relieve siege specifically in the besieger defeat context
- Preconditions: Isolated besieger defeat context for relieve siege
- Expected: Acceptance requirement for besieger defeat: siege lifecycle and army attachments converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

## sieges.016 Capture settlement aftermath

Each entry retains authority and separate owner/observer observations in the CSV.

### sieges.016.case-01

Capture settlement aftermath / show mercy

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform capture settlement aftermath specifically in the show mercy context
- Preconditions: Isolated show mercy context for capture settlement aftermath
- Expected: Acceptance requirement for show mercy: accepted choice produces the intended ownership/roster/relations state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.016.case-02

Capture settlement aftermath / pillage

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform capture settlement aftermath specifically in the pillage context
- Preconditions: Isolated pillage context for capture settlement aftermath
- Expected: Acceptance requirement for pillage: accepted choice produces the intended ownership/roster/relations state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)

### sieges.016.case-03

Capture settlement aftermath / devastate

- Entry/action: Siege camp / settlement siege menu / assault mission; Perform capture settlement aftermath specifically in the devastate context
- Preconditions: Isolated devastate context for capture settlement aftermath
- Expected: Acceptance requirement for devastate: accepted choice produces the intended ownership/roster/relations state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/SiegeEvents](../../source/GameInterface/Services/SiegeEvents), [source/GameInterface/Services/SiegeEngines](../../source/GameInterface/Services/SiegeEngines), [source/GameInterface/Services/BesiegerCamps](../../source/GameInterface/Services/BesiegerCamps), [source/GameInterface/Services/MapEvents](../../source/GameInterface/Services/MapEvents), [source/Missions](../../source/Missions)
