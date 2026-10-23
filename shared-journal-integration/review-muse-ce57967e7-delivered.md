# CLEAN: PR #3949 / issue #3781 delta to ce57967e7

Final-source read-only delta review. Every prior-round fix challenged against the exact head; no defect found in the delta. The verdict covers delta source only.

## Frozen scope and identity

- Repository: `https://github.com/Bannerlord-Coop-Team/BannerlordCoop.git` (authorized origin).
- Reviewed commit: `ce57967e79ae9995bd7f8eea462f786082506ed7`.
- Reviewed tree: `93a104a9920f64d0209c678a0009cbf2b8f73e06`.
- Entry status clean (`git status --porcelain` empty); still clean at report time.
- Focus: `4e0d536da3a48a1c5cddf5b5aeeb7a8763cbc438..HEAD` (3 commits, 8 files, `git diff --check` clean).
- Frozen base `7e59432341d1a5ed215943bd19ee1e563d5951a3`, required `9e85bfe3c`, #4055 `b16d3d16a`: all verified ancestors. No extra development merge taken or needed.
- `ce57967e7` two constructor-test files byte-identical to shared hotfix `364bae7cf` (empty `git diff` on both paths). Private `610566fc1` verified NOT an ancestor.
- No source edits, builds, tests, CI submissions, runtime/lane actions, GitHub writes, or delegation performed. This report is the sole authored artifact.

## Fix challenges (all pass)

### 1. Tournament live snapshot + exact-2 log assertion: sound, fails closed

Anchor: `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:1096-1103`.

- `Observe` trace: the live snapshot (`LiveMatch`, rev 1) records participation with `Round = round`; the finished snapshot (rev 2 > 1, match `Finished`, bracket 1 > 0) completes it. Ending 1 (round 0, not champion) yields `Eliminated` with ` TournamentRoundGoal <= 0 + 1` false, so `TournamentFailure`. Ending 2 (round 1, champion, `IsCompleted`, winner slot match) yields `Champion`, so `TournamentSuccess`. Matches the existing 8-case `TournamentResultRunsTheRealBranchAsItsOwner` matrix.
- Both publishes inside one `Server.Call` keep FIFO order through the same `GameThread.RunSafe` queue; the existing test's split across two calls is equivalent, not required.
- The `Assert.Equal(2, quest.JournalEntries.Count)` on `ClientA` uses ClientA's own quest handle (same closure pattern as the existing test at `:199-203`), so no cross-instance read.
- Synchrony: `TestNetworkRouter.AutoDrainReady` defaults true (documented synchronous harness behavior) with `GameThread` receive context, so `RunSafe` applies inline; `FlushCoalescer` only drains `ISendCoalescer` field sends (gold), which this theory never asserts, so its absence is correct.
- Sensitivity is the point: a surviving duplicate quest leg would read 3, a missing generic snapshot would read 1. Vanilla confirms each ending adds exactly one quest log (success `AddLog` then `CompleteQuestWithSuccess`; fail/crime pass the log to `CompleteQuestWithFail`), and the owner holds the accept start log, so 2 is exact for all three theory cases including crime.
- `SerializeForWire` on every send means the protobuf change below is exercised for real in-process.

### 2. Quest-log leg removal: complete, generic path covers

Anchors: `LadysKnightOutJournalHandler.cs:14-69`, `LadysKnightOutMessages.cs:84-98`, `Patches/LadysKnightOutJournalPatch.cs:13-19`.

- Removal is total: zero remaining references to `LadysKnightOutLogAdded` or `IsIssueLog` anywhere in `source`. Subscribe/unsubscribe pairs balance (issue leg + network leg only). Unused `Quest` aliases removed from both files.
- Generic coverage traced: `IssueFinalizedJournalPatch` prefixes `IssueBase.IssueFinalized` (runs before vanilla detaches the quest) -> `IssueJournalObserved` -> `FinalizationCoordinator:181-194` sends owner-peer `NetworkIssueJournal` carrying the original `TextObject[]` -> client dedupes by rendered text at `:210-224`. The cross-language duplicate is gone because the rendered-literal leg no longer exists.
- Lord retention correct: generic explicitly skips `IsSolvingWithLordSolution` (`FinalizationCoordinator.cs:185`), so the Lady issue leg is still the only carrier there.
- No lord start double-send: cached vanilla `IssueBase` shows `AddLog(LordSolutionStartLog)` at line 796 before `_issueState = SolvingWithLordSolution` at 798, so the postfix (which requires lord state) never fires for the start log; `NetworkLadysKnightOutLordAccepted.StartLog` carries it instead.
- `ProtoMember(5)` removal is safe: the struct is network-only with same-build ends, and protobuf-net tolerates absent fields. Layout rules still hold (public readonly struct, `SkipConstructor`).

