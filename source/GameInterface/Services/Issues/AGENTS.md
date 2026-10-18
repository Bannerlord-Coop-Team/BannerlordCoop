# Agent guide: quest and issue framework

Scope: everything under `source/GameInterface/Services/Issues/`. Read the repo root `AGENTS.md` first, its
rules apply here too. `README.md` in this folder explains the behavior for humans. This file is what to
follow when changing it.

## Layout rules

- `Framework/` must not mention any specific quest. Every quest-specific line lives in
  `Quests/<QuestName>/`, one folder per vanilla issue behavior, named without `IssueBehavior`.
- One file holds one class, struct, enum or interface. All interfaces live in `Framework/Interface/`.
  A message sits in the folder of the area that sends it.
- Types are internal unless Coop.Core or messages need them. Messages are public readonly structs with
  `[ProtoContract(SkipConstructor = true)]`. There is no message registration list, they are found by scanning.
- A quest is registered only by having a descriptor. `IssuesModule` registers every class in
  `GameInterface.Services.Issues.Quests` that implements `IQuestTypeDescriptor` or one of the four strategy
  interfaces. `IssueTypeAllowlistGate` switches off the `RegisterEvents` of every unregistered vanilla issue
  behavior. Do not add a per-quest "disable this behavior" patch, it will fight the gate.
- Keep comments short and to the point. Keep AI talk out of them, such as "real" and other intensifiers.

## Rules that decide correctness

1. The server is authoritative and has no main hero or party. It runs vanilla outcome code as the owning
   player inside `using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))` with patches
   live. Never run server logic under `AllowedThread`, objects created there stay unregistered.
2. Clients replay what the server announced under `AllowedThread`. They never roll or pay anything again.
3. Resolve registry ids inside `GameThread.RunSafe`, not on the poll thread. Use `TryGetObjectWithLogging`.
4. Role checks are positive: `ModInformation.IsServer` or `IsClient`, never the negation.
5. The first accepter owns the issue: `IIssueOwnershipRegistry.TrySetOwner` on the server decides. The same
   registry exists on clients as a mirror (filled from accept messages and the join snapshot, emptied on
   `NetworkIssueFinalized`) and is used only for display decisions. The server's copy is the truth.
6. A non-owner client must end with the issue marked taken (`_issueState` set) and nothing else: no quest
   object, no `AddLog`, no `OnIssueUpdated`, `IsTriedToSolveBefore` left false. Vanilla uses that flag to pick
   the solver, so setting it makes the wrong player the solver at the end. The one addition is for sent troops:
   `AlternativeSolutionAcceptCoordinator.AddPlaceholderLog` puts an empty "Return Days" entry straight into
   `_journalEntries`, without `AddLog`, because vanilla's `IssueManager.DailyTick` reads `JournalEntries[0]` of
   every issue that is solving with troops.
7. `FinalizationCoordinator.Replay` only drives a quest that is in the local `QuestManager.Quests`. The
   server's mirror quest is never started, but reads as ongoing, and a joiner loads it from the save.
8. Anything the server writes into a per-player store is shared by every player. Trait XP is skipped for
   that reason (`Rewards/QuestTraitXpPatches.cs`). Before running a vanilla outcome, decompile it and look for
   `Campaign.Current.Player*`, `MainParty`, `Hero.MainHero` stores that `MainHeroSubstitutionScope` does not swap.
   Example: `IFaction.MainHeroCrimeRating`, which `DiplomacyHelper.IsWarCausedByPlayer` reads, stays the server's.
9. UI bookkeeping the server runs for players leaks into the save a joiner downloads. The server already skips
   quest pop-ups and unread markers (`Services/UI/Notifications/Patches/ServerQuest*Patches.cs`). When adding a
   server-run path, check what vanilla behaviors react to `AddLog`, `OnIssueUpdated`, `OnQuestCompleted`.
10. Prefer the publicizer. Assign and read private and protected members directly, use `nameof(...)` for
    reflection that is still needed (readonly fields), never a string literal. The exception is
    `Dispatch/QuestEventRelay.cs`, which finds vanilla listeners by name at runtime because `Framework/` cannot
    name quest types.
11. A client never ends a registered quest on its own under `AllowedThread`. A quest listener on the owner's
    client still fires inside a replay, for example a war (`Services/Stances/Handlers/FactionStanceHandler.cs`) or an
    army's removal (`Services/Armies/ArmyRegistry.OnClientDestroyed`). `OutcomeDispatcher.BeforeQuestOutcome` then
    drops the ending, unless `FinalizationCoordinator.Replay` is running (`Finalization/QuestEndingReplay.cs`). So
    the quest stays open on that client until the server's `NetworkIssueFinalized` arrives. A listener that fires
    outside `AllowedThread`, like a clan's kingdom change (`Services/Clans/Handlers/ClanKingdomHandler.cs`), still
    sends its ending to the server as a request.
