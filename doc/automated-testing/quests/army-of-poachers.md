# Army of Poachers

Live verification is pending. No game was launched for this port.

The installed Native module is v1.4.8. `TaleWorlds.CampaignSystem.dll` has SHA-256
`1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE`.
The complete `MerchantArmyOfPoachersIssueBehavior`, `IssueBase`, `QuestBase`,
`Helpers.QuestHelper` and `CampaignEventDispatcher` were decompiled into the
worktree's ignored `artifacts/poachers` directory before implementation.
The member names below refer to that installed source.

## Expected flows for the assigned live lane

Use a dedicated server and two clients. Danustica is `town_ES1`, in the Southern
Empire; Polisia is its bound village `village_ES1_2`. Use the actual village in
the accepted issue's journal if vanilla selects a different bound village.
All setup and world changes originate on the server. Accept, decline, dialogue,
persuasion and combat must use the owning client's normal UI.

| Flow | Installed source | Expected server and client result |
| --- | --- | --- |
| Offer, decline, direct accept | Behavior `ConditionsHold`; Issue `CanPlayerTakeQuestConditions`, `IssueStayAliveConditions`; Quest `QuestAcceptedConsequences` | Merchant offer names the selected unraided village. Declining leaves it available. Accepting gives one started journal to the owner, one unstarted quest object to the server, and none to the other player. Poor relation, war, a raided village and security above 90 refuse acceptance. Vanilla also requires 15 healthy troops in dialogue. |
| Concurrent owners and duplicate refusal | Framework `IssueAcceptPreconditions`, `SimilarCommitmentsGatePatch` | Two clients accept different givers' copies. Each sees their own journal and rewards. The framework refuses a second copy for the same player. There is no quest-specific duplicate gate. |
| Village progress | Quest `HourlyTick`, `GameMenuOpened`; Behavior `army_of_poachers_village_on_init` | Daytime does not open the quest menu. Entering or waiting in the selected village at night does. The server creates one registered hostile party with the rolled leather and troops. Only the owner's quest binds that party. Reopening the menu creates no duplicate. Wounded owners cannot fight or negotiate. While the server party is unavailable, Back remains available, including a request received after dawn. |
| Persuasion success | Quest `GetPoacherPartyDialogFlow`, `AddPersuasionDialogs`, `QuestSuccessPlayerComesToAnAgreementWithPoachers` | Complete the real persuasion. Owner gains the quest gold, +5 base giver relation and +1 renown. Giver gains 30 power; town gains 50 prosperity and loses 5 security. Server removes the party. Owner receives the conclusion log and the journal closes; the other player's personal state stays unchanged. |
| Persuasion failure and later fight | Quest `persuasion_failed_with_poachers_on_condition`, `persuasion_rejected_with_poachers_on_consequence` | Exhausted options or critical failure return to the poachers' opening dialogue; persuasion cannot be retried. The quest stays active and the owner can still fight or leave them alone. Exercise each of the five persuasion choices where its skill/trait roll differs. |
| Leave the poachers alone | Quest `QuestFailedAfterTalkingWithPoachers` | Choosing to leave runs one server failure: no gold, -5 base giver relation, -50 giver power, -30 town prosperity and -5 security. Conclusion log belongs to the owner; party is removed. |
| Direct attack and attack after talking | Quest `StartQuestBattle`; Behavior `engage_poachers_consequence`; shared encounter/mission coordinators | Both entries wait for encounter approval, create the same authoritative battle and open the selected village's `village_center` scene. Nearby AI parties do not reinforce. Verify party/map-event IDs on all three peers. |
| Owner combat victory | Quest `QuestSuccessWithPlayerDefeatedPoachers`; co-op `PoachersQuestEventPatches` | Fight the actual mission. Owner receives quest gold and +5 base giver relation; giver gains 30 power and town gains 50 prosperity. No additional security penalty or quest renown. Leather follows the ordinary battle-loot path. Quest and party are cleaned up on all peers. |
| Owner defeat, retreat or unresolved battle | Quest `QuestFailWithPlayerDefeatedAgainstPoachers`; co-op `PoachersQuestEventPatches` | Owner receives no quest gold; -5 base relation, -50 giver power, -30 prosperity and -5 security. As in vanilla, a defeated owner is taken prisoner by the poachers and is released when the quest fails, with no Roguery XP from the release. Another player's captivity is unchanged. |
| Another player fights the party | Co-op `PoachersQuestEventPatches.BattleFinalizingPrefix` | Defeating the party without its owner cancels that owner's quest without paying them. If the poachers win, the owner's quest stays active. Another player's separate copy is unchanged. |
| Timeout | Quest `OnTimedOut`; framework timeout dispatch | Expire the 20-day direct quest: one server penalty matching combat defeat, one owner conclusion log and party cleanup. Client replay does not apply penalties again. |
| Village raid | Quest `MapEventCheck`, `QuestCanceledTargetVillageRaided` | A raid starting or ending at the selected village fails the quest with its raid log, without the combat failure's extra reward/penalty effects. Other villages do not end it. |
| War and clan change | Quest `OnWarDeclared`, `OnClanChangedKingdom`; `QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest` | War against the giver's faction fails the quest if caused by the player, otherwise cancels it. Joining a hostile kingdom cancels it. Unrelated wars and clan changes leave it active. |
| Village coercion | Quest `MapEventStarted`; `QuestHelper.CheckMinorMajorCoercion`, `ApplyGenericMinorMajorCoercionConsequences` | Owner forcing supplies/volunteers from a settlement protected by the giver fails the quest, changes relation by -5 and giver power by -10. Vanilla counts a village as protected only when the giver is in its owner clan or among its notables, which a town merchant normally is not, so this is not expected to fire live. Another player's coercion does not fail this quest. |
| Companion start, success, failure and return | Issue `AlternativeSolutionCondition`, `GetAlternativeSolutionSkill`, `AlternativeSolutionEndWithSuccessConsequence`, `AlternativeSolutionEndWithFailureConsequence`; inherited `IssueBase` completion | Real picker accepts tier-2-or-better healthy troops and a companion using the best of Bow/Crossbow/Throwing (150). Server rolls duration, failure and casualties once. Success gives the owner's gold and +5 base relation, +30 giver power and +50 prosperity. Failure gives no gold, -5 relation, -50 power, -30 prosperity and -5 security. Verify surviving/wounded troops, companion, casualties, XP and troop returns belong only to the owner, and that the owner's journal shows "Your men should be on their way." once. Decline/cancel selection and an invalid roster leave the offer available and return selected troops. |
| Reconnect, save/load and player replacement | Framework ownership snapshot and resolution; inherited quest/issue lifecycle | Reconnect both clients during direct and companion progress. Confirm owner journal, target party, outcomes and returns retain identity. An owner who leaves after the poachers appear has the quest dismissed, and the server removes the poachers party. Exercise giver death/issue transfer and player replacement; retain any framework limitation as an unverified failure. |

No lord solution or bribe is offered: the issue returns `IsThereLordSolution =
false`, and the poacher dialogue offers persuasion, combat and leaving only.
Village ownership changes and ordinary captivity have no separate listener in
this quest; their resulting war, raid or timeout paths above remain applicable.
Trait XP remains disabled by the framework because vanilla shares one trait
store between players, including the `OnIssueFailed` entry used by this quest.

For every live flow retain before/after journals, gold, renown, giver relation,
power, town security/prosperity, party IDs and cleanup on the server and both
clients. Record the actual tested source and screenshots; automated harness
results do not establish that these live flows passed.
