# Village Needs Grain Seeds

Issue #3762 uses `Issues/Framework` and `Quests/HeadmanNeedsGrain`. No legacy quest layer is required.
The server owns consequences. The accepting client starts the real quest and owns its dialogue and
journal. A second player can accept a separate giver's issue; the framework refuses a second copy
for the same player.

## Installed source

The implementation was checked against full decompilations of `HeadmanNeedsGrainIssueBehavior`,
`IssueBase`, `QuestBase`, `IssueManager`, `IssuesCampaignBehavior`, `CampaignEventDispatcher`,
`TownMarketData`, `JournalLog`, `LogEntry` and `TraitLevelingHelper` from the installed
`TaleWorlds.CampaignSystem.dll`, SHA-256
`1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE`.
The installed SandBox module version is `v1.4.8`.
The local decompilations are retained under ignored `artifacts/3762-*.cs`.

## Decisions

- Inventory exchanges do not raise the vanilla player event in co-op. Recount grain when the
  server's item changes reach the owner's roster, including clear, food consumption and sharing.
- A controlled player must not resolve an unaccepted issue through the AI-lord completion path.
  Real AI lords retain that path.
- The replacement framework waits until the owning player's troops can return before completing
  an alternative solution. Vanilla then restores the roster and activates the companion on the
  server. There is no separate client return inquiry or replacement return queue.
- Keep vanilla's live, per-player difficulty estimate until acceptance. Preserve the raw issue
  multiplier at creation; the framework freezes the accepting player's multiplier for both solutions.
- Direct delivery pays zero gold. Keep the vanilla renown, relations, power and prosperity changes.
  Grain trait XP is kept by hero and sent to its owner, without using the server's shared trait store.
- Crime can be reported without an active map event. Keep vanilla penalties and use the giver's
  village for the log when there is no event settlement.
- The new accept path has no conversation-tracker precondition. The old map-conversation tracking
  cherry-pick targets a deleted file and is unnecessary here.
- Retain the framework's existing disconnect/restart dismissal policy. Active direct quests do not
  resume after their owner disconnects. Server restart discards unowned companion missions after
  freeing their companions; ordinary sent troops are not restored by that shared policy. This port
  does not add a separate persistence lifecycle or claim active-quest resumption.

## Automated checks

`HeadmanNeedsGrainTests` uses the existing `QuestScenario`, real grain issue preconditions, real
quest objects and `StartQuest()`. Missing encyclopedia/UI text infrastructure is replaced only in
the test environment. No real issue refusal is stubbed. Alternative outcomes are selected by the
failure-chance fixture while the production completion, consequences and return path execute.

Run the Release solution build, GameInterface tests and the Issues E2E tests through the shared
build gate with node reuse off and `-p:ModName=`. Nothing is deployed to the game install.

## Live handoff

All live rows below are **pending**. Automated tests are not live proof. Use independent quest
instances for mutually exclusive outcomes, a dedicated server and two clients. For each row retain
the authoritative server state and both clients' before/after inventory, gold, journal, relations,
traits, companion state and relevant settlement changes. The other player's personal state must
remain unchanged. Resolve a real headman with `coop.debug.issues.give` and the registered
`HeadmanNeedsGrainIssue` type; use the production offer/discussion dialogue for acceptance and delivery.

