# Lord Needs a Tutor: live handoff

All live flows below are pending. Run only after the supervisor assigns a live lane, with the draft PR head on the dedicated server and two clients. Automated E2E results are not live evidence.

Source: installed Native v1.4.8, `TaleWorlds.CampaignSystem.dll` SHA-256 `1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE`. Whole-type decompilations inspected: `LordsNeedsTutorIssueBehavior`, `IssueBase`, `QuestBase`, `QuestManager`, `CampaignEventDispatcher`. Member names in this table refer to those types.

The framework owns acceptance races, per-player similar commitments, owner resolution, branch dispatch and completion replay. Tutor adds pupil selection, acceptance side effects, progress and dialogue proofs, world callbacks, pupil restrictions and personal journal updates. The server mirror is never started with `StartQuest`; only the owner has a running quest in the quest manager. Quest trait XP follows the framework's explicit current policy: it is skipped rather than applied to the shared server trait store (`Framework/Rewards/QuestTraitXpPatches`). No Tutor-specific trait layer is added.

## Setup and observations

Use the server's `coop.debug.issues.list_types` and `coop.debug.issues.give` with `LordsNeedsTutorIssue`. Debug creation uses vanilla `ConditionsHold` to find a suitable pupil; it does not make an unsuitable pupil eligible. Look up the actual giver/pupil names and registry IDs in the save. An eligible NPC clan and pupil are save-specific, so resolve them before recording a run.

Use Danustica (`town_ES1`, town `town_comp_ES1`) for settlement conversations where the selected nobles can be placed there through existing server tools. Inspect its hall with `coop.debug.location.list` and `coop.debug.location.list_characters`; the hall identity is `Location_town_ES1_lordshall`. Also meet a roaming giver through a real map conversation.

Server skill changes can use `coop.debug.hero_developer.add_skill_xp` with the resolved pupil name or ID, a real skill such as `OneHanded`, and a measured XP amount. Record the baseline and resulting total skill points; XP is not the same as gained skill points. Use normal battles as a separate training case. State-changing commands run only on the server.

For every row retain server and both clients' before/after pupil clan, companion/party/prisoner membership, developer identity, all focus values, skill totals, quest IDs, due times and journals. Record each player's inventory, gold, renown and relations. Capture both clients' relevant dialogue and journal screens. Verify no `Failed to get id` or duplicate pupil and that the other player's personal quest/rewards remain unchanged.

## Required flows

