# Focused stolen-goods issue branches

Focused installed-source expansion for the currently allowlisted
`TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior`.
Every row is source-inspected only; none was played. Owning assembly: `TaleWorlds.CampaignSystem.dll`,
SHA-256 `1f8e33e2ed73e6ec653d7629180afb70649ddc6e5bd1657a802a264efda1c3ae`, Native `v1.4.8`.
The artifact hash/range is in [installed-sources.json](evidence/installed-sources.json).
Lines refer to this decompiled type. No other quest inherits this branch coverage.

The [source-case table](source-cases.csv) contains 31 interpreted cases for
this issue, including generation versus survival thresholds, admission, alternative eligibility,
payment, force, designated battle result, settlement departure and load hooks. Each case carries
its own complete-member span and remaining co-op/runtime work.

The observations paraphrase complete inspected vanilla members. Co-op owner routing,
replacement patches and actual replication require separate evidence. Vanilla local-player
references are not a playable server hero/party; the co-op host has neither.

| Slice | Complete inspected member / lines | Required side effects / independent question |
| --- | --- | --- |
| acceptance eligibility | `CanPlayerTakeQuestConditions`, 281-301 | reject relation below -10, war with giver's settlement faction, or player's clan owning settlement; distinguish eligible neutral case |
| acceptance side effects | `QuestAcceptedConsequences`, 552-559 | quest starts, hidden initial log, relevant hideout spotted/visible and tracked, all for the actual owner |
| giver conversation | `SetDialogs`, 533-550 | offer/discussion use actual quest giver; merchant/hideout flows registered; dialogue entry alone is not acceptance |
| merchant counter-offer | `GetCounterOfferDialogFlow`, 575-592 | correct NPC and not-yet-talked/not-yet-held-goods/not-yet-offered flags; acceptance/refusal require real dialogue |
| timeout hook | `OnTimedOut`, 594-597 | adds timeout log; terminal state must also be established from quest lifecycle |
| pay and retain goods | `SucceedQuestByPayingAndKeepingTheGoods`, 712-728 | success, goods/quantity added, Calculating XP +100, giver power +5, merchant power -5, giver relation +10, merchant relations -3 |
| pay and return goods | `SucceedQuestByPayingAndGivingTheGoodsBack`, 730-746 | success, counter-offer gold, Calculating XP +150, both powers +5, giver relation +10, other notable relations +3 |
| betray and retain goods | `FailQuestByKeepingTheGoods`, 748-763 | betrayal, goods added, Calculating XP +100, both powers -5, giver relation -5, merchant relations -3 |
| betray and return goods | `FailQuestByGivingBackTheGoods`, 765-780 | betrayal, counter-offer gold, Honor XP +100, giver power -5, merchant power +5, giver relation -5, other notable relations +3 |
| lose hideout fight | `FailQuestByLosingHideoutBattle`, 782-788 | failure, both powers -5, giver relation -5; battle casualties/party recovery remain separate |
| alternative success | `AlternativeSolutionEndWithSuccessConsequence`, 186-201 | configured reward gold/goods, issue-owner relation change +10, giver power +5, counter-offer power -5, merchant power -3, Calculating XP +50 |

The four pay/unpaid keep/return cases also bind `GetAfterHideoutMerchantDialogFlow`, 670-710,
which dispatches the selected branch using `_isPayingForGoods`. The meeting case binds both
its condition, 882-895, and conversation consequence, 877-880.
Earlier payment and goods handling are separate flow actions, not inferred from terminal
methods. Runtime amounts/goods, affected notable set, giver, quest ID, owner hero/party and
hideout must be read from the actual quest/registry before action and retained in evidence.
Do not substitute fabricated gold or roster values. Companion/troop return is separate from
the alternative-success member above. Debug completion does not exercise every normal branch.

Remaining work includes caller/transition ordering, actual companion/troop counts and return,
owner versus other-client context, interrupted/repeated actions, complete delegate/dialogue
coverage, persistence/reconnect and indirect lifecycle effects. The other 42 registered issues
remain disabled by the normal source gate and their branches are not interpreted terminal-result
oracles. The [gate recipe](recipes/quest-gate.md) covers admission only. Full quest acceptance
still needs the remaining source interpretation and both real clients' action/state/side-effect
evidence for every required slice.
