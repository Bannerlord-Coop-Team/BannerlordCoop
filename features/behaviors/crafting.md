# crafting behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## crafting.001 Choose smith

Each entry retains authority and separate owner/observer observations in the CSV.

### crafting.001.case-01

Choose smith / player

- Entry/action: Town smithy / crafting screen; Perform choose smith specifically in the player context
- Preconditions: Isolated player context for choose smith
- Expected: Acceptance requirement for player: crafting changes and stamina apply to the selected hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.001.case-02

Choose smith / companion

- Entry/action: Town smithy / crafting screen; Perform choose smith specifically in the companion context
- Preconditions: Isolated companion context for choose smith
- Expected: Acceptance requirement for companion: crafting changes and stamina apply to the selected hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.001.case-03

Choose smith / unavailable smith

- Entry/action: Town smithy / crafting screen; Perform choose smith specifically in the unavailable smith context
- Preconditions: Isolated unavailable smith context for choose smith
- Expected: Acceptance requirement for unavailable smith: crafting changes and stamina apply to the selected hero
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

## crafting.002 Select weapon template

Each entry retains authority and separate owner/observer observations in the CSV.

### crafting.002.case-01

Select weapon template / type

- Entry/action: Town smithy / crafting screen; Perform select weapon template specifically in the type context
- Preconditions: Isolated type context for select weapon template
- Expected: Acceptance requirement for type: selected combination resolves to a valid weapon design
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.002.case-02

Select weapon template / valid pieces

- Entry/action: Town smithy / crafting screen; Perform select weapon template specifically in the valid pieces context
- Preconditions: Isolated valid pieces context for select weapon template
- Expected: Acceptance requirement for valid pieces: selected combination resolves to a valid weapon design
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.002.case-03

Select weapon template / incompatible pieces

- Entry/action: Town smithy / crafting screen; Perform select weapon template specifically in the incompatible pieces context
- Preconditions: Isolated incompatible pieces context for select weapon template
- Expected: Acceptance requirement for incompatible pieces: selected combination resolves to a valid weapon design
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

## crafting.003 Change weapon pieces

Each entry retains authority and separate owner/observer observations in the CSV.

### crafting.003.case-01

Change weapon pieces / blade

- Entry/action: Town smithy / crafting screen; Perform change weapon pieces specifically in the blade context
- Preconditions: Isolated blade context for change weapon pieces
- Expected: Acceptance requirement for blade: crafted design retains the exact selected compatible parts
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.003.case-02

Change weapon pieces / guard

- Entry/action: Town smithy / crafting screen; Perform change weapon pieces specifically in the guard context
- Preconditions: Isolated guard context for change weapon pieces
- Expected: Acceptance requirement for guard: crafted design retains the exact selected compatible parts
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.003.case-03

Change weapon pieces / handle

- Entry/action: Town smithy / crafting screen; Perform change weapon pieces specifically in the handle context
- Preconditions: Isolated handle context for change weapon pieces
- Expected: Acceptance requirement for handle: crafted design retains the exact selected compatible parts
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.003.case-04

Change weapon pieces / pommel

- Entry/action: Town smithy / crafting screen; Perform change weapon pieces specifically in the pommel context
- Preconditions: Isolated pommel context for change weapon pieces
- Expected: Acceptance requirement for pommel: crafted design retains the exact selected compatible parts
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.003.case-05

Change weapon pieces / size

- Entry/action: Town smithy / crafting screen; Perform change weapon pieces specifically in the size context
- Preconditions: Isolated size context for change weapon pieces
- Expected: Acceptance requirement for size: crafted design retains the exact selected compatible parts
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

## crafting.004 Forge weapon

Each entry retains authority and separate owner/observer observations in the CSV.

### crafting.004.case-01

Forge weapon / materials available

- Entry/action: Town smithy / crafting screen; Perform forge weapon specifically in the materials available context
- Preconditions: Isolated materials available context for forge weapon
- Expected: Acceptance requirement for materials available: one weapon is created with correct resource/stamina consumption
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.004.case-02

