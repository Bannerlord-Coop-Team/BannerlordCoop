# Deliver the Herd framework port (#3763, PR #3796)

Live testing is pending. This port uses the framework from development at 7743a0b05; it does not reuse the legacy system's test results.

## Installed source

The complete installed `HeadmanNeedsToDeliverAHerdIssueBehavior`, `IssueBase`, `QuestBase`, `IssueManager`, `QuestManager` and `QuestHelper` were decompiled into `artifacts/3763-framework/` for this port. Owning assembly: `TaleWorlds.CampaignSystem.dll`, SHA-256 `1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE`, file version `1.0.0.0`. Installed Native module version: `v1.4.8`.

## Live flows to run

Use independent issues for mutually exclusive endings. Record exact giver, recipient, settlement and animal IDs from the real save. For a destination in Danustica, the settlement ID is `town_ES1`; use the actual generated recipient, not a guessed hero ID. Inspect server and both clients before and after each action.

| Flow | Production source | Expected server result | Expected clients |
| --- | --- | --- | --- |
| Offer, decline and leave discussion | Issue dialogue and `Quest.SetDialogs` | Declining leaves the issue available and creates no herd | No new personal journal, inventory or reward |
| Accept personally | `Quest.QuestAcceptedConsequences`, framework accept coordinator | Owns one unstarted quest copy; adds requested animals and five grain only to the accepting party | Only accepter starts the quest and journal; both receive the same authoritative inventory |
| Accept from a map conversation | `IssueBase.StartIssueWithQuest` | Same owner and inventory as settlement acceptance | Dialogue completes and accepter starts the quest; no legacy conversation tracker is required |
| Two players accept different herd issues | Framework `IssueAcceptPreconditions` | Each player owns one copy; a second copy for the same player is refused | Each sees only their own journal; ending one leaves the other untouched |
| Real eligibility refusals | `Issue.IssueStayAliveConditions`, `CanPlayerTakeQuestConditions` | Refuses war with giver, relation below -10, raided village, inactive recipient or bound-town security above 70 | No accepted quest or grant from a refused request |
| Generation variants | `Issue` constructor | One recipient, destination and sheep/cow/hog type; fallback is the village's bound town | Replicas receive those exact identities; no host party is needed |
| Full delivery, surplus and modified animals | `Quest.DeliverHerdOnConsequence`, `OnCompleteWithSuccess` | Requires destination and full count; transfers the requested number to the town, pays quoted reward, giver power +5, village hearth +50, destination prosperity +50, base relation +5 | Inventory and gold agree with server; owner gets success journal; other player's personal state stays unchanged |
| Insufficient herd or 'no animals this time' | `Quest.GetDeliveryDialogFlow` | No success, payment or inventory transfer | Quest stays active; there is no partial hand-in |
| Keep the herd | `Quest.DeliverHerdRejectOnConsequence`, `OnFailed` | Owner keeps animals; crime +20, giver power -5, destination prosperity -10, base relation -10 | Failure journal only on owner; no duplicate crime or reward |
| Timeout with partial and surplus herd | `Quest.OnTimedOut`, `RemoveRemainingHorses` | Removes up to requested count, giver power -5, prosperity -10, base relation -5 | Owner gets timeout journal; removal is applied once, personal surplus remains |
| Recipient death or departure | `Quest.OnHeroKilled` | Cancels and removes remaining quest animals from owner | Owner's journal ends; other player's quest remains active |
| Giver death | `IssueManager.OnHeroKilled` | Cancels in owner's context, cleans their animals | Ends only the matching personal journal |
| Player-caused war, other war and kingdom change | `Quest.OnWarDeclared`, `OnClanChangedKingdom`, `QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest` | Runs fail/cancel as each affected owner; unrelated players remain active | Correct outcome and inventory, with no local consequence rerun |
| Force supplies or recruits at giver's village | `Quest.OnMapEventStarted`, `QuestHelper.CheckMinorMajorCoercion` | Only the attacking owner's quest fails; normal failure effects plus vanilla coercion relation/power penalties | Owner's quest ends; other player's state stays unchanged |
| Companion selection, picker cancellation and invalid troops | `IssueBase.StartIssueWithAlternativeSolution`, framework troop validator | Accepts valid companion with tier-2-or-better troops, or refuses and returns selection through framework | Owner alone gets companion journal; no personal herd is granted |
| Companion completion, wounds/XP/reward and troop return | `IssueBase.CompleteIssueWithAlternativeSolution`, `AlternativeSolutionEndWithSuccess` | Server rolls and completes once as owner; pays owner, giver power +5, relation +5, hearth +50; returns companion and troops | Both see returned troops and owner gold; bystander gets no personal journal or reward |
| Save/reload and owner disconnect/reconnect | Framework ownership save and active-issue dismissal | The framework cancels personal quests on disconnect or server restart; remaining quest animals are removed once | Reconnecting does not revive or grant the canceled herd; bystander remains unaffected |
| Character replacement or player removal | Framework lifecycle plus quest cancellation | Confirm old quest cleanup uses its original owner's inventory | No retained herd quest from replaced character and no bystander cleanup |

## Source limits and shared framework behavior

`IsThereLordSolution` is false. There are no counter-offers, price negotiation, persuasion, bribes, combat resolution or partial hand-ins. The quest's alternative scale flag is Duration only, so ordinary companion completion has no failure-risk or casualty scaling; vanilla can still wound returned troops and award XP. A forced failure test exercises inherited failure handling, not an ordinary player choice.

The shared framework currently suppresses quest trait XP for every supported quest (`QuestTraitXpPatches`); this port follows it and does not claim personal Honor rewards. Shared duplicate/similar-commitment gating remains in the framework. Herd-specific vanilla eligibility is not replaced or bypassed.

The old map-conversation tracker was deleted with the legacy system. Acceptance now sends directly from the postfix on `IssueBase.StartIssueWithQuest`, without requiring a tracked conversation.