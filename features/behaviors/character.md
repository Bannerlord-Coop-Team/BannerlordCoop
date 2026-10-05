# character behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## character.001 Create player character

Each entry retains authority and separate owner/observer observations in the CSV.

### character.001.case-01

Create player character / first admission

- Entry/action: Client character creation / character screen; Perform create player character specifically in the first admission context
- Preconditions: Isolated first admission context for create player character
- Expected: Acceptance requirement for first admission: new character belongs to exactly one admitted player
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.001.case-02

Create player character / returning player

- Entry/action: Client character creation / character screen; Perform create player character specifically in the returning player context
- Preconditions: Isolated returning player context for create player character
- Expected: Acceptance requirement for returning player: new character belongs to exactly one admitted player
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

## character.002 Select culture

Each entry retains authority and separate owner/observer observations in the CSV.

### character.002.case-01

Select culture / available cultures

- Entry/action: Client character creation / character screen; Perform select culture specifically in the available cultures context
- Preconditions: Isolated available cultures context for select culture
- Expected: Acceptance requirement for available cultures: chosen culture survives replication and reload
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.002.case-02

Select culture / saved culture

- Entry/action: Client character creation / character screen; Perform select culture specifically in the saved culture context
- Preconditions: Isolated saved culture context for select culture
- Expected: Acceptance requirement for saved culture: chosen culture survives replication and reload
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

## character.003 Choose background

Each entry retains authority and separate owner/observer observations in the CSV.

### character.003.case-01

Choose background / childhood

- Entry/action: Client character creation / character screen; Perform choose background specifically in the childhood context
- Preconditions: Isolated childhood context for choose background
- Expected: Acceptance requirement for childhood: selected background produces the intended initial development state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.003.case-02

Choose background / adolescence

- Entry/action: Client character creation / character screen; Perform choose background specifically in the adolescence context
- Preconditions: Isolated adolescence context for choose background
- Expected: Acceptance requirement for adolescence: selected background produces the intended initial development state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.003.case-03

Choose background / adulthood choices

- Entry/action: Client character creation / character screen; Perform choose background specifically in the adulthood choices context
- Preconditions: Isolated adulthood choices context for choose background
- Expected: Acceptance requirement for adulthood choices: selected background produces the intended initial development state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

## character.004 Choose body appearance

Each entry retains authority and separate owner/observer observations in the CSV.

### character.004.case-01

Choose body appearance / face

- Entry/action: Client character creation / character screen; Perform choose body appearance specifically in the face context
- Preconditions: Isolated face context for choose body appearance
- Expected: Acceptance requirement for face: own character appearance matches on both rendered clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.004.case-02

Choose body appearance / hair

- Entry/action: Client character creation / character screen; Perform choose body appearance specifically in the hair context
- Preconditions: Isolated hair context for choose body appearance
- Expected: Acceptance requirement for hair: own character appearance matches on both rendered clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.004.case-03

Choose body appearance / age

- Entry/action: Client character creation / character screen; Perform choose body appearance specifically in the age context
- Preconditions: Isolated age context for choose body appearance
- Expected: Acceptance requirement for age: own character appearance matches on both rendered clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.004.case-04

Choose body appearance / build

- Entry/action: Client character creation / character screen; Perform choose body appearance specifically in the build context
- Preconditions: Isolated build context for choose body appearance
- Expected: Acceptance requirement for build: own character appearance matches on both rendered clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.004.case-05

Choose body appearance / weight

- Entry/action: Client character creation / character screen; Perform choose body appearance specifically in the weight context
- Preconditions: Isolated weight context for choose body appearance
- Expected: Acceptance requirement for weight: own character appearance matches on both rendered clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.004.case-06

Choose body appearance / sex

- Entry/action: Client character creation / character screen; Perform choose body appearance specifically in the sex context
- Preconditions: Isolated sex context for choose body appearance
- Expected: Acceptance requirement for sex: own character appearance matches on both rendered clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

## character.005 Name hero

Each entry retains authority and separate owner/observer observations in the CSV.

### character.005.case-01

Name hero / initial name

- Entry/action: Client character creation / character screen; Perform name hero specifically in the initial name context
- Preconditions: Isolated initial name context for name hero
- Expected: Acceptance requirement for initial name: name is visible consistently in campaign and character UI
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.005.case-02

Name hero / name edit

- Entry/action: Client character creation / character screen; Perform name hero specifically in the name edit context
- Preconditions: Isolated name edit context for name hero
- Expected: Acceptance requirement for name edit: name is visible consistently in campaign and character UI
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.005.case-03