### 3. Owner-peer routing: correct DI and failure modes

Anchor: `LadysKnightOutJournalHandler.cs:44-54`.

- `IPlayerManager` is registered `InstancePerLifetimeScope` (`GameInterfaceModule.cs:119`); the handler auto-registers via `ServiceModule` `IHandler` scan, so the new ctor parameter resolves with no manual registration. Same dependency shape as `FinalizationCoordinator` and `LadysKnightOutLordHandler`.
- `TryGetPeer` miss (owner disconnected) skips the send, identical to the generic journal path. Retained receive-side `ControllerId` check is harmless defense matching the generic handler.
- Satisfies the QuestLearnings owner-routing entry.

### 4. `HarmonyPriority(Priority.First)` on lord prefixes: pins the intended winner

Anchor: `Patches/LadysKnightOutLordPatches.cs:16-35`.

- Composition: Lady `First` runs before generic `LordSolutionDispatchPatches` (default priority). For a Lady issue on a client, Lady publishes `LadysKnightOutLordTriggered` and returns false, skipping the generic prefix and the original, so exactly one request (`RequestLadysKnightOutLord`) is sent. For any other registered issue Lady returns true and the generic prefix runs. Matches the Artisan precedent (`ArtisanOverpricedGoodsBranchPatches.cs:26`).
- `CompletePrefix` lacking the `CallOriginalPolicy` check is pre-existing, not delta, and harmless: client replay never re-runs `CompleteIssueWithLordSolutionWithRefuseCounterOffer` under `AllowedThread` (finalize replay goes through `IssueFinalized`/`TryCompleteIssue`; `HandleAccepted` sets state directly). Delta only pins the order the prior round observed.
- No competition on `OnHeroKilled`: per-quest fallback allowed by the learnings; no generic patch targets it.
- A future flip would fail closed: E2E lord tests assert Lady messages, exact cost, and one finalization.

### 5. Proof Warning + local history handle: clean

Anchors: `LadysKnightOutFinalizationProofStrategy.cs:24`, `LadysKnightOutTests.cs:1071`.

- Warning format matches the `ArmyNeedsSupplies` precedent. `quest` is non-null from both callers (coordinator null-checks at `FinalizationCoordinator.cs:165`; tournament handler iterates live `RunAsOwners` quests), so `quest.StringId` cannot throw on a reachable path.
- `:1071` now uses the `ClientA` issue handle inside `ClientA.Call`; `IsRelatedTo` compares `StringId`, so behavior is unchanged and the handle is now local.

### 6. Constructor-test hotfix: identical, test-only

`ce57967e7` touches only the two E2E command-test files, diff-empty against `364bae7cf`. No production behavior.

## Touched-rule dispositions

- Root AGENTS: host model (no host-party branches added), no server `AllowedThread` (receive-path `AllowedThread` only in `HandleLog`/`HandleAccepted`), per-channel ordering preserved (journal before finalize on `ReliableOrdered`, both applied on the game-thread FIFO), registry lookups inside `RunSafe`, publicized members bound directly, no reflection added. Clean.
- Issues AGENTS rules 1-11: substitution scopes, branch proofs (generic `QuestSuccess` still rejected for this descriptor), ownership registry, non-owner handling, mirror quest, per-player Honor via the shared service (no private mirror present), server notification suppression via shared patches, client endings dropped, server-decided relays, lord/menu handling. Clean.
- QuestLearnings: owner-routing and log-skipped-cases entries now satisfied; no other entry violated.
- CodeStyle: handler/patch/message naming and layout match siblings; `IIssueResolver`/`TryGetObjectWithLogging`/`RunSafe` usage unchanged; `Assert.Single` on sends retained.

## Vanilla identity and evidence limits

- Installed `TaleWorlds.CampaignSystem.dll` (`bin/Win64_Shipping_Client`) SHA-256 `1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE`, matching the brief.
- Cached `vanilla-IssueBase.cs` confirms log-before-state for lord start; `vanilla-LadysKnightOutIssueBehavior.cs` confirms start log plus exactly one log per tournament/crime ending.
- `ci-shared-journal-ce57967e7-focused` exists but its `-result` file does not; the 4e focused result is superseded. No CI pass claimed. Full Release not verified (HIVE only, none inspected).
- Final-head live NOT RUN: crime canonical (A Honor -50, B unchanged), `TournamentFailure`, real `TournamentSuccess`, PendingJoin paired journals with server owner-tagged archives and B filtered by real IDs. Old e5/up27 observations are not final acceptance.

Source verdict: **CLEAN**. Readiness: **NOT ESTABLISHED**.