12. Endings caused by something the server sees (a war, a kingdom change, a capture, an army breaking up) are
    decided on the server. The server's mirror quest is never started and has no listeners, so
    `Dispatch/QuestWorldEventRelayPatches.cs` postfixes `CampaignEventDispatcher.OnWarDeclared`,
    `OnClanChangedKingdom` and `OnHeroPrisonerTaken`. `QuestEventRelay` then runs the listener with that exact
    name and parameter types on every running registered quest, as its owner through
    `IOutcomeDispatcher.RunAsOwners`. The dispatch patches announce the ending and every client replays it. A
    quest needs no patch of its own for these. A listener under another name is skipped without a log (vanilla
    also uses `WarDeclared`, `OnPrisonerTaken` and `HeroPrisonerTaken`). Another event needs a new postfix in that
    file.
13. `Replay` cancels the owner's running quest after an issue-only ending (`IssueStayAliveFailed`,
    `IssueTimedOut`). Vanilla's `IssueFinalized` detaches the quest before removing the issue, which would leave it
    in the journal. On the server `BeforeIssueOutcome` only announces such an issue while its quest still runs, a
    finished quest was already announced by `BeforeQuestOutcome`.
14. Players and their clans never offer issues. `Gating/PlayerHeroIssueOwnerGate.cs` turns
    `Hero.CanHaveCampaignIssues` off for them on the server. Vanilla only excludes `Clan.PlayerClan`, which on the
    server is not a player, and lord-given issues (a player leading an army) would otherwise land on players.

## Recipes

Add a quest type: follow "Adding a quest type" in `README.md`. Then:
- Decompile the vanilla behavior (`decompile` skill). Do not copy a claim like "payload: none needed" from any
  note. Check `IssueDifficultyMultiplier`, `Town.GetItemPrice` and anything else the constructor derives from
  per-machine state, and carry it in the accept strategy.
- List every listener in the quest's `RegisterEvents` and in the behavior's. The gate turns the behavior's on for
  every machine, the quest's only run on the owner's client. For each listener that ends the quest, decide who
  drives it (rules 11 and 12). ArmyNeedsSupplies and LordNeedsHorses: wars, kingdom changes and the giver's
  capture are relayed on the server. An army breaking up is ended by the behavior's stay-alive failure on the
  server.
- Rule 2 also covers the owner's own vanilla code. When the owner's client replays a success, vanilla moves the
  items again on top of the server's replicated roster change. And vanilla's sent-troops dialog runs
  `AlternativeSolutionStartConsequence` on the client before the server accepts. Add a prefix that returns
  `ModInformation.IsServer` on each method like that (`LordNeedsHorsesGiveMountsPatch`,
  `LordNeedsHorsesStartConsequencePatch`). `GiveGoldAction.ApplyInternal` is already blocked on clients
  (`Services/Actions/Patches/GiveGoldActionPatches.cs`).
- Put a quest's branch patches in one `[HarmonyPatch]` class with one prefix per branch and a shared helper (see
  `ArmyNeedsSuppliesBranchPatches`). Name each prefix after the `CompleteQuestWith*` call inside the branch
  method, not the method name.
- Keep patches thin. Resolving owners, scanning issues or swapping the main hero belongs in a DI'd framework
  service (`IOutcomeDispatcher`), so it can be tested without Harmony. The maintainer asks for this on review.
- `TryBuildDebugCapture` must return false for owners the issue's `IssueStayAliveConditions` would throw on, and
  for owners the quest type cannot have. `coop.debug.issues.give` only refuses player heroes, so each strategy
  checks its own owner (a notable for GangLeader, an army owner for ArmyNeedsSupplies). LordNeedsHorses calls
  `IssueStayAliveConditions` on a throwaway issue. It first removes that issue's listeners with
  `CampaignEventDispatcher.Instance.RemoveListeners`, because the `IssueBase` constructor registers one.
- Add the quest's folder only. If `Framework/` needs a change for it, stop and ask, the framework should not
  learn about one quest.

Add a framework message or handler:
- Server to client: `IServerToClientCommand`, handler as an `IHandler` in GameInterface (auto-registered by
  namespace). Check `ModInformation` first. Single-peer send for join data (see `IssueOwnershipSnapshotHandler`,
  triggered by `PlayerCampaignEntered`, which is internal to Coop.Core).
- A new Coop.Core server handler that depends on a GameInterface service needs a mock in
  `Coop.Tests/TestComponentBase.cs` (`RegisterMock<T>`), otherwise every server-container test in `Coop.Tests`
  fails to build its container. This broke CI once.