Forge weapon / missing material

- Entry/action: Town smithy / crafting screen; Perform forge weapon specifically in the missing material context
- Preconditions: Isolated missing material context for forge weapon
- Expected: Acceptance requirement for missing material: one weapon is created with correct resource/stamina consumption
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.004.case-03

Forge weapon / insufficient stamina

- Entry/action: Town smithy / crafting screen; Perform forge weapon specifically in the insufficient stamina context
- Preconditions: Isolated insufficient stamina context for forge weapon
- Expected: Acceptance requirement for insufficient stamina: one weapon is created with correct resource/stamina consumption
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

## crafting.005 Name crafted weapon

Each entry retains authority and separate owner/observer observations in the CSV.

### crafting.005.case-01

Name crafted weapon / default name

- Entry/action: Town smithy / crafting screen; Perform name crafted weapon specifically in the default name context
- Preconditions: Isolated default name context for name crafted weapon
- Expected: Acceptance requirement for default name: name and crafted item identity survive replication/reload
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.005.case-02

Name crafted weapon / custom name

- Entry/action: Town smithy / crafting screen; Perform name crafted weapon specifically in the custom name context
- Preconditions: Isolated custom name context for name crafted weapon
- Expected: Acceptance requirement for custom name: name and crafted item identity survive replication/reload
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.005.case-03

Name crafted weapon / localized text

- Entry/action: Town smithy / crafting screen; Perform name crafted weapon specifically in the localized text context
- Preconditions: Isolated localized text context for name crafted weapon
- Expected: Acceptance requirement for localized text: name and crafted item identity survive replication/reload
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

## crafting.006 Refine material

Each entry retains authority and separate owner/observer observations in the CSV.

### crafting.006.case-01

Refine material / valid conversion

- Entry/action: Town smithy / crafting screen; Perform refine material specifically in the valid conversion context
- Preconditions: Isolated valid conversion context for refine material
- Expected: Acceptance requirement for valid conversion: resource quantities balance according to the selected conversion
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.006.case-02

Refine material / missing input

- Entry/action: Town smithy / crafting screen; Perform refine material specifically in the missing input context
- Preconditions: Isolated missing input context for refine material
- Expected: Acceptance requirement for missing input: resource quantities balance according to the selected conversion
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.006.case-03

Refine material / perk variant

- Entry/action: Town smithy / crafting screen; Perform refine material specifically in the perk variant context
- Preconditions: Isolated perk variant context for refine material
- Expected: Acceptance requirement for perk variant: resource quantities balance according to the selected conversion
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

## crafting.007 Smelt weapon

Each entry retains authority and separate owner/observer observations in the CSV.

### crafting.007.case-01

Smelt weapon / ordinary

- Entry/action: Town smithy / crafting screen; Perform smelt weapon specifically in the ordinary context
- Preconditions: Isolated ordinary context for smelt weapon
- Expected: Acceptance requirement for ordinary: weapon is consumed and recovered materials match the action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.007.case-02

Smelt weapon / crafted

- Entry/action: Town smithy / crafting screen; Perform smelt weapon specifically in the crafted context
- Preconditions: Isolated crafted context for smelt weapon
- Expected: Acceptance requirement for crafted: weapon is consumed and recovered materials match the action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.007.case-03

Smelt weapon / modifier

- Entry/action: Town smithy / crafting screen; Perform smelt weapon specifically in the modifier context
- Preconditions: Isolated modifier context for smelt weapon
- Expected: Acceptance requirement for modifier: weapon is consumed and recovered materials match the action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.007.case-04

Smelt weapon / no stamina

- Entry/action: Town smithy / crafting screen; Perform smelt weapon specifically in the no stamina context
- Preconditions: Isolated no stamina context for smelt weapon
- Expected: Acceptance requirement for no stamina: weapon is consumed and recovered materials match the action
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

## crafting.008 Unlock crafting piece

Each entry retains authority and separate owner/observer observations in the CSV.

### crafting.008.case-01

Unlock crafting piece / new unlock

