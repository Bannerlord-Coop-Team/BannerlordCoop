# vanilla-modes behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## vanilla-modes.001 Sandbox campaign

Each entry retains authority and separate owner/observer observations in the CSV.

### vanilla-modes.001.case-01

Sandbox campaign / new game

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform sandbox campaign specifically in the new game context
- Preconditions: Isolated new game context for sandbox campaign
- Expected: Acceptance requirement for new game: entry and behavior registration are established for the exact installed build
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

### vanilla-modes.001.case-02

Sandbox campaign / saved game

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform sandbox campaign specifically in the saved game context
- Preconditions: Isolated saved game context for sandbox campaign
- Expected: Acceptance requirement for saved game: entry and behavior registration are established for the exact installed build
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

## vanilla-modes.002 Story campaign

Each entry retains authority and separate owner/observer observations in the CSV.

### vanilla-modes.002.case-01

Story campaign / tutorial

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform story campaign specifically in the tutorial context
- Preconditions: Isolated tutorial context for story campaign
- Expected: Acceptance requirement for tutorial: StoryMode-owned paths require separate source and runtime evidence
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

### vanilla-modes.002.case-02

Story campaign / story progression

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform story campaign specifically in the story progression context
- Preconditions: Isolated story progression context for story campaign
- Expected: Acceptance requirement for story progression: StoryMode-owned paths require separate source and runtime evidence
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

### vanilla-modes.002.case-03

Story campaign / quest phases

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform story campaign specifically in the quest phases context
- Preconditions: Isolated quest phases context for story campaign
- Expected: Acceptance requirement for quest phases: StoryMode-owned paths require separate source and runtime evidence
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

## vanilla-modes.003 Custom battle

Each entry retains authority and separate owner/observer observations in the CSV.

### vanilla-modes.003.case-01

Custom battle / battle setup

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform custom battle specifically in the battle setup context
- Preconditions: Isolated battle setup context for custom battle
- Expected: Acceptance requirement for battle setup: standalone mode is kept separate from co-op campaign claims
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

### vanilla-modes.003.case-02

Custom battle / teams

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform custom battle specifically in the teams context
- Preconditions: Isolated teams context for custom battle
- Expected: Acceptance requirement for teams: standalone mode is kept separate from co-op campaign claims
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

### vanilla-modes.003.case-03

Custom battle / troop selection

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform custom battle specifically in the troop selection context
- Preconditions: Isolated troop selection context for custom battle
- Expected: Acceptance requirement for troop selection: standalone mode is kept separate from co-op campaign claims
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

## vanilla-modes.004 Native multiplayer

Each entry retains authority and separate owner/observer observations in the CSV.

### vanilla-modes.004.case-01

Native multiplayer / lobby

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform native multiplayer specifically in the lobby context
- Preconditions: Isolated lobby context for native multiplayer
- Expected: Acceptance requirement for lobby: native multiplayer behavior is not inferred from co-op campaign networking
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

### vanilla-modes.004.case-02

Native multiplayer / matchmaking

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform native multiplayer specifically in the matchmaking context
- Preconditions: Isolated matchmaking context for native multiplayer
- Expected: Acceptance requirement for matchmaking: native multiplayer behavior is not inferred from co-op campaign networking
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

### vanilla-modes.004.case-03

Native multiplayer / multiplayer modes

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform native multiplayer specifically in the multiplayer modes context
- Preconditions: Isolated multiplayer modes context for native multiplayer
- Expected: Acceptance requirement for multiplayer modes: native multiplayer behavior is not inferred from co-op campaign networking
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

## vanilla-modes.005 DLC module availability

Each entry retains authority and separate owner/observer observations in the CSV.

### vanilla-modes.005.case-01

DLC module availability / installed

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform dlc module availability specifically in the installed context
- Preconditions: Isolated installed context for dlc module availability
- Expected: Acceptance requirement for installed: module availability and active gameplay support are reported separately
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

### vanilla-modes.005.case-02

DLC module availability / selected

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform dlc module availability specifically in the selected context
- Preconditions: Isolated selected context for dlc module availability
- Expected: Acceptance requirement for selected: module availability and active gameplay support are reported separately
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

### vanilla-modes.005.case-03

DLC module availability / licensed

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform dlc module availability specifically in the licensed context
- Preconditions: Isolated licensed context for dlc module availability
- Expected: Acceptance requirement for licensed: module availability and active gameplay support are reported separately
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

### vanilla-modes.005.case-04

DLC module availability / opt-in

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform dlc module availability specifically in the opt-in context
- Preconditions: Isolated opt-in context for dlc module availability
- Expected: Acceptance requirement for opt-in: module availability and active gameplay support are reported separately
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

## vanilla-modes.006 Launcher and module selection

Each entry retains authority and separate owner/observer observations in the CSV.

### vanilla-modes.006.case-01

Launcher and module selection / required baseline modules

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform launcher and module selection specifically in the required baseline modules context
- Preconditions: Isolated required baseline modules context for launcher and module selection
- Expected: Acceptance requirement for required baseline modules: selected module set matches the bound installed/deployed source identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)

### vanilla-modes.006.case-02

Launcher and module selection / Coop declaration

- Entry/action: Native/SandBox/StoryMode/CustomBattle/Multiplayer entry points; Perform launcher and module selection specifically in the Coop declaration context
- Preconditions: Isolated Coop declaration context for launcher and module selection
- Expected: Acceptance requirement for Coop declaration: selected module set matches the bound installed/deployed source identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [features/baseline/installed.json](../../features/baseline/installed.json), [source/Coop/CoopMod.cs](../../source/Coop/CoopMod.cs), [source/GameInterface/Services/UI](../../source/GameInterface/Services/UI)
