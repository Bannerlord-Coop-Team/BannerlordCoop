# save behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## save.001 Save campaign

Each entry retains authority and separate owner/observer observations in the CSV.

### save.001.case-01

Save campaign / manual

- Entry/action: Server save / session sidecar / startup save selection; Perform save campaign specifically in the manual context
- Preconditions: Isolated manual context for save campaign
- Expected: Acceptance requirement for manual: completed save is readable and identified after write completion
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.001.case-02

Save campaign / automatic

- Entry/action: Server save / session sidecar / startup save selection; Perform save campaign specifically in the automatic context
- Preconditions: Isolated automatic context for save campaign
- Expected: Acceptance requirement for automatic: completed save is readable and identified after write completion
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.001.case-03

Save campaign / pending authoritative action

- Entry/action: Server save / session sidecar / startup save selection; Perform save campaign specifically in the pending authoritative action context
- Preconditions: Isolated pending authoritative action context for save campaign
- Expected: Acceptance requirement for pending authoritative action: completed save is readable and identified after write completion
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

## save.002 Save player registrations

Each entry retains authority and separate owner/observer observations in the CSV.

### save.002.case-01

Save player registrations / multiple clients

- Entry/action: Server save / session sidecar / startup save selection; Perform save player registrations specifically in the multiple clients context
- Preconditions: Isolated multiple clients context for save player registrations
- Expected: Acceptance requirement for multiple clients: session metadata retains correct platform/hero/party mappings
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.002.case-02

Save player registrations / disconnected player

- Entry/action: Server save / session sidecar / startup save selection; Perform save player registrations specifically in the disconnected player context
- Preconditions: Isolated disconnected player context for save player registrations
- Expected: Acceptance requirement for disconnected player: session metadata retains correct platform/hero/party mappings
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

## save.003 Save quest ownership

Each entry retains authority and separate owner/observer observations in the CSV.

### save.003.case-01

Save quest ownership / personal

- Entry/action: Server save / session sidecar / startup save selection; Perform save quest ownership specifically in the personal context
- Preconditions: Isolated personal context for save quest ownership
- Expected: Acceptance requirement for personal: owner/progress survive without reward replay
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.003.case-02

Save quest ownership / alternative in progress

- Entry/action: Server save / session sidecar / startup save selection; Perform save quest ownership specifically in the alternative in progress context
- Preconditions: Isolated alternative in progress context for save quest ownership
- Expected: Acceptance requirement for alternative in progress: owner/progress survive without reward replay
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.003.case-03

Save quest ownership / completed

- Entry/action: Server save / session sidecar / startup save selection; Perform save quest ownership specifically in the completed context
- Preconditions: Isolated completed context for save quest ownership
- Expected: Acceptance requirement for completed: owner/progress survive without reward replay
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

## save.004 Save crafted items

Each entry retains authority and separate owner/observer observations in the CSV.

### save.004.case-01

Save crafted items / new weapon

- Entry/action: Server save / session sidecar / startup save selection; Perform save crafted items specifically in the new weapon context
- Preconditions: Isolated new weapon context for save crafted items
- Expected: Acceptance requirement for new weapon: item and design reload with stable identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.004.case-02

Save crafted items / custom name

- Entry/action: Server save / session sidecar / startup save selection; Perform save crafted items specifically in the custom name context
- Preconditions: Isolated custom name context for save crafted items
- Expected: Acceptance requirement for custom name: item and design reload with stable identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.004.case-03

Save crafted items / design identity

- Entry/action: Server save / session sidecar / startup save selection; Perform save crafted items specifically in the design identity context
- Preconditions: Isolated design identity context for save crafted items
- Expected: Acceptance requirement for design identity: item and design reload with stable identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

## save.005 Save dynamic world objects

Each entry retains authority and separate owner/observer observations in the CSV.

### save.005.case-01

Save dynamic world objects / new party