| Source member | Action / distinct cases | Expected server and clients |
| --- | --- | --- |
| `ConditionsHold`, `SuitableCondition` | Suitable adult male pupil in an NPC clan; unsuitable age, level, female, busy party, prisoner, previously chosen, player clan; minor-faction/young giver | Server generates only eligible offers; all clients use its same pupil and issue ID. |
| `IssueStayAliveConditions` | Pupil ages out, becomes unavailable or joins a player clan before acceptance; spouse with/without children | Stale offer is refused/removed without attaching a pupil. |
| `CanPlayerTakeQuestConditions` | Relation below/at -10, clan below/at tier 2, kingdom leader, war, companion limit full/free | Native refusal applies to the requester. No pupil/focus/reward mutation on refusal. |
| `IssueBase` dialogue | Decline, accept, competing accepts of one giver, same player asks a second giver | Decline changes nothing; one owner per issue; framework refuses that player's second Tutor quest. |
| Framework accept coordinators | Two players accept different givers concurrently | Both get their own copy, pupil and journal; each sees only their own quest dialogues. |
| `QuestAcceptedConsequences` | Accept from settlement dialogue and roaming map giver | One pupil joins the owner's clan/party, each focus gains one, owner log starts at 0/30. Server mirror has no started listeners or player party. |
| `GetYoungHeroFirstDialogFlow` | Both friendly/cautious introductory answers | Only the owner sees the pupil introduction; server records completion once. |
| `SetDialogs`, `GetYoungHeroSecondDialogFlow` | Both giver discussion answers; pupil discussion before/after 15 skill points; each of the six attribute commentary groups | Correct dialogue and progress text; no reward or premature completion. |
| `OnHeroGainedSkill` | 1, 15, 29, 30, over 30 gained points; skill loss; another hero gains skill | Owner log clamps to 0..30 and becomes ready at 30; other quest untouched. |
| `PupilJoinMeCondition`, rejoin consequence | Separate and find pupil in a settlement, rejoin, repeat choice | Only owner can rejoin; exactly one pupil in the owner's roster. |
| `QuestCompletedWithSuccessAfterConversation` | Early attempt; legitimate completion with 2, 3 and 4 jewelry; repeat completion | Early request changes nothing. Success grants one gift and +5 renown, base +25 giver relation and +15 pupil relation to owner; other player's balances unchanged. |
| `OnFinalize` | Success, timeout, fail and cancellation cleanup | Living pupil leaves companion/party, original clan restored unless marriage requires retaining it; focus bonuses removed once. Spouse/children follow the pupil's final clan where vanilla requires. |
| `OnTimedOut`, `QuestManager.HourlyTick` | Before/equal/after due time; free map vs settlement/encounter/battle | Strictly past deadline ends the quest, -10 giver relation, failure journal; map farewell only in a safe owner map state. Server never opens a conversation. |
| `OnHeroKilled` | Pupil dies | Fail journal, -40 giver relation, no gift; no dereference of the dead developer during cleanup. |
| `OnCompanionRemoved` | Pupil dismissed | Fail journal and cleanup once; unrelated companion removal leaves it running. |
| `OnClanLeaderChanged` | Pupil becomes a clan leader | Failure journal and cleanup. |
| `OnHeroesMarried` | Owner marries pupil; unrelated marriage | Owner marriage cancels while preserving marriage clan membership; unrelated marriage does not cancel. |
| `OnWarDeclared`, `OnClanChangedKingdom` | Owner-caused hostility versus external war; faction changes into/out of war | Native fail/cancel distinction and correct journal/relations for that owner. |
| `OnMapEventStarted` | Force supplies and force recruits from a village owned by the giver's clan; repeat in an unrelated village | Related village fails only the acting owner's quest, -5 giver relation and -10 giver power, then pupil cleanup. Unrelated village leaves both quests active. Honor XP follows the framework policy above. |
| `OnPrisonerTaken` | Owner captured with pupil participating; unrelated player captured | Participating pupil escapes per vanilla; other player's capture does not drive this quest. |
| `OnNewCompanionAdded`, `OnPerkReset`, `CheckCompanionLimit` | Owner exceeds companion limit; owner's perk reset reduces it; another player's limit changes | Cancel only where the owner's actual companion count exceeds their limit. |
| `OnSettlementEntered`, `OnGameLoadFinished` | Pupil without a party in Danustica; enter hall and reload/reconnect | Correct hall placement and dialogue on owner, no observer quest dialogue or duplicate location character. |
| `CommonRestrictionInfoIsRequested`, `QuestBase` | Query equipment, party leadership, party role/governor, marriage and issue permissions | Vanilla pupil restrictions hold on server and clients despite the server mirror not being started. |
| `QuestManager` succession / `PlayerHeirSelectionCompleted` | Owner character replacement, other player's replacement | Cancels replaced owner's agreement with cleanup; other player's quest continues. |
| `ActiveIssueDismisser`, `IssueBase.IssueFinalized` | Owner disconnects, observer disconnects, server reloads an active agreement | Current framework cancels the departed owner's quest and dismisses restored quests at server startup. Pupil returns and focus bonuses are removed; observer disconnect leaves the owner's quest running. Reconnect does not restore a dismissed quest. |

## Inapplicable solutions and effects

`LordsNeedsTutorIssue.IsThereAlternativeSolution` and `IsThereLordSolution` are both false: no companion/troop delegation, lord solution or alternative returns. The source exposes no bribery, persuasion, counter-offer, delivery inventory requirement or quest-created mobile party. Jewelry reward selection is random; no gold reward is granted by the success method. There is no quest-specific raid or settlement-owner callback. World changes matter only through the callbacks listed above.
