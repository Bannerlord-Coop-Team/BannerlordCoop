# inventory behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## inventory.001 Equip weapon

Each entry retains authority and separate owner/observer observations in the CSV.

### inventory.001.case-01

Equip weapon / empty slot

- Entry/action: Client inventory / equipment screen; Perform equip weapon specifically in the empty slot context
- Preconditions: Isolated empty slot context for equip weapon
- Expected: Acceptance requirement for empty slot: selected item appears in the correct equipment slot and mission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.001.case-02

Equip weapon / replacement

- Entry/action: Client inventory / equipment screen; Perform equip weapon specifically in the replacement context
- Preconditions: Isolated replacement context for equip weapon
- Expected: Acceptance requirement for replacement: selected item appears in the correct equipment slot and mission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.001.case-03

Equip weapon / two-handed

- Entry/action: Client inventory / equipment screen; Perform equip weapon specifically in the two-handed context
- Preconditions: Isolated two-handed context for equip weapon
- Expected: Acceptance requirement for two-handed: selected item appears in the correct equipment slot and mission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.001.case-04

Equip weapon / shield

- Entry/action: Client inventory / equipment screen; Perform equip weapon specifically in the shield context
- Preconditions: Isolated shield context for equip weapon
- Expected: Acceptance requirement for shield: selected item appears in the correct equipment slot and mission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

## inventory.002 Equip armor

Each entry retains authority and separate owner/observer observations in the CSV.

### inventory.002.case-01

Equip armor / head

- Entry/action: Client inventory / equipment screen; Perform equip armor specifically in the head context
- Preconditions: Isolated head context for equip armor
- Expected: Acceptance requirement for head: armor and resulting character appearance match both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.002.case-02

Equip armor / body

- Entry/action: Client inventory / equipment screen; Perform equip armor specifically in the body context
- Preconditions: Isolated body context for equip armor
- Expected: Acceptance requirement for body: armor and resulting character appearance match both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.002.case-03

Equip armor / arm

- Entry/action: Client inventory / equipment screen; Perform equip armor specifically in the arm context
- Preconditions: Isolated arm context for equip armor
- Expected: Acceptance requirement for arm: armor and resulting character appearance match both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.002.case-04

Equip armor / leg

- Entry/action: Client inventory / equipment screen; Perform equip armor specifically in the leg context
- Preconditions: Isolated leg context for equip armor
- Expected: Acceptance requirement for leg: armor and resulting character appearance match both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.002.case-05

Equip armor / cape

- Entry/action: Client inventory / equipment screen; Perform equip armor specifically in the cape context
- Preconditions: Isolated cape context for equip armor
- Expected: Acceptance requirement for cape: armor and resulting character appearance match both clients
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

## inventory.003 Equip mount

Each entry retains authority and separate owner/observer observations in the CSV.

### inventory.003.case-01

Equip mount / horse

- Entry/action: Client inventory / equipment screen; Perform equip mount specifically in the horse context
- Preconditions: Isolated horse context for equip mount
- Expected: Acceptance requirement for horse: mounted state uses the selected valid equipment combination
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.003.case-02

Equip mount / camel

- Entry/action: Client inventory / equipment screen; Perform equip mount specifically in the camel context
- Preconditions: Isolated camel context for equip mount
- Expected: Acceptance requirement for camel: mounted state uses the selected valid equipment combination
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.003.case-03

Equip mount / incompatible harness

- Entry/action: Client inventory / equipment screen; Perform equip mount specifically in the incompatible harness context
- Preconditions: Isolated incompatible harness context for equip mount
- Expected: Acceptance requirement for incompatible harness: mounted state uses the selected valid equipment combination
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.003.case-04

Equip mount / no mount

- Entry/action: Client inventory / equipment screen; Perform equip mount specifically in the no mount context
- Preconditions: Isolated no mount context for equip mount
- Expected: Acceptance requirement for no mount: mounted state uses the selected valid equipment combination
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

## inventory.004 Equip civilian clothing

Each entry retains authority and separate owner/observer observations in the CSV.

### inventory.004.case-01

Equip civilian clothing / town scene

- Entry/action: Client inventory / equipment screen; Perform equip civilian clothing specifically in the town scene context
- Preconditions: Isolated town scene context for equip civilian clothing
- Expected: Acceptance requirement for town scene: civilian loadout is preserved separately from battle equipment
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.004.case-02

