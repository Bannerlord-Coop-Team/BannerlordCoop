# quests behavior leaves

Generated from [behavior-leaves.csv](../behavior-leaves.csv).
[Index](README.md) · [Evidence meanings](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

## quests.001 Discover issue

Each entry retains authority and separate owner/observer observations in the CSV.

### quests.001.case-01

Discover issue / eligible giver

- Entry/action: Notable/lord conversation / quest journal; Perform discover issue specifically in the eligible giver context
- Preconditions: Isolated eligible giver context for discover issue
- Expected: Acceptance requirement for eligible giver: only source-allowlisted issues appear through normal supported admission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.001.case-02

Discover issue / normal generation

- Entry/action: Notable/lord conversation / quest journal; Perform discover issue specifically in the normal generation context
- Preconditions: Isolated normal generation context for discover issue
- Expected: Acceptance requirement for normal generation: only source-allowlisted issues appear through normal supported admission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.001.case-03

Discover issue / disabled type

- Entry/action: Notable/lord conversation / quest journal; Perform discover issue specifically in the disabled type context
- Preconditions: Isolated disabled type context for discover issue
- Expected: Acceptance requirement for disabled type: only source-allowlisted issues appear through normal supported admission
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### q.survival

Issue / survive

- Entry/action: Notable/lord conversation / quest journal; Issue / survive
- Preconditions: Security below 90; merchant active and in issue settlement; nonnull infested hideout
- Expected: Survival true only while all conditions hold
- State: Survival true only while all conditions hold
- Side effects: Issue availability is separate from accepted quest state
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Security exactly 90 fails survival
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@233-240
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.generation

Generation / eligible giver and hideout

- Entry/action: Notable/lord conversation / quest journal; Generation / eligible giver and hideout
- Preconditions: Gang leader in town, security below 70, different merchant, suitable hideout
- Expected: Generation condition true only with suitable hideout
- State: Generation condition true only with suitable hideout
- Side effects: Hideout search and locality require separate observations
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Security exactly 70 fails generation
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@935-945
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## quests.002 View issue dialogue

Each entry retains authority and separate owner/observer observations in the CSV.

### quests.002.case-01

View issue dialogue / issue icon

- Entry/action: Notable/lord conversation / quest journal; Perform view issue dialogue specifically in the issue icon context
- Preconditions: Isolated issue icon context for view issue dialogue
- Expected: Acceptance requirement for issue icon: dialogue entry follows the exact allowlist gate
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.002.case-02

View issue dialogue / task entry

- Entry/action: Notable/lord conversation / quest journal; Perform view issue dialogue specifically in the task entry context
- Preconditions: Isolated task entry context for view issue dialogue
- Expected: Acceptance requirement for task entry: dialogue entry follows the exact allowlist gate
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.002.case-03

View issue dialogue / alternate task entry

- Entry/action: Notable/lord conversation / quest journal; Perform view issue dialogue specifically in the alternate task entry context
- Preconditions: Isolated alternate task entry context for view issue dialogue
- Expected: Acceptance requirement for alternate task entry: dialogue entry follows the exact allowlist gate
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### q.counter-offer-gate

Merchant / initial offer gate

- Entry/action: Notable/lord conversation / quest journal; Merchant / initial offer gate
- Preconditions: Actual merchant, no leader talk, no goods, counter-offer not given
- Expected: Counter-offer dialog eligible
- State: Counter-offer dialog eligible
- Side effects: No payment or terminal outcome from condition
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Any of the three flags suppresses the initial flow
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@575-592
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.counter-offer-accept

Merchant / favorable reply

- Entry/action: Notable/lord conversation / quest journal; Merchant / favorable reply
- Preconditions: Initial offer eligible; select favorable reply
- Expected: Conversation closes without goods/gold mutation in this member
- State: Conversation closes without goods/gold mutation in this member
- Side effects: Offer-given flag is handled on settlement departure
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: This reply alone does not complete the quest
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@575-592
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.counter-offer-refuse

Merchant / dismissive reply

- Entry/action: Notable/lord conversation / quest journal; Merchant / dismissive reply
- Preconditions: Initial offer eligible; select dismissive reply
- Expected: Conversation closes without terminal failure in this member
- State: Conversation closes without terminal failure in this member
- Side effects: Offer-given flag handled elsewhere
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Do not infer betrayal from dialogue text
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@575-592
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.meeting-menu

Menu / meeting availability

- Entry/action: Notable/lord conversation / quest journal; Menu / meeting availability
- Preconditions: At quest hideout; neither fighting nor paying
- Expected: Meeting option returns true; conversation leave type and ActiveIssue metadata
- State: Meeting option returns true; conversation leave type and ActiveIssue metadata
- Side effects: Choosing consequence opens leader conversation
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Paying or fighting suppresses option
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@882-895
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## quests.003 Accept issue personally

Each entry retains authority and separate owner/observer observations in the CSV.

### quests.003.case-01

Accept issue personally / eligible

- Entry/action: Notable/lord conversation / quest journal; Perform accept issue personally specifically in the eligible context
- Preconditions: Isolated eligible context for accept issue personally
- Expected: Acceptance requirement for eligible: accepted quest belongs to the intended player
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.003.case-02

Accept issue personally / already active

- Entry/action: Notable/lord conversation / quest journal; Perform accept issue personally specifically in the already active context
- Preconditions: Isolated already active context for accept issue personally
- Expected: Acceptance requirement for already active: accepted quest belongs to the intended player
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.003.case-03

Accept issue personally / ownership conflict

- Entry/action: Notable/lord conversation / quest journal; Perform accept issue personally specifically in the ownership conflict context
- Preconditions: Isolated ownership conflict context for accept issue personally
- Expected: Acceptance requirement for ownership conflict: accepted quest belongs to the intended player
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### q.relation-reject

Relation admission / below -10

- Entry/action: Notable/lord conversation / quest journal; Relation admission / below -10
- Preconditions: Giver relation below -10
- Expected: Relation flag set, relation hero is giver, condition false
- State: Relation flag set, relation hero is giver, condition false
- Side effects: No accepted quest; requiredGold output stays 0
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Relation exactly -10 does not set this flag; other guards still apply
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@281-301
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.war-reject

War admission / at war

- Entry/action: Notable/lord conversation / quest journal; War admission / at war
- Preconditions: Giver settlement faction at war with player faction
- Expected: AtWar flag set; condition false
- State: AtWar flag set; condition false
- Side effects: Other failure flags may accumulate
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Peace alone does not clear relation or ownership flags
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@281-301
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.owner-reject

Ownership admission / player owns settlement

- Entry/action: Notable/lord conversation / quest journal; Ownership admission / player owns settlement
- Preconditions: PlayerClan equals giver settlement OwnerClan
- Expected: PlayerIsOwnerOfSettlement flag set; condition false
- State: PlayerIsOwnerOfSettlement flag set; condition false
- Side effects: No acceptance through this condition
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: A different owner avoids this flag
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@281-301
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.eligible

Admission / all guards clear

- Entry/action: Notable/lord conversation / quest journal; Admission / all guards clear
- Preconditions: Relation at least -10, no war, player's clan not settlement owner
- Expected: Condition true with no flags
- State: Condition true with no flags
- Side effects: Quest start remains a separate consequence
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Any one failed guard rejects admission
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@281-301
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.start

Accept / reveal and track hideout

- Entry/action: Notable/lord conversation / quest journal; Accept / reveal and track hideout
- Preconditions: Offer flow invokes acceptance for actual quest giver
- Expected: StartQuest called; hidden initial log; hideout spotted, visible and tracked
- State: StartQuest called; hidden initial log; hideout spotted, visible and tracked
- Side effects: No payment in this member
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Dialogue entry without acceptance does not start this consequence
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@552-559
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.duration

Quest / duration

- Entry/action: Notable/lord conversation / quest journal; Quest / duration
- Preconditions: Issue generates actual quest id and fields
- Expected: Accepted quest constructed with 20 days from now
- State: Accepted quest constructed with 20 days from now
- Side effects: Goods, hideout, reward and merchant fields passed through
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Issue availability duration is different
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@271-274
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## quests.004 Accept alternative solution

Each entry retains authority and separate owner/observer observations in the CSV.

### quests.004.case-01

Accept alternative solution / eligible companion

- Entry/action: Notable/lord conversation / quest journal; Perform accept alternative solution specifically in the eligible companion context
- Preconditions: Isolated eligible companion context for accept alternative solution
- Expected: Acceptance requirement for eligible companion: assigned companion/troops and ownership state agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.004.case-02

Accept alternative solution / troop selection

- Entry/action: Notable/lord conversation / quest journal; Perform accept alternative solution specifically in the troop selection context
- Preconditions: Isolated troop selection context for accept alternative solution
- Expected: Acceptance requirement for troop selection: assigned companion/troops and ownership state agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.004.case-03

Accept alternative solution / cancellation

- Entry/action: Notable/lord conversation / quest journal; Perform accept alternative solution specifically in the cancellation context
- Preconditions: Isolated cancellation context for accept alternative solution
- Expected: Acceptance requirement for cancellation: assigned companion/troops and ownership state agree
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### q.companion-skill

Alternative / best eligible skill

- Entry/action: Notable/lord conversation / quest journal; Alternative / best eligible skill
- Preconditions: Resolve actual hero's melee, Roguery and Tactics skills
- Expected: Best of those skills selected with required value 120
- State: Best of those skills selected with required value 120
- Side effects: No departure or troop mutation by this member
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Do not equate selected skill with full alternative admission
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@173-179
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.troop-tier

Alternative / troop tier

- Entry/action: Notable/lord conversation / quest journal; Alternative / troop tier
- Preconditions: Character tier resolved
- Expected: Tier 2 or above true; tier 1 false
- State: Tier 2 or above true; tier 1 false
- Side effects: Full men count is checked separately
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Tier boundary is inclusive
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@208-211
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## quests.005 Advance quest progress

Each entry retains authority and separate owner/observer observations in the CSV.

### quests.005.case-01

Advance quest progress / production action

- Entry/action: Notable/lord conversation / quest journal; Perform advance quest progress specifically in the production action context
- Preconditions: Isolated production action context for advance quest progress
- Expected: Acceptance requirement for production action: only relevant owned progress updates the quest
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.005.case-02

Advance quest progress / unrelated player action

- Entry/action: Notable/lord conversation / quest journal; Perform advance quest progress specifically in the unrelated player action context
- Preconditions: Isolated unrelated player action context for advance quest progress
- Expected: Acceptance requirement for unrelated player action: only relevant owned progress updates the quest
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### q.pay-enabled

Hideout / payment eligible

- Entry/action: Notable/lord conversation / quest journal; Hideout / payment eligible
- Preconditions: Correct leader/hideout conversation; gold at least stored price
- Expected: Payment clickable condition true
- State: Payment clickable condition true
- Side effects: Gold not deducted until consequence
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Gold equal to stored price qualifies
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@605-668
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.pay-insufficient

Hideout / payment unavailable

- Entry/action: Notable/lord conversation / quest journal; Hideout / payment unavailable
- Preconditions: Gold below stored stolen-goods price
- Expected: Payment clickable condition false; insufficient-funds reply available
- State: Payment clickable condition false; insufficient-funds reply available
- Side effects: No deduction, goods or payment flags from that reply
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: No extra credit path inferred
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@605-668
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.pay-commit

Hideout / payment consequence

- Entry/action: Notable/lord conversation / quest journal; Hideout / payment consequence
- Preconditions: Enabled payment option selected
- Expected: Stored price deducted; paying and has-goods flags true
- State: Stored price deducted; paying and has-goods flags true
- Side effects: Item roster insertion occurs in later keep-goods terminal member
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Do not use has-goods flag as proof of roster insertion
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@605-668
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.force-choice

Hideout / threaten force

- Entry/action: Notable/lord conversation / quest journal; Hideout / threaten force
- Preconditions: Force response chosen in applicable conversation
- Expected: Before-fight log added; fighting-for-goods true
- State: Before-fight log added; fighting-for-goods true
- Side effects: No goods awarded or battle victory by this consequence
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Battle entry and result remain distinct
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@605-668
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.battle-win

Battle / designated hideout won

- Entry/action: Notable/lord conversation / quest journal; Battle / designated hideout won
- Preconditions: Player side wins; event settlement is designated hideout
- Expected: Has-goods true; quick information shown
- State: Has-goods true; quick information shown
- Side effects: No terminal completion or roster insertion in this member
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Other hideout victory does not satisfy designated-hideout branch
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@833-847
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.other-party

Departure / unrelated party

- Entry/action: Notable/lord conversation / quest journal; Departure / unrelated party
- Preconditions: Departing party is not MainParty
- Expected: Immediate return
- State: Immediate return
- Side effects: No merchant conversation or offer flag change
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Co-op owner context must be traced separately
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@857-875
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.leave-hideout

Departure / hideout with goods

- Entry/action: Notable/lord conversation / quest journal; Departure / hideout with goods
- Preconditions: MainParty leaves designated hideout; has-goods true
- Expected: Merchant map conversation opens
- State: Merchant map conversation opens
- Side effects: No terminal reward before conversation choice
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: No has-goods means no conversation in this branch
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@857-875
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.leave-merchant-town

Departure / first counter-offer

- Entry/action: Notable/lord conversation / quest journal; Departure / first counter-offer
- Preconditions: MainParty leaves merchant settlement outside hideout branch; offer not given
- Expected: Merchant conversation opens; offer-given true
- State: Merchant conversation opens; offer-given true
- Side effects: Repeated departure suppressed by flag
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: The hideout branch takes precedence
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@857-875
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## quests.006 Quest time limit

Each entry retains authority and separate owner/observer observations in the CSV.

### quests.006.case-01

Quest time limit / before deadline

- Entry/action: Notable/lord conversation / quest journal; Perform quest time limit specifically in the before deadline context
- Preconditions: Isolated before deadline context for quest time limit
- Expected: Acceptance requirement for before deadline: expiration and outcome occur once for the correct quest
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.006.case-02

Quest time limit / expiration

- Entry/action: Notable/lord conversation / quest journal; Perform quest time limit specifically in the expiration context
- Preconditions: Isolated expiration context for quest time limit
- Expected: Acceptance requirement for expiration: expiration and outcome occur once for the correct quest
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.006.case-03

Quest time limit / offline owner

- Entry/action: Notable/lord conversation / quest journal; Perform quest time limit specifically in the offline owner context
- Preconditions: Isolated offline owner context for quest time limit
- Expected: Acceptance requirement for offline owner: expiration and outcome occur once for the correct quest
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### q.timeout

Timeout / hook

- Entry/action: Notable/lord conversation / quest journal; Timeout / hook
- Preconditions: OnTimedOut invoked
- Expected: Failure-timeout log appended
- State: Failure-timeout log appended
- Side effects: Terminal state comes from base quest lifecycle, not established by this hook
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: No reward/relation/power change in this hook
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@594-597
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## quests.007 Quest success

Each entry retains authority and separate owner/observer observations in the CSV.

### quests.007.case-01

Quest success / valid production completion

- Entry/action: Notable/lord conversation / quest journal; Perform quest success specifically in the valid production completion context
- Preconditions: Isolated valid production completion context for quest success
- Expected: Acceptance requirement for valid production completion: reward and finalization reach the owning player once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.007.case-02

Quest success / partial objective

- Entry/action: Notable/lord conversation / quest journal; Perform quest success specifically in the partial objective context
- Preconditions: Isolated partial objective context for quest success
- Expected: Acceptance requirement for partial objective: reward and finalization reach the owning player once
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### q.paid-keep

Terminal / pay and retain goods

- Entry/action: Notable/lord conversation / quest journal; Terminal / pay and retain goods
- Preconditions: Merchant keep branch selected with paying flag true
- Expected: Success; goods quantity added; Calculating XP +100
- State: Success; goods quantity added; Calculating XP +100
- Side effects: Giver power +5; merchant power -5; giver relation +10; merchant notable relations -3
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Payment was earlier; no second payment here
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@712-728
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.paid-return

Terminal / pay and return goods

- Entry/action: Notable/lord conversation / quest journal; Terminal / pay and return goods
- Preconditions: Merchant return branch selected with paying flag true
- Expected: Success; counter-offer gold; Calculating XP +150
- State: Success; counter-offer gold; Calculating XP +150
- Side effects: Both powers +5; giver relation +10; other notable relations +3
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: No item-roster insertion in this terminal member
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@730-746
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## quests.008 Quest failure

Each entry retains authority and separate owner/observer observations in the CSV.

### quests.008.case-01

Quest failure / objective failure

- Entry/action: Notable/lord conversation / quest journal; Perform quest failure specifically in the objective failure context
- Preconditions: Isolated objective failure context for quest failure
- Expected: Acceptance requirement for objective failure: failure consequences and quest removal converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.008.case-02

Quest failure / giver unavailable

- Entry/action: Notable/lord conversation / quest journal; Perform quest failure specifically in the giver unavailable context
- Preconditions: Isolated giver unavailable context for quest failure
- Expected: Acceptance requirement for giver unavailable: failure consequences and quest removal converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.008.case-03

Quest failure / rival state

- Entry/action: Notable/lord conversation / quest journal; Perform quest failure specifically in the rival state context
- Preconditions: Isolated rival state context for quest failure
- Expected: Acceptance requirement for rival state: failure consequences and quest removal converge
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### q.battle-loss

Battle / losing side

- Entry/action: Notable/lord conversation / quest journal; Battle / losing side
- Preconditions: Player side differs from winner side
- Expected: FailQuestByLosingHideoutBattle called
- State: FailQuestByLosingHideoutBattle called
- Side effects: Loss branch precedes designated-hideout comparison
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: This handler does not first test which hideout was lost
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@833-847
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## quests.009 Quest betrayal or cancellation

Each entry retains authority and separate owner/observer observations in the CSV.

### quests.009.case-01

Quest betrayal or cancellation / dialogue choice

- Entry/action: Notable/lord conversation / quest journal; Perform quest betrayal or cancellation specifically in the dialogue choice context
- Preconditions: Isolated dialogue choice context for quest betrayal or cancellation
- Expected: Acceptance requirement for dialogue choice: accepted terminal state is applied once with correct consequences
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.009.case-02

Quest betrayal or cancellation / invalid terminal request

- Entry/action: Notable/lord conversation / quest journal; Perform quest betrayal or cancellation specifically in the invalid terminal request context
- Preconditions: Isolated invalid terminal request context for quest betrayal or cancellation
- Expected: Acceptance requirement for invalid terminal request: accepted terminal state is applied once with correct consequences
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### q.unpaid-keep

Terminal / unpaid goods retained

- Entry/action: Notable/lord conversation / quest journal; Terminal / unpaid goods retained
- Preconditions: Merchant keep branch selected with paying flag false
- Expected: Betrayal; goods quantity added; Calculating XP +100
- State: Betrayal; goods quantity added; Calculating XP +100
- Side effects: Both powers -5; giver relation -5; merchant relations -3
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inventory reward does not make this a success
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@748-763
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.unpaid-return

Terminal / unpaid goods returned

- Entry/action: Notable/lord conversation / quest journal; Terminal / unpaid goods returned
- Preconditions: Merchant return branch selected with paying flag false
- Expected: Betrayal; counter-offer gold; Honor XP +100
- State: Betrayal; counter-offer gold; Honor XP +100
- Side effects: Giver power -5; merchant power +5; giver relation -5; other notable relations +3
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Gold reward accompanies betrayal
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@765-780
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

### q.capture-cancel

Ownership / player capture

- Entry/action: Notable/lord conversation / quest journal; Ownership / player capture
- Preconditions: Giver settlement changed; newOwner equals MainHero
- Expected: Quest cancelled with ownership-change log
- State: Quest cancelled with ownership-change log
- Side effects: No terminal reward in handler
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Other new owners do not satisfy this predicate
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@825-831
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## quests.010 Quest journal

Each entry retains authority and separate owner/observer observations in the CSV.

### quests.010.case-01

Quest journal / new log

- Entry/action: Notable/lord conversation / quest journal; Perform quest journal specifically in the new log context
- Preconditions: Isolated new log context for quest journal
- Expected: Acceptance requirement for new log: attempt to enter vanilla quest journal is blocked by the current QuestsState gate; private journal/progress support remains unverified
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support disabled by QuestsState gate
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json), [source/GameInterface/Services/UI/Patches/GameUIDisable.cs](../../source/GameInterface/Services/UI/Patches/GameUIDisable.cs)