- Entry/action: Server save / session sidecar / startup save selection; Perform save dynamic world objects specifically in the new party context
- Preconditions: Isolated new party context for save dynamic world objects
- Expected: Acceptance requirement for new party: reloaded objects register once and references resolve
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.005.case-02

Save dynamic world objects / rebel clan

- Entry/action: Server save / session sidecar / startup save selection; Perform save dynamic world objects specifically in the rebel clan context
- Preconditions: Isolated rebel clan context for save dynamic world objects
- Expected: Acceptance requirement for rebel clan: reloaded objects register once and references resolve
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.005.case-03

Save dynamic world objects / newborn hero

- Entry/action: Server save / session sidecar / startup save selection; Perform save dynamic world objects specifically in the newborn hero context
- Preconditions: Isolated newborn hero context for save dynamic world objects
- Expected: Acceptance requirement for newborn hero: reloaded objects register once and references resolve
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

## save.006 Load existing campaign

Each entry retains authority and separate owner/observer observations in the CSV.

### save.006.case-01

Load existing campaign / compatible

- Entry/action: Server save / session sidecar / startup save selection; Perform load existing campaign specifically in the compatible context
- Preconditions: Isolated compatible context for load existing campaign
- Expected: Acceptance requirement for compatible: loaded identity/readiness is observed or concrete load error is retained
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.006.case-02

Load existing campaign / missing

- Entry/action: Server save / session sidecar / startup save selection; Perform load existing campaign specifically in the missing context
- Preconditions: Isolated missing context for load existing campaign
- Expected: Acceptance requirement for missing: loaded identity/readiness is observed or concrete load error is retained
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.006.case-03

Load existing campaign / corrupt

- Entry/action: Server save / session sidecar / startup save selection; Perform load existing campaign specifically in the corrupt context
- Preconditions: Isolated corrupt context for load existing campaign
- Expected: Acceptance requirement for corrupt: loaded identity/readiness is observed or concrete load error is retained
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

## save.007 Select startup save

Each entry retains authority and separate owner/observer observations in the CSV.

### save.007.case-01

Select startup save / exact catalog basename

- Entry/action: Server save / session sidecar / startup save selection; Perform select startup save specifically in the exact catalog basename context
- Preconditions: Isolated exact catalog basename context for select startup save
- Expected: Acceptance requirement for exact catalog basename: requested selection is distinct from actual confirmed campaign identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.007.case-02

Select startup save / missing

- Entry/action: Server save / session sidecar / startup save selection; Perform select startup save specifically in the missing context
- Preconditions: Isolated missing context for select startup save
- Expected: Acceptance requirement for missing: requested selection is distinct from actual confirmed campaign identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.007.case-03

Select startup save / unreadable

- Entry/action: Server save / session sidecar / startup save selection; Perform select startup save specifically in the unreadable context
- Preconditions: Isolated unreadable context for select startup save
- Expected: Acceptance requirement for unreadable: requested selection is distinct from actual confirmed campaign identity
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

## save.008 Join after reload

Each entry retains authority and separate owner/observer observations in the CSV.

### save.008.case-01

Join after reload / returning player

- Entry/action: Server save / session sidecar / startup save selection; Perform join after reload specifically in the returning player context
- Preconditions: Isolated returning player context for join after reload
- Expected: Acceptance requirement for returning player: correct saved or new hero/party is admitted once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.008.case-02

Join after reload / new player

- Entry/action: Server save / session sidecar / startup save selection; Perform join after reload specifically in the new player context
- Preconditions: Isolated new player context for join after reload
- Expected: Acceptance requirement for new player: correct saved or new hero/party is admitted once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

## save.009 Save shutdown continuity

Each entry retains authority and separate owner/observer observations in the CSV.

### save.009.case-01

Save shutdown continuity / save active

- Entry/action: Server save / session sidecar / startup save selection; Perform save shutdown continuity specifically in the save active context
- Preconditions: Isolated save active context for save shutdown continuity
- Expected: Acceptance requirement for save active: only verified completed saves are treated as durable recovery points
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.009.case-02

