# Quest and issue framework

Scope: everything under `source/GameInterface/Services/Issues/`. The repo root `AGENTS.md` applies too.
`README.md` here is the short player-facing summary and the debug commands. This file owns the rules.
If you are working on quests, view [`QUEST_DOCS.md`](QUEST_DOCS.md).

## Layout

- `Framework/` must not mention any specific quest. Quest code lives in `Quests/<QuestName>/`, one folder per
  vanilla issue behavior, named without `IssueBehavior`. If `Framework/` needs a change for one quest, stop and ask.
- A quest is registered only by having a descriptor. `IssuesModule` registers every class in
  `GameInterface.Services.Issues.Quests` that implements `IQuestTypeDescriptor` or a strategy interface, and
  `Framework/Gating/IssueTypeAllowlistGate.cs` turns off `RegisterEvents` for every other issue behavior. Do not
  add a per-quest "disable this behavior" patch, it will fight the gate.
- `IssuesLayoutTests` in `GameInterface.Tests` checks that `Framework/` interfaces sit in `Framework.Interface` and that
  messages are public readonly structs, with network commands as `[ProtoContract(SkipConstructor = true)]`.

## Rules that decide correctness

Open framework gaps are tracked as `[QUESTS]` issues (#3981 to #3988).

1. The server runs vanilla outcome code as the owning player inside
   `using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))`. Clients replay what the
   server announced and never roll or pay anything again.
2. A quest with a `FinalizationProofStrategy` ends through its branch, not a generic `QuestSuccess`, which skips
   the branch method where vanilla pays. The framework doesnt enforce this yet (#3981), so reject a generic
   `QuestSuccess` for your descriptor.
3. The first accepter owns the issue, decided by `IIssueOwnershipRegistry.TrySetOwner` on the server. Clients
   keep a mirror of the registry, filled from accept messages and the join snapshot and emptied on
   `NetworkIssueFinalized`. `Framework/Gating/SimilarCommitmentsGatePatch.cs` reads it inside
   `IssueBase.CheckPreconditions` so vanilla's one-issue-per-type check counts only the player's own issues, so a
   stale mirror changes whether a player can accept.
4. A non-owner client ends with the issue marked taken (`_issueState` set) and nothing else: no quest, no
   `AddLog`, no `OnIssueUpdated`, `IsTriedToSolveBefore` left false (vanilla uses it to pick the solver). For sent
   troops `AlternativeSolutionAcceptCoordinator.AddPlaceholderLog` also puts an empty entry straight into
   `_journalEntries`, because `IssueManager.DailyTick` reads `JournalEntries[0]` of every troop-solved issue.
5. `FinalizationCoordinator.Replay` only drives a quest in the local `QuestManager.Quests`. The server's mirror
   quest is never started but reads as ongoing, and a joiner loads it from the save.
6. Anything the server writes into a per-player store is shared by every player. Before running a vanilla
   outcome, decompile it and look for `Campaign.Current.Player*`, `MainParty` and `Hero.MainHero` stores that
   `MainHeroSubstitutionScope` does not swap. Trait XP is skipped for this (`Framework/Rewards/QuestTraitXpPatches.cs`),
   and `IFaction.MainHeroCrimeRating`, read by `DiplomacyHelper.IsWarCausedByPlayer`, stays the server's.
7. UI bookkeeping the server runs for players leaks into the save a joiner downloads. The server skips quest
   pop-ups and unread markers (`Services/UI/Notifications/Patches/ServerQuest*Patches.cs`). For a new server path,
   check what reacts to `AddLog`, `OnIssueUpdated` and `OnQuestCompleted`.
8. A client never ends a registered quest on its own under `AllowedThread`. A quest listener still fires inside a
   replay (a war, an army's removal), and `OutcomeDispatcher.BeforeQuestOutcome` drops that ending unless
   `Replay` is running (`Framework/Finalization/QuestEndingReplay.cs`). A listener outside `AllowedThread`, like a
   clan's kingdom change, still sends its ending to the server as a request.
9. Endings the server sees (a war, a kingdom change, a capture, an army dispersal) are decided on the server.
   `Framework/Dispatch/QuestWorldEventRelayPatches.cs` postfixes `CampaignEventDispatcher.OnWarDeclared`,
   `OnClanChangedKingdom`, `OnHeroPrisonerTaken` and `OnArmyDispersed`, and `Framework/Dispatch/QuestEventRelay.cs`
   runs the listener with that exact name and parameter types on every running registered quest, as its owner through
   `IOutcomeDispatcher.RunAsOwners`. A listener under another name is skipped without a log. These two files pass
   listener names as strings because `Framework/` cannot name quest types.
10. `Replay` cancels the owner's running quest after an issue-only ending (`IssueStayAliveFailed`,
    `IssueTimedOut`), because vanilla's `IssueFinalized` detaches the quest first and would leave it in the
    journal. On the server `BeforeIssueOutcome` announces such an issue only while its quest still runs.
11. Players and their clans never offer issues. `Framework/Gating/PlayerHeroIssueOwnerGate.cs` turns
    `Hero.CanHaveCampaignIssues` off for them on the server, vanilla only excludes `Clan.PlayerClan`.

## Adding a quest type

`ArmyNeedsSupplies` is the worked example. `LordNeedsHorses` shows sending troops and the client patches.

1. Decompile the vanilla behavior and quest (`decompile` skill). List every listener in both `RegisterEvents`:
   the behavior's run on every machine, the quest's only on the owner's client. For each one that ends the
   quest, decide who drives it (rules 8 and 9). Another event or listener name needs a new relay postfix.
2. `<QuestName>CreationCaptureStrategy` (`ICreationCaptureStrategy`) carries what the constructor rolled.
   `TryBuildDebugCapture` returns false for owners the issue's `IssueStayAliveConditions` would throw on and for
   owners the quest type cannot have, because `coop.debug.issues.give` only refuses player heroes.
3. `<QuestName>QuestSolutionAcceptStrategy` (`IQuestSolutionAcceptStrategy`) carries anything the accept derives
   from per-machine state (`IssueDifficultyMultiplier`, `Town.GetItemPrice`). Check before leaving it empty.
4. `<QuestName>AlternativeSolutionAcceptStrategy`, only if the quest can be solved by sending troops.
5. Only if branches pay differently: a `byte` enum `<QuestName>Branch`, one `Patches/<QuestName>BranchPatches`
   class with a prefix per branch calling `IOutcomeDispatcher.BeforeQuestBranch`, named after the
   `CompleteQuestWith*` call inside the branch, and a `<QuestName>FinalizationProofStrategy`.
6. A prefix returning `ModInformation.IsServer` on each vanilla method that moves items or gold on the owner's
   client the server already moves (`LordNeedsHorsesGiveMountsPatch`, `LordNeedsHorsesStartConsequencePatch`).
   `GiveGoldAction.ApplyInternal` is already blocked on clients.
7. `<QuestName>Descriptor` (`IQuestTypeDescriptor`) names the issue type and takes the strategies, `null` for ones
   the quest does not have. Nothing else needs registering.

Keep patches thin. Owner lookups, issue scans and main hero swaps belong in a DI'd framework service like
`IOutcomeDispatcher`, so they can be tested without Harmony.

## Verification

- `dotnet test source\GameInterface.Tests\GameInterface.Tests.csproj -c Debug -p:NuGetAudit=false -p:ModName= --filter FullyQualifiedName~GameInterface.Tests.Services.Issues`
- `dotnet test source\E2E.Tests\E2E.Tests.csproj -c Debug -p:NuGetAudit=false -p:ModName= --filter "FullyQualifiedName~E2E.Tests.Services.Issues|FullyQualifiedName~E2E.Tests.Services.UI"`
- After adding patches or registrations also run `PatchTest` in `GameInterface.Tests` and `ContainerBuildTests`
  in `Coop.Tests`.
- Quest debug commands (`coop.debug.issues.*`, including `Quests/<QuestName>/Commands/`) are dev tools and it
  doesnt matter if they break. Dont write tests for them or raise review findings on them.
- To test the owner's side of `Replay` in E2E, start the quest with `StartQuest()` under `AllowedThread` on the
  client, otherwise `Replay` drops it (rule 5).
- Run the flows the change touches through their real UI on a server and two clients: accept, success and fail,
  sending troops and their return, endings from a war or capture, the non-owner's view, and a rejoin. Use
  `coop.debug.issues.complete` only for endings, it skips the branch methods. In the PR list each flow as
  in-process only, live on the exact head commit sha, or not run.