### quests.010.case-02

Quest journal / progress

- Entry/action: Notable/lord conversation / quest journal; Perform quest journal specifically in the progress context
- Preconditions: Isolated progress context for quest journal
- Expected: Acceptance requirement for progress: attempt to enter vanilla quest journal is blocked by the current QuestsState gate; private journal/progress support remains unverified
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support disabled by QuestsState gate
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json), [source/GameInterface/Services/UI/Patches/GameUIDisable.cs](../../source/GameInterface/Services/UI/Patches/GameUIDisable.cs)

### quests.010.case-03

Quest journal / completed log

- Entry/action: Notable/lord conversation / quest journal; Perform quest journal specifically in the completed log context
- Preconditions: Isolated completed log context for quest journal
- Expected: Acceptance requirement for completed log: attempt to enter vanilla quest journal is blocked by the current QuestsState gate; private journal/progress support remains unverified
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support disabled by QuestsState gate
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json), [source/GameInterface/Services/UI/Patches/GameUIDisable.cs](../../source/GameInterface/Services/UI/Patches/GameUIDisable.cs)

## quests.011 Alternative solution return

Each entry retains authority and separate owner/observer observations in the CSV.

### quests.011.case-01

Alternative solution return / duration

- Entry/action: Notable/lord conversation / quest journal; Perform alternative solution return specifically in the duration context
- Preconditions: Isolated duration context for alternative solution return
- Expected: Acceptance requirement for duration: returned roster, companion and rewards reach the owner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.011.case-02

