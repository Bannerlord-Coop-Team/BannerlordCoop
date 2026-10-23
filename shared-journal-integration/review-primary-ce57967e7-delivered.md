# CLEAN: PR #3949 / issue #3781, final delta review (muse)

Source verdict: **CLEAN for the requested delta**. No actionable defect found. Severity, trigger and minimal-fix entries are therefore empty. Confidence is high for the traced source paths; execution and release readiness are separate and remain unverified.

## Frozen identity and scope

- Authorized origin verified: `https://github.com/Bannerlord-Coop-Team/BannerlordCoop.git`.
- HEAD verified: `ce57967e79ae9995bd7f8eea462f786082506ed7`.
- Tree verified: `93a104a9920f64d0209c678a0009cbf2b8f73e06`.
- Worktree clean at entry and final source inspection.
- Reviewed all eight files in `4e0d536da3a48a1c5cddf5b5aeeb7a8763cbc438..ce57967e79ae9995bd7f8eea462f786082506ed7`, plus connected dispatch, replay, ownership, serialization, DI and test helpers. This is a fresh delta verdict, not a fresh whole-PR review.
- Frozen full-PR base `7e59432341d1a5ed215943bd19ee1e563d5951a3`, merge `1f7644df8`, shared journal `b16d3d16a`, and required `9e85bfe3c` verified ancestors. No reliance on moving `origin/development`; no extra merge required.
- Private constructor variant `610566fc1` verified NOT an ancestor. Both shared constructor-test files are identical to `364bae7cf`, with matching Git blobs: `HeroSocialParameterCoopCommandTests.cs` = `edc90e2c6b958a79b9e451ee7136d190799db5c7`; `HeroConversationDebugCommandTests.cs` = `1efddd9a3ec41a5b89f34a058f04676e8b01ee05`.
- `git diff --check` clean for both the delta and frozen full-PR comparison.

Required root `AGENTS.md`, Issues `AGENTS.md`, `QuestLearnings.md`, and `doc/CodeStyle.md` read. Both prior `4e0d536da` reviews and `review-response.md` read as prior objections, then checked against current source. No source edits, builds, tests, CI submissions, runtime/lane actions, GitHub writes or delegation performed. Native Windows only. The report and focused Harmony decompilation evidence are outside the product tree. PR #3949 registered with T3 metadata only.

## Prior findings challenged against final source

### Tournament participation and exact two-log assertion: closed, high confidence

`source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:1097` now publishes the live snapshot before the finished snapshot at line 1099 through the production `TournamentSessionUpdated` subscription. `Snapshot` at line 1131 supplies the same session, controller, slot and match, a Join choice, an unfinished live match, and increasing revision/bracket revision on completion.

`LadysKnightOutTournamentProgress.cs:63` records participation only from that live snapshot. Its completion path at line 43 then finds the recorded match, checks the newer revision and finished state, and returns elimination/champion. `LadysKnightOutTournamentHandler.cs:97` runs this as the owner and selects the real branch at lines 104-107. A lone finished snapshot still produces Ongoing, so removing the new live publish would leave no finalization and fail `QuestJournalHistoryAssertions.cs:27`. There is no direct-finalization fallback in this theory.

`LadysKnightOutTests.cs:1103` requires exactly two owner quest entries for crime, tournament failure and tournament success. Owner acceptance creates the first; the shared ending snapshot supplies the second. The history helper subsequently requires one outcome broadcast and an owner-tagged server archive. The local owner handle correction at line 1071 is correct. These are source assertions, not claimed test results.

### Duplicate quest journal transport: closed, high confidence

The Lady `OnQuestLogAdded` postfix, `LadysKnightOutLogAdded` event, subscription/unsubscription, send branch and client quest replay branch are removed together. Repository search found no remaining event references or `IsIssueLog` references. The retained `LadysKnightOutJournalPatch.cs:13` handles only issue logs while solving through the lord solution.

The replacement is complete for the inspected Lady lifecycle. Its installed vanilla implementation adds a client-local start entry on acceptance; server additions occur on endings. `Framework/Dispatch/IssueFinalizedJournalPatch.cs:14` captures the server journal before `IssueFinalized` detaches the quest. `FinalizationCoordinator.cs:181` sends the `NetworkIssueJournal` to the owning peer before outcome observation removes ownership and broadcasts `NetworkIssueFinalized` at lines 229-245. Both client handlers use `GameThread.RunSafe`, so the related game-thread applies retain order.

`FinalizationCoordinator.cs:202` requires the local controller to own the issue and an active local quest before adding snapshot logs. `NetworkIssueJournal.cs:14` carries `TextObject[]`; `TextObjectSurrogate.cs:25` preserves `.Value` and nested text/int/string attributes rather than rendering the server's language. `QuestJournalReplayPatch.cs:14` suppresses a completion callback's repeated text during replay. The removed server-rendered Lady quest message can therefore no longer add a second literal-English ending beside the localized generic ending. No multilingual runtime execution was performed.

The lord issue-log transport must remain: `FinalizationCoordinator.cs:185` explicitly excludes lord solutions. Retaining its rendered text is not a second quest-ending transport and does not reintroduce the removed competing-entry path.

### Owner-peer routing, DI, disposal and protobuf: closed, high confidence

`LadysKnightOutJournalHandler.cs:47` resolves ownership through `IIssueOwnerResolver`, then the controller's peer through `IPlayerManager.TryGetPeer`, and sends once with `network.Send(peer, ...)` at line 49. The owner is the player controller, not the NPC giver. The receive controller check and issue lookup remain inside `RunSafe` at lines 59-66.

