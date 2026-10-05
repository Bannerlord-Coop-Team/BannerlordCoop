# buildings behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## buildings.001 Select construction project

Each entry retains authority and separate owner/observer observations in the CSV.

### buildings.001.case-01

Select construction project / available building

- Entry/action: Town/castle construction interface; Perform select construction project specifically in the available building context
- Preconditions: Isolated available building context for select construction project
- Expected: Acceptance requirement for available building: selected queue entry names the intended building
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.001.case-02

Select construction project / unavailable project

- Entry/action: Town/castle construction interface; Perform select construction project specifically in the unavailable project context
- Preconditions: Isolated unavailable project context for select construction project
- Expected: Acceptance requirement for unavailable project: selected queue entry names the intended building
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

## buildings.002 Change construction queue

Each entry retains authority and separate owner/observer observations in the CSV.

### buildings.002.case-01

Change construction queue / append

- Entry/action: Town/castle construction interface; Perform change construction queue specifically in the append context
- Preconditions: Isolated append context for change construction queue
- Expected: Acceptance requirement for append: queue order matches the accepted client action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.002.case-02

Change construction queue / reorder

- Entry/action: Town/castle construction interface; Perform change construction queue specifically in the reorder context
- Preconditions: Isolated reorder context for change construction queue
- Expected: Acceptance requirement for reorder: queue order matches the accepted client action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.002.case-03

Change construction queue / remove

- Entry/action: Town/castle construction interface; Perform change construction queue specifically in the remove context
- Preconditions: Isolated remove context for change construction queue
- Expected: Acceptance requirement for remove: queue order matches the accepted client action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

## buildings.003 Change daily default project

Each entry retains authority and separate owner/observer observations in the CSV.

### buildings.003.case-01

Change daily default project / housing

- Entry/action: Town/castle construction interface; Perform change daily default project specifically in the housing context
- Preconditions: Isolated housing context for change daily default project
- Expected: Acceptance requirement for housing: selected daily project and observed effect match the installed options
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.003.case-02

Change daily default project / irrigation

- Entry/action: Town/castle construction interface; Perform change daily default project specifically in the irrigation context
- Preconditions: Isolated irrigation context for change daily default project
- Expected: Acceptance requirement for irrigation: selected daily project and observed effect match the installed options
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.003.case-03

Change daily default project / militia

- Entry/action: Town/castle construction interface; Perform change daily default project specifically in the militia context
- Preconditions: Isolated militia context for change daily default project
- Expected: Acceptance requirement for militia: selected daily project and observed effect match the installed options
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.003.case-04

Change daily default project / festival where available

- Entry/action: Town/castle construction interface; Perform change daily default project specifically in the festival where available context
- Preconditions: Isolated festival where available context for change daily default project
- Expected: Acceptance requirement for festival where available: selected daily project and observed effect match the installed options
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

## buildings.004 Fund construction

Each entry retains authority and separate owner/observer observations in the CSV.

### buildings.004.case-01

Fund construction / deposit

- Entry/action: Town/castle construction interface; Perform fund construction specifically in the deposit context
- Preconditions: Isolated deposit context for fund construction
- Expected: Acceptance requirement for deposit: reserve and player gold balance
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.004.case-02

Fund construction / withdraw

- Entry/action: Town/castle construction interface; Perform fund construction specifically in the withdraw context
- Preconditions: Isolated withdraw context for fund construction
- Expected: Acceptance requirement for withdraw: reserve and player gold balance
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.004.case-03

Fund construction / insufficient gold

- Entry/action: Town/castle construction interface; Perform fund construction specifically in the insufficient gold context
- Preconditions: Isolated insufficient gold context for fund construction
- Expected: Acceptance requirement for insufficient gold: reserve and player gold balance
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

## buildings.005 Construction progress

Each entry retains authority and separate owner/observer observations in the CSV.

### buildings.005.case-01

Construction progress / daily tick

- Entry/action: Town/castle construction interface; Perform construction progress specifically in the daily tick context
- Preconditions: Isolated daily tick context for construction progress
- Expected: Acceptance requirement for daily tick: progress agrees across peers and completion occurs once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.005.case-02

Construction progress / boosted

- Entry/action: Town/castle construction interface; Perform construction progress specifically in the boosted context
- Preconditions: Isolated boosted context for construction progress
- Expected: Acceptance requirement for boosted: progress agrees across peers and completion occurs once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.005.case-03

Construction progress / paused by low loyalty

- Entry/action: Town/castle construction interface; Perform construction progress specifically in the paused by low loyalty context
- Preconditions: Isolated paused by low loyalty context for construction progress
- Expected: Acceptance requirement for paused by low loyalty: progress agrees across peers and completion occurs once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

## buildings.006 Building completion

Each entry retains authority and separate owner/observer observations in the CSV.

### buildings.006.case-01

Building completion / level upgrade

- Entry/action: Town/castle construction interface; Perform building completion specifically in the level upgrade context
- Preconditions: Isolated level upgrade context for building completion
- Expected: Acceptance requirement for level upgrade: new building level and next active project converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.006.case-02

Building completion / queue advances

- Entry/action: Town/castle construction interface; Perform building completion specifically in the queue advances context
- Preconditions: Isolated queue advances context for building completion
- Expected: Acceptance requirement for queue advances: new building level and next active project converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

## buildings.007 Building effects

Each entry retains authority and separate owner/observer observations in the CSV.

### buildings.007.case-01

Building effects / food

- Entry/action: Town/castle construction interface; Perform building effects specifically in the food context
- Preconditions: Isolated food context for building effects
- Expected: Acceptance requirement for food: measure the selected building's independent expected settlement effect
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.007.case-02

Building effects / security

- Entry/action: Town/castle construction interface; Perform building effects specifically in the security context
- Preconditions: Isolated security context for building effects
- Expected: Acceptance requirement for security: measure the selected building's independent expected settlement effect
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.007.case-03

Building effects / prosperity

- Entry/action: Town/castle construction interface; Perform building effects specifically in the prosperity context
- Preconditions: Isolated prosperity context for building effects
- Expected: Acceptance requirement for prosperity: measure the selected building's independent expected settlement effect
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.007.case-04

Building effects / militia

- Entry/action: Town/castle construction interface; Perform building effects specifically in the militia context
- Preconditions: Isolated militia context for building effects
- Expected: Acceptance requirement for militia: measure the selected building's independent expected settlement effect
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)

### buildings.007.case-05

Building effects / siege defenses

- Entry/action: Town/castle construction interface; Perform building effects specifically in the siege defenses context
- Preconditions: Isolated siege defenses context for building effects
- Expected: Acceptance requirement for siege defenses: measure the selected building's independent expected settlement effect
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Buildings](../../source/GameInterface/Services/Buildings), [source/GameInterface/Services/Towns](../../source/GameInterface/Services/Towns)