Alternative solution return / troop loss

- Entry/action: Notable/lord conversation / quest journal; Perform alternative solution return specifically in the troop loss context
- Preconditions: Isolated troop loss context for alternative solution return
- Expected: Acceptance requirement for troop loss: returned roster, companion and rewards reach the owner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.011.case-03

Alternative solution return / companion recovery

- Entry/action: Notable/lord conversation / quest journal; Perform alternative solution return specifically in the companion recovery context
- Preconditions: Isolated companion recovery context for alternative solution return
- Expected: Acceptance requirement for companion recovery: returned roster, companion and rewards reach the owner
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### q.alternative-success

Alternative / completion rewards

- Entry/action: Notable/lord conversation / quest journal; Alternative / completion rewards
- Preconditions: Actual alternative completion consequence
- Expected: RewardGold and goods; Calculating XP +50; relationship change field +10
- State: RewardGold and goods; Calculating XP +50; relationship change field +10
- Side effects: Giver power +5; merchant power -5; merchant notable powers -3
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Companion return/XP and application of relation field remain base lifecycle
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@186-201
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## quests.012 Quest persistence

Each entry retains authority and separate owner/observer observations in the CSV.

### quests.012.case-01

Quest persistence / save/reload

- Entry/action: Notable/lord conversation / quest journal; Perform quest persistence specifically in the save/reload context
- Preconditions: Isolated save/reload context for quest persistence
- Expected: Acceptance requirement for save/reload: ownership and progress resume without replayed rewards
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.012.case-02