| Flow | Vanilla source members | Expected server and client result |
| --- | --- | --- |
| Offer generation | `ConditionsHold`, `OnCheckForIssue` | Only eligible headmen, non-wheat villages bound to a town, scarce and expensive grain; creation and market identity agree. |
| Offer removal | `IssueStayAliveConditions`, `IssueManager.DailyTick` | No suitable market or market count at least 350 removes an unaccepted issue everywhere. |
| Decline and discuss | issue dialogue properties, quest `SetDialogs` | Declining or saying work continues changes no inventory or rewards. |
| Eligibility | `CanPlayerTakeQuestConditions`, framework `CanTake` | Low relation and war refuse acceptance; two players can hold separate copies; the same player's second copy is refused. |
| Direct acceptance | `GenerateIssueQuest`, `QuestAcceptedConsequences` | Owning client gets one collect-grain journal, exact amount and deadline; server has an unstarted consequence object; observer has no personal quest. |
| Progress | inventory exchange, food consumption/sharing, hourly party tick, settlement entry, captivity callbacks | Grain acquisition/loss updates the owner only; enough-grain log appears and disappears; captivity alone does not fail the quest. |
| Hand-in refusal | `CompleteQuestClickableConditions`, finalization proof | Insufficient grain or leaving the village before the request arrives does not complete or reward. No partial hand-in. |
| Delivery | `Success` | Exactly the needed grain reaches the village; no gold payment; owner gets +1 renown, +70 Mercy XP, +50 Generosity XP, +5 giver relation and +1 other notable relation; giver +10 power, bound town +50 prosperity. One terminal journal, all copies end. |
| Timeout | `OnTimedOut`, `TimeoutFail` | Giver -5 power, bound town -10 prosperity, giver relation -5 and other notable relation -3; one owner log; no replayed client penalty. |
| War and kingdom change | `OnWarDeclared`, `OnClanChangedKingdom` | A new hostile faction relationship cancels the appropriate owner's quest; unrelated player's personal quest remains. |
| Crime and own raid | `CriminalRatingFail`, `OnWarDeclared` | Giver -10 power and -5 relation; owner -50 Honor XP; failure log and cleanup, including crime without an active map event. |
| Someone else raids | `OnVillageBeingRaided` | Giver's village raid cancels; unrelated village raid does not; owner's own raid is handled by the hostile-action branch. |
| Companion selection | `AlternativeSolutionCondition`, `DoTroopsSatisfyAlternativeSolution`, shared picker | Enough gold, a suitable Trade/Medicine companion, and healthy tier-two-or-better troops required; cancellation returns selections without payment. |
| Companion funding | `AlternativeSolutionStartConsequence`, `StartIssueWithAlternativeSolution` | Server charges owner once, village receives funding; companion disabled; return time/difficulty agree; observer pays nothing. A balance reduced before acceptance is refused. |
| Companion progress | `IssueManager.DailyTick`, `OnAlternativeSolutionSolvedAndTroopsAreReturning` | Owner sees return-days progress and returning log; observers have no personal journal. |
| Companion success | inherited `CompleteIssueWithAlternativeSolution`, `AlternativeSolutionEndWithSuccess` | Companion/troops return to owner on server and clients with vanilla wounds/XP; +1 owner renown, +30 Generosity XP, +5 giver/+1 other relations, +10 giver power, +50 bound-town prosperity. |
| Companion failure | inherited completion, `AlternativeSolutionEndWithFail` | Preserve the exact-zero RNG failure branch despite default zero failure chance: troops return, giver -5 power, notable relations -3 and town prosperity -10. |
| Busy owner | framework `BeforeAlternativeSolutionCompletion` | While return is disallowed, keep the same issue and troops; complete and return once on a later eligible tick. |
| AI lord arrival | `IssuesCampaignBehavior.OnSettlementEntered`, `CompleteIssueWithAiLord` | A real AI may complete an unaccepted offer; player arrival cannot; clients remove the offer. |
| Save and reconnect | behavior `SyncData`, framework ownership/visibility/dismissal | Verify framework dismissal on disconnect/restart, journal isolation, nearby market, average price and owner trait XP after loading and switching to the local hero. Active-quest resumption is unsupported by the framework. |

There is no lord solution (`IsThereLordSolution == false`), counteroffer, bribe, persuasion,
requested-item variant or quest-specific combat mission. Direct reward gold is zero and direct
delivery consumes all required grain at once. Village-raiding flows must use real campaign actions;
direct assignment of a completed battle is not evidence.
