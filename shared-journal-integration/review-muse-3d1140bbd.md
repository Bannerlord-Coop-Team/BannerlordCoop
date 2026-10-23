# Delta review: #3949 merge + 8 Lady category attributes at 3d1140bbd

Scope: merge `03de64df5` resolution + commit `3d1140bbd` (8 category attributes in 6 files) only.
Inherited architecture and old excluded archive-history behavior not reopened. No tests or live runs by this reviewer.

## Identity

- origin: `https://github.com/Bannerlord-Coop-Team/BannerlordCoop.git`
- HEAD: `3d1140bbd01d588e18655734f96da083fbf86b43`
- tree: `e6c31dde1f707d2ff806abf32f449ef8cdf28f2d`
- worktree `C:/Users/Andrew/CodexWindows/worktrees/BannerlordCoop/3781-ladys-knight-out` clean (empty porcelain)
- merge parents: `b57cac11` + `e4b38b62e` (#4075). Verified via `git log --format='%H %P'`.
- Read in full: root `AGENTS.md`, `source/GameInterface/Services/Issues/AGENTS.md`,
  `QuestLearnings.md`, `doc/CodeStyle.md`.

## Verdict: source CLEAN, no actionable findings

No Lady method or test behavior changed. The attribute commit is 8 insertions, 0 deletions:

- `LadysKnightOutBranchPatches` (`Patches/LadysKnightOutBranchPatches.cs:8`)
- `LadysKnightOutEventPatches` (`Patches/LadysKnightOutEventPatches.cs:18`)
- `LadysKnightOutWorldListenerPatches` (`Patches/LadysKnightOutEventPatches.cs:58`)
- `LadysKnightOutNativeTournamentPatches` (`Patches/LadysKnightOutEventPatches.cs:72`)
- `LadysKnightOutGenerationPatch` (`Patches/LadysKnightOutGenerationPatch.cs:7`)
- `LadysKnightOutHonorPatches` (`Patches/LadysKnightOutHonorPatches.cs:7`)
- `LadysKnightOutJournalPatch` (`Patches/LadysKnightOutJournalPatch.cs:10`)
- `LadysKnightOutLordPatches` (`Patches/LadysKnightOutLordPatches.cs:13`)

No file matching `*adys*` differs between `b57` and merge `03de64df5`.

## Merge resolutions verified

- `HeroSocialParameterCoopCommandTests`: merge == development (dev-vs-merge diff empty);
  b57-vs-merge shows only the formatting split at lines 89-91. Development version kept. No behavior change.
- `HeroConversationDebugCommandTests`: merge == b57 (b57-vs-merge diff empty). #4085 ctor fix
  retained: `using GameInterface.Utils.Commands;` line 9 and
  `new HeroConversationOpenCoopCommand(Client.Resolve<ICommandObjectResolver>())` line 216.
- `IssuesModule.cs`: keeps both sides. Lady `LadysKnightOutTournamentProgress` line 43 plus
  development `QuestFeatureFlag` line 26 and `IssueDismisser` line 44.
- `StartIssueWithQuestDispatchPatch.cs`: keeps both sides. Development category line 11 plus
  b57 client Prefix (lines 15-27) and Postfix with `__runOriginal` guard (lines 29-43).

## Category loading and Harmony ordering

- `GameInterfacePatchInstaller.Install` (`source/GameInterface/GameInterfacePatchInstaller.cs:29-44`):
  static fixes, then `PatchAllUncategorized`, then flag-gated `PatchCategory(quests)`.
- Flag on only under `TESTER` (`Framework/Gating/QuestFeatureFlag.cs:10-14`); registry empty when off
  (`Framework/Registries/QuestTypeRegistry.cs:18`). The 4 always-on safety patches carry no category
  (verified by grep); `IssuesLayoutTests.Patches_AreInTheQuestCategory_ExceptTheAlwaysOnGates`
  (`source/GameInterface.Tests/Services/Issues/IssuesLayoutTests.cs:78-107`) enforces both directions,
  and the 8 Lady attributes satisfy it.
- Flag-ON equivalence with up28 holds. Challenge considered: `PatchCategory` enumerates the same
  assembly type order as `PatchAll`, so the quest subset (framework + Lady) keeps its b57 relative
  order; explicit `Priority.First` (lord prefixes) and `Priority.Last` (war-initiator clear) are
  order-deterministic regardless of pass. The only always-on/quest shared target touching Lady is
  `CampaignEventDispatcher.OnHeroKilled` (`ClanManagementRefreshPatches.cs:30` void postfix vs Lady
  void prefix/postfix with private `__state`, no skips), which is order-independent. All other shared
  targets (`StartIssueWithLordSolution`, `CompleteIssueWithLordSolutionWithRefuseCounterOffer`,
  `OnWarDeclared`, `OnMapEventStarted`) have their counterparts co-categorized, so they apply and omit
  atomically. `OnSettlementLeft`, `OnIssueLogAdded`, and all `Quest.*` / `LadysKnightOutIssueBehavior`
  targets are Lady-only (alliance/kingdom/siege/UI patches with similar names target different types).
- Flag-OFF: 8 Lady classes omitted with all other quest patches, registry empty, no Lady offers
  (`UnregisteredIssueBehaviorPatches` keeps the behavior off, `LadysKnightOutGenerationPatch` is gone
  with the rest), and the always-on purger dismisses saved taken issues through `IIssueDismisser`,
  not vanilla cancel (`UnregisteredIssuePurger.cs:93-99`).

## DI handlers when off

`ServiceModule.cs:34` AutoActivates every `IHandler`, so the 3 Lady handlers stay subscribed with the
flag off. They are inert: journal/lord/tournament handlers react to Lady-only messages with no
publishers once the patches are omitted. The non-Lady subscriptions (`TournamentSessionUpdated/Removed`,
`CampaignReady`, `NetworkIssueOwnershipSnapshot`) only touch actually-present Lady issues/quests, which
the flag-off purger removes. Residual, not actionable in this delta: `OutcomeDispatcher.RunAsOwners`
(`Framework/Dispatch/OutcomeDispatcher.cs:214-245`) does not filter by registry, so a purge-surviving
running Lady quest could still be driven by a tournament snapshot; that needs vanilla `IssueFinalized`
to throw inside `Dismiss` (caught per-issue and logged), is inherited #4075-framework behavior, and
cannot be reached by cross-flag traffic since the flag is compile-time symmetric.

## Readiness (distinct from source verdict)

- Source at `3d1140bbd` is CLEAN. Runtime evidence remains up28 on `b57` per the supervisor retest waiver.
- Final `3d1140bbd` CI queued; no final pass claimed. Supervisor requires final focused + one full Release
  plus this delta review. Installer/patch/layout tests assert presence/absence only (no exact counts),
  and both test bootstraps register the flag on, so the attributes cannot skew them.
- Owner preservation after disconnect still unrun and unclaimed, as scoped.