Quest persistence / reconnect

- Entry/action: Notable/lord conversation / quest journal; Perform quest persistence specifically in the reconnect context
- Preconditions: Isolated reconnect context for quest persistence
- Expected: Acceptance requirement for reconnect: ownership and progress resume without replayed rewards
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.012.case-03

Quest persistence / alternative in progress

- Entry/action: Notable/lord conversation / quest journal; Perform quest persistence specifically in the alternative in progress context
- Preconditions: Isolated alternative in progress context for quest persistence
- Expected: Acceptance requirement for alternative in progress: ownership and progress resume without replayed rewards
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### q.load

Load / registration restore

- Entry/action: Notable/lord conversation / quest journal; Load / registration restore
- Preconditions: Existing quest initializes on load
- Expected: Dialogs and menu options registered
- State: Dialogs and menu options registered
- Side effects: No StartQuest in this member
- Authority: Actual owner-scoped production action; vanilla MainHero/MainParty context must not be a server hero/party. Co-op routing needs separate trace
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Observe restored availability without accepting twice
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: source-interpreted; runtime unrun; support allowlisted-source-only
- Source span: TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior@561-565
- Remaining: Outcome interpreted from complete member; exact live subject, co-op producer/apply path, independent measurements and both clients remain unverified

Sources: [features/baseline/behavior-sources.json](../../features/baseline/behavior-sources.json)

