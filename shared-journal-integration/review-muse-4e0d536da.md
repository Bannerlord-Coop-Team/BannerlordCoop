# Review: PR #3949 shared-journal integration (muse) — 4e0d536da

Scope: read-only exact-head review, issue #3781 Lady's Knight Out. No edits, builds, tests, CI, game, GitHub, or delegation.
Head `4e0d536da3a48a1c5cddf5b5aeeb7a8763cbc438`, tree `a617ca0e0493240971a8590549b6b149073177ef`, `git status` clean, frozen base `7e5943234` (= origin/development) verified ancestor, `9e85bfe3c` verified ancestor, `b16d3d16a` (#4055) in history. Focus increment is HEAD alone (2 files); merge `1f7644df8` verified overlap-free except `QuestSolutionAcceptCoordinator.cs`, where both sides are preserved (dev `OfferAgain`+`DiscardArchive`, PR `Reject` due-time + restore). Installed `TaleWorlds.CampaignSystem.dll` SHA-256 `1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE` matches the quest doc. No private Honor mirror (e5 intact). Trusted cooperative clients.

Verdict: CHANGES REQUESTED (source correctness). Readiness: NOT READY, final-head live not run, CI pending.

## Findings

### 1. New history theory's tournament cases can never finalize (HIGH, high confidence)

`source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:1090` — cases `ending=1,2` publish one already-finished `TournamentSessionUpdated` (`Snapshot(round, finished:true, ...)`, Phase `Completed`). `LadysKnightOutTournamentProgress.Observe` (`source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutTournamentProgress.cs:43`) records participation only from a `LiveMatch` snapshot (`:63`), so a lone finished snapshot returns `Ongoing`, no branch runs, no `NetworkIssueFinalized` is sent, and `AssertLateJoinHistory` fails at its first `Assert.Single`. The existing tournament test does the two-step publish (`LadysKnightOutTests.cs:192`).

Fix: publish the live snapshot first, then the finished one, mirroring lines 192-196:

```csharp
var round = ending == 2 ? 1 : 0;
var champion = ending == 2;
var broker = scenario.Server.Resolve<IMessageBroker>();
broker.Publish(this, new TournamentSessionUpdated(Snapshot(round, false, champion)));
broker.Publish(this, new TournamentSessionUpdated(Snapshot(round, true, champion)));
```

Expected outcomes are unchanged (round 0 eliminated below goal 2 fails; round 1 qualifies for success).

### 2. Two bool prefixes own the lord Start/Complete methods (MEDIUM, high mechanism / medium likelihood)

`source/GameInterface/Services/Issues/Quests/LadysKnightOut/Patches/LadysKnightOutLordPatches.cs:16` and `:25` add bool prefixes on `IssueBase.StartIssueWithLordSolution` and `CompleteIssueWithLordSolutionWithRefuseCounterOffer`, which already have bool prefixes in `source/GameInterface/Services/Issues/Framework/Dispatch/LordSolutionDispatchPatches.cs:16` and `:24`. Harmony runs the first false-returning prefix and skips the rest, so patch order decides whether the client sends the Lady request or the generic `RequestLordSolution`. Lady currently wins (Lady lord tests pass), but a metadata reorder silently flips live lord accepts to the generic coordinator (`LordRefusedCounterOffer` outcome, influence re-checked at completion unlike vanilla, no owner archive). Violates the one-owner-per-method rule.

Fix (quest-side, no Framework change): add `[HarmonyPriority(Priority.First)]` to Lady's two prefixes with a one-line why (Artisan precedent at `ArtisanOverpricedGoodsBranchPatches.cs:26`), or get a maintainer call on folding Lady into the generic coordinator. E2E would catch a flip (Lady lord tests assert Lady messages), which keeps this latent rather than active.

### 3. Owner-only journal messages use SendAll (LOW, high confidence)

`source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutJournalHandler.cs:50` and `:65` — `NetworkLadysKnightOutLog` carries an owner id and non-owners drop it (`:79`), so this is the `SendAll` for an owner-only message `QuestLearnings.md` says to route to the owner's peer.

Fix: inject `IPlayerManager`, resolve the owner peer, `network.Send(peer, ...)` (generic `NetworkIssueJournal` already sends owner-only).

### 4. Proof strategy doesn't log skipped cases (LOW, high confidence)

`source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutFinalizationProofStrategy.cs:16` returns false silently when no tournament outcome was recorded. `QuestLearnings.md` says to log skipped cases the way `ArmyNeedsSuppliesFinalizationProofStrategy.cs:42` does.

Fix: add a `Logger.Warning` with issue id and proof in the false branch.

## Nits

- `LadysKnightOutTests.cs:1071` reads `issues[scenario.Server]` inside a `ClientA.Call`. Works (`IsRelatedTo` compares `StringId`) but use the local handle.

## Verified clean

- Removed prefixes: `IssueRewards` covered by shared `DisableIssuesCampaignBehavior` (server-only); `QuestNotification` identical to shared `ServerQuestNotificationPatches`; `HistoryPrefix` removal is the intended change (server now writes a `PlayerJournalLogEntry` archive, tested owner-typed); the dropped `IsTriedToSolveBefore` client notification gate has no practical effect (observer replays call `IssueFinalized` directly and never fire local `OnIssueUpdated`, verified against cached vanilla `IssueBase`/`JournalLogsCampaignBehavior`).
- Owner/archive/visibility chain traced end to end: every server completion runs inside a substitution scope (lord accept+complete, native outcome, snapshot outcome, crime/timeout via relay+request), `CreateRelatedLog` postfixes single-wrap (not-Player guard), `IsEnded` leaves the server alone and filters clients by owner.
- No duplicate logs: Lady quest transport + generic ending snapshot dedupe by text, `QuestJournalReplayPatch` covers replay, order preserved by `ReliableOrdered` + game-thread FIFO. Issue-log transport is required (generic `IssueJournalObserved` skips lord solutions); the start log is not double-sent because vanilla `AddLog`s before setting lord state (cached `vanilla-IssueBase.cs:791`).
- Pending-lord `CampaignReady` clear + snapshot restore has no generic equivalent (snapshot receiver only rebuilds the registry) and doesn't conflict with endings; owner disconnect dismisses the issue so rejoin-mid-lord can't strand.
- Timeout: client `OnTimedOut` block matches the Artisan/Smugglers pattern; consequences run once on the server via the `Complete*` hook, no double penalty.
- Honor: no private mirror; shared Traits service only.
- Framework diffs carry no quest-specific references; messages/layout/role-check/`RunSafe`/`TryGetObjectWithLogging`/no-server-`AllowedThread` all clean. New tests use `Assert.Single` on sends and assert post-risk state (no self-masking beyond finding 1, which fails closed).

## Rule disposition

- Root AGENTS: host model, `AllowedThread`, ordering, game-thread resolution, publicizer, CodeStyle clean. Testing: finding 1 breaks it (new test fails); rest clean.
- Issues AGENTS: layout clean; rules 1-11 traced (substitution scopes, branch proofs, ownership, non-owner handling, mirror quest, per-player Honor via shared service, server notification suppression via shared patches, client endings dropped, server-decided relays incl. per-quest map-event/departure fallback per learnings, death via vanilla `IssueManager` + observation).
- QuestLearnings: findings 3 and 4 violate the owner-routing and log-skipped-cases entries; rest clean (native tournament path trusts the owner claim, acceptable under trusted scope with finalize-once guards).

## Readiness (separate from correctness)

NOT READY. Final-head live not run; Issues AGENTS requires the touched flows (accept, tournament success/fail, timeout, death, crime, lord accept/complete/disconnect, observer view, rejoin) run on the exact head or listed as not run. CI focused + full Release pending; finding 1 predicts the new theory red. After fixes: re-run focused Lady + UI history + layout/Patch + containers, then full Release, then live on the exact head.

## Evidence limitations

- Read-only: all findings code-traced, nothing executed; CI pending.
- `DefaultNotificationsCampaignBehavior.OnIssueUpdated` vanilla internals not in cached decompiles; the notification-gate claim rests on shared-parity plus no-local-event reasoning.
- `QuestBase.Complete*`-calls-`On*` hook direction inferred from sibling patterns and passing tests, not a cached `QuestBase` decompile.
- Production prefix order assumed same-build-deterministic; finding 2's flip is latent.