Equip civilian clothing / civilian weapon restrictions

- Entry/action: Client inventory / equipment screen; Perform equip civilian clothing specifically in the civilian weapon restrictions context
- Preconditions: Isolated civilian weapon restrictions context for equip civilian clothing
- Expected: Acceptance requirement for civilian weapon restrictions: civilian loadout is preserved separately from battle equipment
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

## inventory.005 Equip stealth loadout

Each entry retains authority and separate owner/observer observations in the CSV.

### inventory.005.case-01

Equip stealth loadout / stealth mission

- Entry/action: Client inventory / equipment screen; Perform equip stealth loadout specifically in the stealth mission context
- Preconditions: Isolated stealth mission context for equip stealth loadout
- Expected: Acceptance requirement for stealth mission: context uses the correct loadout and restores ordinary equipment afterward
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.005.case-02

Equip stealth loadout / return to ordinary scene

- Entry/action: Client inventory / equipment screen; Perform equip stealth loadout specifically in the return to ordinary scene context
- Preconditions: Isolated return to ordinary scene context for equip stealth loadout
- Expected: Acceptance requirement for return to ordinary scene: context uses the correct loadout and restores ordinary equipment afterward
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

## inventory.006 Move inventory item

Each entry retains authority and separate owner/observer observations in the CSV.

### inventory.006.case-01

Move inventory item / single

- Entry/action: Client inventory / equipment screen; Perform move inventory item specifically in the single context
- Preconditions: Isolated single context for move inventory item
- Expected: Acceptance requirement for single: item identities and quantities balance across moved stacks
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.006.case-02

Move inventory item / stack

- Entry/action: Client inventory / equipment screen; Perform move inventory item specifically in the stack context
- Preconditions: Isolated stack context for move inventory item
- Expected: Acceptance requirement for stack: item identities and quantities balance across moved stacks
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.006.case-03

Move inventory item / modifier variant

- Entry/action: Client inventory / equipment screen; Perform move inventory item specifically in the modifier variant context
- Preconditions: Isolated modifier variant context for move inventory item
- Expected: Acceptance requirement for modifier variant: item identities and quantities balance across moved stacks
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

## inventory.007 Discard item

Each entry retains authority and separate owner/observer observations in the CSV.

### inventory.007.case-01

Discard item / confirm

- Entry/action: Client inventory / equipment screen; Perform discard item specifically in the confirm context
- Preconditions: Isolated confirm context for discard item
- Expected: Acceptance requirement for confirm: discard removes only the confirmed quantity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.007.case-02

Discard item / cancel

- Entry/action: Client inventory / equipment screen; Perform discard item specifically in the cancel context
- Preconditions: Isolated cancel context for discard item
- Expected: Acceptance requirement for cancel: discard removes only the confirmed quantity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.007.case-03

Discard item / whole stack

- Entry/action: Client inventory / equipment screen; Perform discard item specifically in the whole stack context
- Preconditions: Isolated whole stack context for discard item
- Expected: Acceptance requirement for whole stack: discard removes only the confirmed quantity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

## inventory.008 Sort inventory

Each entry retains authority and separate owner/observer observations in the CSV.

### inventory.008.case-01

Sort inventory / name

- Entry/action: Client inventory / equipment screen; Perform sort inventory specifically in the name context
- Preconditions: Isolated name context for sort inventory
- Expected: Acceptance requirement for name: ordering changes presentation without changing ownership or quantities
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.008.case-02

Sort inventory / type

- Entry/action: Client inventory / equipment screen; Perform sort inventory specifically in the type context
- Preconditions: Isolated type context for sort inventory
- Expected: Acceptance requirement for type: ordering changes presentation without changing ownership or quantities
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.008.case-03

Sort inventory / value

- Entry/action: Client inventory / equipment screen; Perform sort inventory specifically in the value context
- Preconditions: Isolated value context for sort inventory
- Expected: Acceptance requirement for value: ordering changes presentation without changing ownership or quantities
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.008.case-04

Sort inventory / weight

- Entry/action: Client inventory / equipment screen; Perform sort inventory specifically in the weight context
- Preconditions: Isolated weight context for sort inventory
- Expected: Acceptance requirement for weight: ordering changes presentation without changing ownership or quantities
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

## inventory.009 Inventory capacity

Each entry retains authority and separate owner/observer observations in the CSV.

