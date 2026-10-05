# locations behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## locations.001 Walk town center

Each entry retains authority and separate owner/observer observations in the CSV.

### locations.001.case-01

Walk town center / spawn

- Entry/action: Town/castle/village scene / conversation; Perform walk town center specifically in the spawn context
- Preconditions: Isolated spawn context for walk town center
- Expected: Acceptance requirement for spawn: client enters the intended town scene with correct actors
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.001.case-02

Walk town center / move

- Entry/action: Town/castle/village scene / conversation; Perform walk town center specifically in the move context
- Preconditions: Isolated move context for walk town center
- Expected: Acceptance requirement for move: client enters the intended town scene with correct actors
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.001.case-03

Walk town center / interact

- Entry/action: Town/castle/village scene / conversation; Perform walk town center specifically in the interact context
- Preconditions: Isolated interact context for walk town center
- Expected: Acceptance requirement for interact: client enters the intended town scene with correct actors
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.001.case-04

Walk town center / leave

- Entry/action: Town/castle/village scene / conversation; Perform walk town center specifically in the leave context
- Preconditions: Isolated leave context for walk town center
- Expected: Acceptance requirement for leave: client enters the intended town scene with correct actors
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## locations.002 Enter lord's hall

Each entry retains authority and separate owner/observer observations in the CSV.

### locations.002.case-01

Enter lord's hall / permission

- Entry/action: Town/castle/village scene / conversation; Perform enter lord's hall specifically in the permission context
- Preconditions: Isolated permission context for enter lord's hall
- Expected: Acceptance requirement for permission: scene access and authoritative consequences match accepted action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.002.case-02

Enter lord's hall / bribe

- Entry/action: Town/castle/village scene / conversation; Perform enter lord's hall specifically in the bribe context
- Preconditions: Isolated bribe context for enter lord's hall
- Expected: Acceptance requirement for bribe: scene access and authoritative consequences match accepted action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.002.case-03

Enter lord's hall / disguise

- Entry/action: Town/castle/village scene / conversation; Perform enter lord's hall specifically in the disguise context
- Preconditions: Isolated disguise context for enter lord's hall
- Expected: Acceptance requirement for disguise: scene access and authoritative consequences match accepted action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.002.case-04

Enter lord's hall / denied

- Entry/action: Town/castle/village scene / conversation; Perform enter lord's hall specifically in the denied context
- Preconditions: Isolated denied context for enter lord's hall
- Expected: Acceptance requirement for denied: scene access and authoritative consequences match accepted action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## locations.003 Enter tavern

Each entry retains authority and separate owner/observer observations in the CSV.

### locations.003.case-01

Enter tavern / town menu

- Entry/action: Town/castle/village scene / conversation; Perform enter tavern specifically in the town menu context
- Preconditions: Isolated town menu context for enter tavern
- Expected: Acceptance requirement for town menu: correct tavern NPCs and player identities appear
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.003.case-02

Enter tavern / scene transition

- Entry/action: Town/castle/village scene / conversation; Perform enter tavern specifically in the scene transition context
- Preconditions: Isolated scene transition context for enter tavern
- Expected: Acceptance requirement for scene transition: correct tavern NPCs and player identities appear
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.003.case-03

Enter tavern / return

- Entry/action: Town/castle/village scene / conversation; Perform enter tavern specifically in the return context
- Preconditions: Isolated return context for enter tavern
- Expected: Acceptance requirement for return: correct tavern NPCs and player identities appear
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## locations.004 Enter prison

Each entry retains authority and separate owner/observer observations in the CSV.

### locations.004.case-01

Enter prison / permission

- Entry/action: Town/castle/village scene / conversation; Perform enter prison specifically in the permission context
- Preconditions: Isolated permission context for enter prison
- Expected: Acceptance requirement for permission: correct prisoner characters are presented in the intended prison
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.004.case-02

Enter prison / ransom

- Entry/action: Town/castle/village scene / conversation; Perform enter prison specifically in the ransom context
- Preconditions: Isolated ransom context for enter prison
- Expected: Acceptance requirement for ransom: correct prisoner characters are presented in the intended prison
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.004.case-03

Enter prison / prison break context

- Entry/action: Town/castle/village scene / conversation; Perform enter prison specifically in the prison break context context
- Preconditions: Isolated prison break context context for enter prison
- Expected: Acceptance requirement for prison break context: correct prisoner characters are presented in the intended prison
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## locations.005 Walk village

Each entry retains authority and separate owner/observer observations in the CSV.

### locations.005.case-01

Walk village / normal village