Name hero / localized characters

- Entry/action: Client character creation / character screen; Perform name hero specifically in the localized characters context
- Preconditions: Isolated localized characters context for name hero
- Expected: Acceptance requirement for localized characters: name is visible consistently in campaign and character UI
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

## character.006 Name clan

Each entry retains authority and separate owner/observer observations in the CSV.

### character.006.case-01

Name clan / initial clan name

- Entry/action: Client character creation / character screen; Perform name clan specifically in the initial clan name context
- Preconditions: Isolated initial clan name context for name clan
- Expected: Acceptance requirement for initial clan name: clan name converges without changing clan identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.006.case-02

Name clan / rename

- Entry/action: Client character creation / character screen; Perform name clan specifically in the rename context
- Preconditions: Isolated rename context for name clan
- Expected: Acceptance requirement for rename: clan name converges without changing clan identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

## character.007 Edit banner

Each entry retains authority and separate owner/observer observations in the CSV.

### character.007.case-01

Edit banner / colors

- Entry/action: Client character creation / character screen; Perform edit banner specifically in the colors context
- Preconditions: Isolated colors context for edit banner
- Expected: Acceptance requirement for colors: party and mission banner use the selected design
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.007.case-02

Edit banner / emblem

- Entry/action: Client character creation / character screen; Perform edit banner specifically in the emblem context
- Preconditions: Isolated emblem context for edit banner
- Expected: Acceptance requirement for emblem: party and mission banner use the selected design
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.007.case-03

Edit banner / saved banner

- Entry/action: Client character creation / character screen; Perform edit banner specifically in the saved banner context
- Preconditions: Isolated saved banner context for edit banner
- Expected: Acceptance requirement for saved banner: party and mission banner use the selected design
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

## character.008 Initial equipment

Each entry retains authority and separate owner/observer observations in the CSV.

### character.008.case-01

Initial equipment / battle

- Entry/action: Client character creation / character screen; Perform initial equipment specifically in the battle context
- Preconditions: Isolated battle context for initial equipment
- Expected: Acceptance requirement for battle: correct equipment is used in each applicable context
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.008.case-02

Initial equipment / civilian

- Entry/action: Client character creation / character screen; Perform initial equipment specifically in the civilian context
- Preconditions: Isolated civilian context for initial equipment
- Expected: Acceptance requirement for civilian: correct equipment is used in each applicable context
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.008.case-03

Initial equipment / stealth equipment

- Entry/action: Client character creation / character screen; Perform initial equipment specifically in the stealth equipment context
- Preconditions: Isolated stealth equipment context for initial equipment
- Expected: Acceptance requirement for stealth equipment: correct equipment is used in each applicable context
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

## character.009 Create starting party

Each entry retains authority and separate owner/observer observations in the CSV.

### character.009.case-01

Create starting party / registration

- Entry/action: Client character creation / character screen; Perform create starting party specifically in the registration context
- Preconditions: Isolated registration context for create starting party
- Expected: Acceptance requirement for registration: the client has one controllable party with resolvable network IDs
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.009.case-02

Create starting party / ownership

- Entry/action: Client character creation / character screen; Perform create starting party specifically in the ownership context
- Preconditions: Isolated ownership context for create starting party
- Expected: Acceptance requirement for ownership: the client has one controllable party with resolvable network IDs
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.009.case-03

Create starting party / initial roster

- Entry/action: Client character creation / character screen; Perform create starting party specifically in the initial roster context
- Preconditions: Isolated initial roster context for create starting party
- Expected: Acceptance requirement for initial roster: the client has one controllable party with resolvable network IDs
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

## character.010 Character creation cancellation

Each entry retains authority and separate owner/observer observations in the CSV.

### character.010.case-01

Character creation cancellation / before confirmation

- Entry/action: Client character creation / character screen; Perform character creation cancellation specifically in the before confirmation context
- Preconditions: Isolated before confirmation context for character creation cancellation
- Expected: Acceptance requirement for before confirmation: cancellation does not leave a second hero or party
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)

### character.010.case-02

Character creation cancellation / reconnect during creation

- Entry/action: Client character creation / character screen; Perform character creation cancellation specifically in the reconnect during creation context
- Preconditions: Isolated reconnect during creation context for character creation cancellation
- Expected: Acceptance requirement for reconnect during creation: cancellation does not leave a second hero or party
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CharacterCreation](../../source/GameInterface/Services/CharacterCreation), [source/GameInterface/Services/Heroes](../../source/GameInterface/Services/Heroes), [source/GameInterface/Services/Banners](../../source/GameInterface/Services/Banners)