- Entry/action: Town smithy / crafting screen; Perform unlock crafting piece specifically in the new unlock context
- Preconditions: Isolated new unlock context for unlock crafting piece
- Expected: Acceptance requirement for new unlock: unlock state belongs to the correct development context
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.008.case-02

Unlock crafting piece / already unlocked

- Entry/action: Town smithy / crafting screen; Perform unlock crafting piece specifically in the already unlocked context
- Preconditions: Isolated already unlocked context for unlock crafting piece
- Expected: Acceptance requirement for already unlocked: unlock state belongs to the correct development context
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

## crafting.009 Complete crafting order

Each entry retains authority and separate owner/observer observations in the CSV.

### crafting.009.case-01

Complete crafting order / requirements met

- Entry/action: Town smithy / crafting screen; Perform complete crafting order specifically in the requirements met context
- Preconditions: Isolated requirements met context for complete crafting order
- Expected: Acceptance requirement for requirements met: reward and order state reflect the completed crafted weapon
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.009.case-02

Complete crafting order / missed quality

- Entry/action: Town smithy / crafting screen; Perform complete crafting order specifically in the missed quality context
- Preconditions: Isolated missed quality context for complete crafting order
- Expected: Acceptance requirement for missed quality: reward and order state reflect the completed crafted weapon
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.009.case-03

Complete crafting order / deadline

- Entry/action: Town smithy / crafting screen; Perform complete crafting order specifically in the deadline context
- Preconditions: Isolated deadline context for complete crafting order
- Expected: Acceptance requirement for deadline: reward and order state reflect the completed crafted weapon
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

## crafting.010 Crafting stamina recovery

Each entry retains authority and separate owner/observer observations in the CSV.

### crafting.010.case-01

Crafting stamina recovery / rest

- Entry/action: Town smithy / crafting screen; Perform crafting stamina recovery specifically in the rest context
- Preconditions: Isolated rest context for crafting stamina recovery
- Expected: Acceptance requirement for rest: recovery follows time and the selected hero's record
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.010.case-02

Crafting stamina recovery / campaign travel

- Entry/action: Town smithy / crafting screen; Perform crafting stamina recovery specifically in the campaign travel context
- Preconditions: Isolated campaign travel context for crafting stamina recovery
- Expected: Acceptance requirement for campaign travel: recovery follows time and the selected hero's record
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.010.case-03

Crafting stamina recovery / hero switched

- Entry/action: Town smithy / crafting screen; Perform crafting stamina recovery specifically in the hero switched context
- Preconditions: Isolated hero switched context for crafting stamina recovery
- Expected: Acceptance requirement for hero switched: recovery follows time and the selected hero's record
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

## crafting.011 Crafted item registration

Each entry retains authority and separate owner/observer observations in the CSV.

### crafting.011.case-01

Crafted item registration / new item

- Entry/action: Town smithy / crafting screen; Perform crafted item registration specifically in the new item context
- Preconditions: Isolated new item context for crafted item registration
- Expected: Acceptance requirement for new item: all peers resolve the same crafted item and design identities
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.011.case-02

Crafted item registration / repeat same design

- Entry/action: Town smithy / crafting screen; Perform crafted item registration specifically in the repeat same design context
- Preconditions: Isolated repeat same design context for crafted item registration
- Expected: Acceptance requirement for repeat same design: all peers resolve the same crafted item and design identities
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)

### crafting.011.case-03

Crafted item registration / reload

- Entry/action: Town smithy / crafting screen; Perform crafted item registration specifically in the reload context
- Preconditions: Isolated reload context for crafted item registration
- Expected: Acceptance requirement for reload: all peers resolve the same crafted item and design identities
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/CraftingService](../../source/GameInterface/Services/CraftingService), [source/GameInterface/Services/CraftingOrders](../../source/GameInterface/Services/CraftingOrders), [source/GameInterface/Services/Smithing](../../source/GameInterface/Services/Smithing), [source/GameInterface/Services/WeaponDesigns](../../source/GameInterface/Services/WeaponDesigns)