Save shutdown continuity / process exit

- Entry/action: Server save / session sidecar / startup save selection; Perform save shutdown continuity specifically in the process exit context
- Preconditions: Isolated process exit context for save shutdown continuity
- Expected: Acceptance requirement for process exit: only verified completed saves are treated as durable recovery points
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.009.case-03

Save shutdown continuity / cancellation

- Entry/action: Server save / session sidecar / startup save selection; Perform save shutdown continuity specifically in the cancellation context
- Preconditions: Isolated cancellation context for save shutdown continuity
- Expected: Acceptance requirement for cancellation: only verified completed saves are treated as durable recovery points
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

## save.010 Session sidecar missing

Each entry retains authority and separate owner/observer observations in the CSV.

### save.010.case-01

Session sidecar missing / campaign save exists

- Entry/action: Server save / session sidecar / startup save selection; Perform session sidecar missing specifically in the campaign save exists context
- Preconditions: Isolated campaign save exists context for session sidecar missing
- Expected: Acceptance requirement for campaign save exists: missing registration persistence is reported without invented recovery
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

### save.010.case-02

Session sidecar missing / no player metadata

- Entry/action: Server save / session sidecar / startup save selection; Perform session sidecar missing specifically in the no player metadata context
- Preconditions: Isolated no player metadata context for session sidecar missing
- Expected: Acceptance requirement for no player metadata: missing registration persistence is reported without invented recovery
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Save](../../source/GameInterface/Services/Save), [source/Coop.Core](../../source/Coop.Core), [doc/automated-testing/mcp-deployment.md](../../doc/automated-testing/mcp-deployment.md), [tools/CoopMcpServer](../../tools/CoopMcpServer)

## save.011 Client escape-menu save buttons

Each entry retains authority and separate owner/observer observations in the CSV.

### save.011.case-01

Client escape-menu save buttons / Save

- Entry/action: Client campaign escape menu; Perform client escape-menu save buttons specifically in the Save context
- Preconditions: Isolated Save context for client escape-menu save buttons
- Expected: Acceptance requirement for Save: client save actions remain disabled after refresh and identify server-owned campaign saving
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support disabled on clients
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/UI/Patches/EscapeMenuDisableSavePatch.cs](../../source/GameInterface/Services/UI/Patches/EscapeMenuDisableSavePatch.cs)

### save.011.case-02

Client escape-menu save buttons / Save As

- Entry/action: Client campaign escape menu; Perform client escape-menu save buttons specifically in the Save As context
- Preconditions: Isolated Save As context for client escape-menu save buttons
- Expected: Acceptance requirement for Save As: client save actions remain disabled after refresh and identify server-owned campaign saving
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support disabled on clients
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/UI/Patches/EscapeMenuDisableSavePatch.cs](../../source/GameInterface/Services/UI/Patches/EscapeMenuDisableSavePatch.cs)

### save.011.case-03

Client escape-menu save buttons / Save And Exit

- Entry/action: Client campaign escape menu; Perform client escape-menu save buttons specifically in the Save And Exit context
- Preconditions: Isolated Save And Exit context for client escape-menu save buttons
- Expected: Acceptance requirement for Save And Exit: client save actions remain disabled after refresh and identify server-owned campaign saving
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support disabled on clients
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/UI/Patches/EscapeMenuDisableSavePatch.cs](../../source/GameInterface/Services/UI/Patches/EscapeMenuDisableSavePatch.cs)

### save.011.case-04

Client escape-menu save buttons / refresh

- Entry/action: Client campaign escape menu; Perform client escape-menu save buttons specifically in the refresh context
- Preconditions: Isolated refresh context for client escape-menu save buttons
- Expected: Acceptance requirement for refresh: client save actions remain disabled after refresh and identify server-owned campaign saving
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support disabled on clients
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/UI/Patches/EscapeMenuDisableSavePatch.cs](../../source/GameInterface/Services/UI/Patches/EscapeMenuDisableSavePatch.cs)