- Entry/action: Town/castle/village scene / conversation; Perform walk village specifically in the normal village context
- Preconditions: Isolated normal village context for walk village
- Expected: Acceptance requirement for normal village: village scene and campaign return agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.005.case-02

Walk village / hostility

- Entry/action: Town/castle/village scene / conversation; Perform walk village specifically in the hostility context
- Preconditions: Isolated hostility context for walk village
- Expected: Acceptance requirement for hostility: village scene and campaign return agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.005.case-03

Walk village / scene return

- Entry/action: Town/castle/village scene / conversation; Perform walk village specifically in the scene return context
- Preconditions: Isolated scene return context for walk village
- Expected: Acceptance requirement for scene return: village scene and campaign return agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## locations.006 Talk to notable

Each entry retains authority and separate owner/observer observations in the CSV.

### locations.006.case-01

Talk to notable / issue giver

- Entry/action: Town/castle/village scene / conversation; Perform talk to notable specifically in the issue giver context
- Preconditions: Isolated issue giver context for talk to notable
- Expected: Acceptance requirement for issue giver: conversation uses the selected registered hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.006.case-02

Talk to notable / recruitment

- Entry/action: Town/castle/village scene / conversation; Perform talk to notable specifically in the recruitment context
- Preconditions: Isolated recruitment context for talk to notable
- Expected: Acceptance requirement for recruitment: conversation uses the selected registered hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.006.case-03

Talk to notable / caravan

- Entry/action: Town/castle/village scene / conversation; Perform talk to notable specifically in the caravan context
- Preconditions: Isolated caravan context for talk to notable
- Expected: Acceptance requirement for caravan: conversation uses the selected registered hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## locations.007 Talk to companion

Each entry retains authority and separate owner/observer observations in the CSV.

### locations.007.case-01

Talk to companion / hire

- Entry/action: Town/castle/village scene / conversation; Perform talk to companion specifically in the hire context
- Preconditions: Isolated hire context for talk to companion
- Expected: Acceptance requirement for hire: dialogue outcome affects only the intended player relationship
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.007.case-02

Talk to companion / dismiss

- Entry/action: Town/castle/village scene / conversation; Perform talk to companion specifically in the dismiss context
- Preconditions: Isolated dismiss context for talk to companion
- Expected: Acceptance requirement for dismiss: dialogue outcome affects only the intended player relationship
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.007.case-03

Talk to companion / party assignment

- Entry/action: Town/castle/village scene / conversation; Perform talk to companion specifically in the party assignment context
- Preconditions: Isolated party assignment context for talk to companion
- Expected: Acceptance requirement for party assignment: dialogue outcome affects only the intended player relationship
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## locations.008 Location character creation

Each entry retains authority and separate owner/observer observations in the CSV.

### locations.008.case-01

Location character creation / new character

- Entry/action: Town/castle/village scene / conversation; Perform location character creation specifically in the new character context
- Preconditions: Isolated new character context for location character creation
- Expected: Acceptance requirement for new character: the same registered character appears once in the location
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.008.case-02

Location character creation / visitor

- Entry/action: Town/castle/village scene / conversation; Perform location character creation specifically in the visitor context
- Preconditions: Isolated visitor context for location character creation
- Expected: Acceptance requirement for visitor: the same registered character appears once in the location
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.008.case-03

Location character creation / late entrant

- Entry/action: Town/castle/village scene / conversation; Perform location character creation specifically in the late entrant context
- Preconditions: Isolated late entrant context for location character creation
- Expected: Acceptance requirement for late entrant: the same registered character appears once in the location
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## locations.009 Location character removal

Each entry retains authority and separate owner/observer observations in the CSV.

### locations.009.case-01

Location character removal / leave

- Entry/action: Town/castle/village scene / conversation; Perform location character removal specifically in the leave context
- Preconditions: Isolated leave context for location character removal
- Expected: Acceptance requirement for leave: removed actor does not remain as a divergent scene copy
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.009.case-02

Location character removal / death

- Entry/action: Town/castle/village scene / conversation; Perform location character removal specifically in the death context
- Preconditions: Isolated death context for location character removal
- Expected: Acceptance requirement for death: removed actor does not remain as a divergent scene copy
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.009.case-03

Location character removal / party departed

- Entry/action: Town/castle/village scene / conversation; Perform location character removal specifically in the party departed context
- Preconditions: Isolated party departed context for location character removal
- Expected: Acceptance requirement for party departed: removed actor does not remain as a divergent scene copy
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## locations.010 Special location items

Each entry retains authority and separate owner/observer observations in the CSV.

### locations.010.case-01

Special location items / add