`ServiceModule.cs:22` discovers and autoactivates `IHandler` implementations. `GameInterfaceModule.cs:119` already registers `IPlayerManager`; the new constructor parameter needs no additional service registration. `IHandler` extends `IDisposable`, and the two retained subscriptions are both removed in `Dispose` at lines 38-41.

`LadysKnightOutMessages.cs:84` retains a public readonly server-to-client command with `ProtoContract(SkipConstructor = true)`. Existing fields keep tags 1-4; deleted tag 5 is not repurposed. All constructor callers were updated. There is no remaining quest leg that depends on the removed boolean. This review assumes matched server/client versions and makes no mixed-version protocol claim.

### Competing lord prefixes: closed, high confidence

`LadysKnightOutLordPatches.cs:18` and line 28 now use `HarmonyPriority(Priority.First)`. The generic `LordSolutionDispatchPatches.cs:18` and line 26 remain normal-priority bool prefixes on those same methods. Repository search found no additional prefix owner for either target.

Checked actual installed Harmony implementation, not an assumed rule: `MethodCreatorTools.AffectsOriginal` returns true for bool prefixes; `MethodCreator.AddPrefixes` checks `runOriginal` before each such prefix and stores the previous bool result into it. `PatchSorter` uses descending priority; First is 800, Normal is 400. On an ordinary Lady client call, Lady publishes its specialized request and returns false, so the generic bool prefix and vanilla are skipped. On the server, Lady returns true and the generic coordinator also returns true, leaving vanilla to run. Start under AllowedThread passes both prefixes; Lady completion deliberately still requests the authoritative consequence, preserving the existing settlement-replay behavior.

This resolves the previously unspecified ordering without changing the generic coordinator or importing its stricter completion influence check. Existing exact-cost/single-finalization assertions, including `LadysKnightOutTests.cs:1109`, remain meaningful. No runtime patch inspection or test execution was performed.

### Missing proof diagnostic: closed, high confidence

`LadysKnightOutFinalizationProofStrategy.cs:24` logs the quest id and supplied branch before returning false. The consuming outcome check and actual branch calls are unchanged. The production caller checks for a quest before invoking the strategy; no reachable null-quest failure was found from the added log.

## Shared integration and retained behavior

The removed Lady archive suppression remains removed, allowing shared server journal creation/update. `ServerQuestJournalPatches.cs:18` wraps issue archives with player ownership, avoids wrapping an existing player archive again in its quest overload, and filters ended client history by the persisted owner at line 49. The server is not filtered. `PlayerJournalLogEntry.cs:12` saves the controller independently of the active issue registry. Lord start, completion, tournament and crime paths run authoritative consequences inside owner substitution scopes.

Generic reward/notification gates remain in place (`Framework/Gating/DisableIssuesCampaignBehavior.cs:26`, `ServerQuestNotificationPatches.cs:17`). The Lady-specific pending-lord normalization in `LadysKnightOutLordHandler.cs:112` and line 123 remains necessary for the loaded player-local attempted flag; the persisted archive owner does not normalize that field. Both snapshot/finalization order cases remain covered in source. The private Honor mirror is still absent; the crime prefix blocks local execution and the shared trait path runs under owner substitution.

No new delta violation found for quest layout, thin patches, publicized access, game-thread receive lookups, subscription lifetime, reliable ordered replay or generic/quest ownership boundaries. No quest debug-command findings raised.

## Exact installed implementation evidence

Native module metadata verified `v1.4.8`. Installed `TaleWorlds.CampaignSystem.dll` SHA-256 verified:

`1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE`

Its length is 5,469,656 bytes and UTC mtime is 2026-08-10 14:53:23, matching complete decompile cache keys `TaleWorlds.CampaignSystem-1786373603-5469656`. Supplied full Lady, IssueBase and JournalLogsCampaignBehavior files are byte-identical to those cached full decompiles; the supplied Honor Lady copy is identical too. Full cached QuestBase was also inspected for AddLog/completion ordering. No native behavior assumptions were needed.

Managed analysis tools were absent from the exposed catalog. Used the offline decompile fallback with the existing GUI hidden host through native Cygwin, without WSL. Installed/deployed `0Harmony.dll` is byte-identical to the Lib.Harmony 2.4.2 net472 package used for full focused type decompilations, SHA-256:

`7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C`

Harmony decompiles are stored beside this report as `review-ce57967e7-HarmonyLib.*.cs`. The initial old `HarmonyLib.MethodPatcher` name did not exist in this version; actual `MethodCreator`, `MethodCreatorTools`, `PatchSorter` and priority implementation were inspected instead.

## Readiness, separate from source verdict

**NOT ESTABLISHED.** The specified `ci-shared-journal-ce57967e7-focused-result` path was absent when inspected. No focused or full Release pass is claimed. The inherited constructor failure on the old 4e head and cancelled queued temporary constructor job do not establish final-head results. No CI was submitted or monitored by this reviewer.

Fresh final-head live is **NOT RUN**, as specified in the brief. Required remaining evidence includes canonical crime Honor A -50/B unchanged; tournament failure; a real tournament success; and PendingJoin paired journals, with owner-tagged server archives and observer filtering by actual runtime archive IDs rather than counts. The rejected old up27 B archive leak is not acceptance evidence.

The history helpers transfer existing log objects in process; they do not prove save serialization, download, reconnect or localization in the actual game. Those limits do not produce a new source defect in this delta, but prevent a readiness claim. Keep the PR draft until its required exact-head execution evidence is delivered.