## quests.013 Debug issue grant catalog

Each entry retains authority and separate owner/observer observations in the CSV.

### quests.013.case-01

Debug issue grant catalog / listed

- Entry/action: Notable/lord conversation / quest journal; Perform debug issue grant catalog specifically in the listed context
- Preconditions: Isolated listed context for debug issue grant catalog
- Expected: Acceptance requirement for listed: debug grant availability is reported separately from normal quest support
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.013.case-02

Debug issue grant catalog / wired

- Entry/action: Notable/lord conversation / quest journal; Perform debug issue grant catalog specifically in the wired context
- Preconditions: Isolated wired context for debug issue grant catalog
- Expected: Acceptance requirement for wired: debug grant availability is reported separately from normal quest support
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.013.case-03

Debug issue grant catalog / unwired

- Entry/action: Notable/lord conversation / quest journal; Perform debug issue grant catalog specifically in the unwired context
- Preconditions: Isolated unwired context for debug issue grant catalog
- Expected: Acceptance requirement for unwired: debug grant availability is reported separately from normal quest support
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)

### quests.013.case-04

Debug issue grant catalog / normal gate disabled

- Entry/action: Notable/lord conversation / quest journal; Perform debug issue grant catalog specifically in the normal gate disabled context
- Preconditions: Isolated normal gate disabled context for debug issue grant catalog
- Expected: Acceptance requirement for normal gate disabled: debug grant availability is reported separately from normal quest support
- State: Relevant subject state before and after this single action
- Side effects: Inventory every triggered effect; incidental effects remain unresolved
- Authority: Client originates player input; authoritative shared-world changes belong to the server
- Owner observation: Read the addressed subject on the owning client; retain before/after values
- Other client: Read permitted shared state on the other real client; do not require private UI to match
- Boundary/negative: Inspect the actual guard and a nonqualifying input; no accepted mutation may be inferred from an acknowledgment
- Persistence: Save/reload and reconnect are separate required cases when this changes durable state
- Evidence: candidate; runtime unrun; support unverified
- Source span: exact installed member unresolved
- Remaining: This is one explicitly named coverage requirement, not a source-derived outcome; resolve its exact conditions and effects

Sources: [source/GameInterface/Services/Issues](../../source/GameInterface/Services/Issues), [source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs](../../source/GameInterface/Services/Issues/Patches/DisableAllIssueBehaviorsExceptAllowlist.cs), [features/baseline/installed.json](../../features/baseline/installed.json)