- Joiners are sent nothing for events before their save, so anything a joiner needs from earlier has to be in
  a join snapshot or derivable from the save.
- An empty array of structs in a message arrives as `null`, protobuf-net writes nothing for it (empty `byte[]`
  fields arrive non-null). Guard it with `?? Array.Empty<T>()`. `MessageBroker.Publish` catches and logs a
  subscriber's exception, so an in-process test can pass over a throwing handler. Call the handler directly
  (see `IssueOwnershipSnapshotReceiverTests`) and send a real protobuf round trip to prove it.

## Verification

- Compile only, no deploy: `dotnet build source\E2E.Tests\E2E.Tests.csproj -c Debug -p:NuGetAudit=false -p:ModName= --no-restore`.
  `-p:ModName=` stops `Deploy.targets` copying into the game folder.
- Run the E2E Issues and UI namespaces: `dotnet test source\E2E.Tests\E2E.Tests.csproj -c Debug -p:NuGetAudit=false -p:ModName= --no-restore --no-build --filter "FullyQualifiedName~E2E.Tests.Services.Issues|FullyQualifiedName~E2E.Tests.Services.UI"`.
- Also run `GameInterface.Tests` `PatchTest` and `ContainerTest` when adding patches or registrations.
- CI builds Release and runs `CoopUnitTests.slnf` (including all of `Coop.Tests`) plus eight E2E shards.
  Debug has extra local-only failures. After touching Coop.Core or registrations, run all of `Coop.Tests` with
  `-c Release`.
- Prove every new test can fail: temporarily break the code it covers, see it fail, restore it, check with
  `git diff` that the file matches again.
- Tests use the in-process environment (`E2ETestEnvironment`, `EnvironmentInstance.Call`, `SimulateMessage`).
  Reuse `QuestScenario`, `StubIssueQuestGenerator`, `UnstartedQuest` and `MethodCallRecorder` before building
  a GangLeader fixture. Messages cross a real protobuf round trip in that environment, but a handler that throws
  is logged and swallowed there, so also assert something the handler does after its riskiest line.
- To test the owner's side of `Replay`, the quest must be in the client's `QuestManager.Quests`: call
  `StartQuest()` under `AllowedThread`. `Replay` drops a quest that was never started, so a test built on a bare
  `UnstartedQuest` cannot see the owner's journal.
- The test campaign cannot build encyclopedia links, so vanilla quest log texts built with
  `StringHelpers.SetCharacterProperties` throw. Turn those getters off with `Call(action, disabledMethods)`
  (see `ArmyNeedsSuppliesWarDeclaredTests`).
- Fixtures that work: an army leader with a real `ArmyNeedsSuppliesIssue` on every instance, accepted with
  `StartIssueWithQuest` on the client (`ArmyNeedsSuppliesWarDeclaredTests`); a town and a hideout
  (`IssueSafetyTests`); a server war with `DeclareWarAction.ApplyInternal`, which replicates to clients; the
  relayed war, kingdom change and capture in `LordNeedsHorsesQuestEventTests`.
- When proving tests can fail, several breaks can share one build if each test targets a different one.
- Say what was not run. The maintainer has hand-tested giving an issue, accepting it, sending troops and a
  player rejoining in a real game. Everything else has only run in the in-process environment.

## Do not

- Do not add host-party edge cases or null-party fallbacks on the server.
- Do not call vanilla `StartIssueWithQuest` or `StartIssueWithAlternativeSolution` on a non-owner client.
- Do not widen the registered set implicitly. A new quest type is an explicit descriptor.

## Known gaps (do not claim these work)

- Quest endings other than the ones above and a late join during a "do it yourself" quest are untested in a
  real game.
- Quests are transient (`Dismissal/ActiveIssueDismisser.cs`): sent troops are lost on a server restart, and
  vanilla endings that bypass the dispatch patches (`CompleteIssueWithCancel/Fail/Betrayal/Quest/AiLord` on a
  registered issue without a quest) are not announced.
- Only wars, kingdom changes and captures are relayed, and only to the exact listener names in rule 12.
  GangLeader's `OnSettlementOwnerChanged` cancel is not relayed. If a settlement owner change is replayed on
  clients under `AllowedThread`, rule 11 now drops that ending and nothing ends the quest. Not checked.
- A kingdom change ends a relayed quest twice over: the server's relay ends it, and the owner's client still sends
  its own request. If the request arrives after the server's ending, it should be dropped with a
  "does not own" warning.
- Endings sent as a request or replayed by `Replay` drop vanilla's log line, so the owner's journal shows the quest
  ended without the reason (for example "Army is disbanded and your mission was canceled").
- The server judges a war over crime rating against its own `MainHeroCrimeRating` (rule 8), so the owner can get
  a cancel where vanilla would fail the quest.