- Entry/action: Town/castle/village scene / conversation; Perform special location items specifically in the add context
- Preconditions: Isolated add context for special location items
- Expected: Acceptance requirement for add: location's item set agrees across peers without duplicates
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.010.case-02

Special location items / remove

- Entry/action: Town/castle/village scene / conversation; Perform special location items specifically in the remove context
- Preconditions: Isolated remove context for special location items
- Expected: Acceptance requirement for remove: location's item set agrees across peers without duplicates
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.010.case-03

Special location items / duplicate

- Entry/action: Town/castle/village scene / conversation; Perform special location items specifically in the duplicate context
- Preconditions: Isolated duplicate context for special location items
- Expected: Acceptance requirement for duplicate: location's item set agrees across peers without duplicates
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.010.case-04

Special location items / reload

- Entry/action: Town/castle/village scene / conversation; Perform special location items specifically in the reload context
- Preconditions: Isolated reload context for special location items
- Expected: Acceptance requirement for reload: location's item set agrees across peers without duplicates
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## locations.011 Scene transition

Each entry retains authority and separate owner/observer observations in the CSV.

### locations.011.case-01

Scene transition / tavern to center

- Entry/action: Town/castle/village scene / conversation; Perform scene transition specifically in the tavern to center context
- Preconditions: Isolated tavern to center context for scene transition
- Expected: Acceptance requirement for tavern to center: owned mission exits and next scene retains the intended campaign context
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.011.case-02

Scene transition / prison to center

- Entry/action: Town/castle/village scene / conversation; Perform scene transition specifically in the prison to center context
- Preconditions: Isolated prison to center context for scene transition
- Expected: Acceptance requirement for prison to center: owned mission exits and next scene retains the intended campaign context
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.011.case-03

Scene transition / campaign map

- Entry/action: Town/castle/village scene / conversation; Perform scene transition specifically in the campaign map context
- Preconditions: Isolated campaign map context for scene transition
- Expected: Acceptance requirement for campaign map: owned mission exits and next scene retains the intended campaign context
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## locations.012 Barber appearance change

Each entry retains authority and separate owner/observer observations in the CSV.

### locations.012.case-01

Barber appearance change / available actor

- Entry/action: Town/castle/village scene / conversation; Perform barber appearance change specifically in the available actor context
- Preconditions: Isolated available actor context for barber appearance change
- Expected: Acceptance requirement for available actor: accepted appearance changes reach both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.012.case-02

Barber appearance change / payment

- Entry/action: Town/castle/village scene / conversation; Perform barber appearance change specifically in the payment context
- Preconditions: Isolated payment context for barber appearance change
- Expected: Acceptance requirement for payment: accepted appearance changes reach both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.012.case-03

Barber appearance change / cancel

- Entry/action: Town/castle/village scene / conversation; Perform barber appearance change specifically in the cancel context
- Preconditions: Isolated cancel context for barber appearance change
- Expected: Acceptance requirement for cancel: accepted appearance changes reach both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

## locations.013 Board game interaction

Each entry retains authority and separate owner/observer observations in the CSV.

### locations.013.case-01

Board game interaction / start

- Entry/action: Town/castle/village scene / conversation; Perform board game interaction specifically in the start context
- Preconditions: Isolated start context for board game interaction
- Expected: Acceptance requirement for start: game state and reward agree where the current co-op path exists
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.013.case-02

Board game interaction / move

- Entry/action: Town/castle/village scene / conversation; Perform board game interaction specifically in the move context
- Preconditions: Isolated move context for board game interaction
- Expected: Acceptance requirement for move: game state and reward agree where the current co-op path exists
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.013.case-03.1

Board game interaction / win

- Entry/action: Town/castle/village scene / conversation; Perform board game interaction specifically in the win context
- Preconditions: Isolated win context for board game interaction
- Expected: Acceptance requirement for win: game state and reward agree where the current co-op path exists
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.013.case-03.2

Board game interaction / loss

- Entry/action: Town/castle/village scene / conversation; Perform board game interaction specifically in the loss context
- Preconditions: Isolated loss context for board game interaction
- Expected: Acceptance requirement for loss: game state and reward agree where the current co-op path exists
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)

### locations.013.case-04

Board game interaction / leave

- Entry/action: Town/castle/village scene / conversation; Perform board game interaction specifically in the leave context
- Preconditions: Isolated leave context for board game interaction
- Expected: Acceptance requirement for leave: game state and reward agree where the current co-op path exists
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Locations](../../source/GameInterface/Services/Locations), [source/GameInterface/Services/Characters](../../source/GameInterface/Services/Characters), [source/GameInterface/Services/Missions](../../source/GameInterface/Services/Missions)