### inventory.009.case-01

Inventory capacity / pack animals

- Entry/action: Client inventory / equipment screen; Perform inventory capacity specifically in the pack animals context
- Preconditions: Isolated pack animals context for inventory capacity
- Expected: Acceptance requirement for pack animals: weight and movement consequences match roster/equipment state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.009.case-02

Inventory capacity / heavy load

- Entry/action: Client inventory / equipment screen; Perform inventory capacity specifically in the heavy load context
- Preconditions: Isolated heavy load context for inventory capacity
- Expected: Acceptance requirement for heavy load: weight and movement consequences match roster/equipment state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.009.case-03

Inventory capacity / overburden

- Entry/action: Client inventory / equipment screen; Perform inventory capacity specifically in the overburden context
- Preconditions: Isolated overburden context for inventory capacity
- Expected: Acceptance requirement for overburden: weight and movement consequences match roster/equipment state
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

## inventory.010 Item modifiers

Each entry retains authority and separate owner/observer observations in the CSV.

### inventory.010.case-01

Item modifiers / damaged

- Entry/action: Client inventory / equipment screen; Perform item modifiers specifically in the damaged context
- Preconditions: Isolated damaged context for item modifiers
- Expected: Acceptance requirement for damaged: modifier identity survives transfer and equipment replication
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.010.case-02

Item modifiers / fine

- Entry/action: Client inventory / equipment screen; Perform item modifiers specifically in the fine context
- Preconditions: Isolated fine context for item modifiers
- Expected: Acceptance requirement for fine: modifier identity survives transfer and equipment replication
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.010.case-03

Item modifiers / crafted

- Entry/action: Client inventory / equipment screen; Perform item modifiers specifically in the crafted context
- Preconditions: Isolated crafted context for item modifiers
- Expected: Acceptance requirement for crafted: modifier identity survives transfer and equipment replication
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.010.case-04

Item modifiers / replacement

- Entry/action: Client inventory / equipment screen; Perform item modifiers specifically in the replacement context
- Preconditions: Isolated replacement context for item modifiers
- Expected: Acceptance requirement for replacement: modifier identity survives transfer and equipment replication
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

## inventory.011 Use consumable resources

Each entry retains authority and separate owner/observer observations in the CSV.

### inventory.011.case-01

Use consumable resources / food

- Entry/action: Client inventory / equipment screen; Perform use consumable resources specifically in the food context
- Preconditions: Isolated food context for use consumable resources
- Expected: Acceptance requirement for food: the production action consumes the correct item quantity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.011.case-02

Use consumable resources / crafting material

- Entry/action: Client inventory / equipment screen; Perform use consumable resources specifically in the crafting material context
- Preconditions: Isolated crafting material context for use consumable resources
- Expected: Acceptance requirement for crafting material: the production action consumes the correct item quantity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.011.case-03

Use consumable resources / ammunition

- Entry/action: Client inventory / equipment screen; Perform use consumable resources specifically in the ammunition context
- Preconditions: Isolated ammunition context for use consumable resources
- Expected: Acceptance requirement for ammunition: the production action consumes the correct item quantity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

## inventory.012 Loot inventory

Each entry retains authority and separate owner/observer observations in the CSV.

### inventory.012.case-01

Loot inventory / battle loot

- Entry/action: Client inventory / equipment screen; Perform loot inventory specifically in the battle loot context
- Preconditions: Isolated battle loot context for loot inventory
- Expected: Acceptance requirement for battle loot: accepted items reach the owning player's inventory once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.012.case-02

Loot inventory / raid loot

- Entry/action: Client inventory / equipment screen; Perform loot inventory specifically in the raid loot context
- Preconditions: Isolated raid loot context for loot inventory
- Expected: Acceptance requirement for raid loot: accepted items reach the owning player's inventory once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)

### inventory.012.case-03

Loot inventory / declined items

- Entry/action: Client inventory / equipment screen; Perform loot inventory specifically in the declined items context
- Preconditions: Isolated declined items context for loot inventory
- Expected: Acceptance requirement for declined items: accepted items reach the owning player's inventory once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Inventory](../../source/GameInterface/Services/Inventory), [source/GameInterface/Services/ItemRosters](../../source/GameInterface/Services/ItemRosters), [source/GameInterface/Services/Equipments](../../source/GameInterface/Services/Equipments)
