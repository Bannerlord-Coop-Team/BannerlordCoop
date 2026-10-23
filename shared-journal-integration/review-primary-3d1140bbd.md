# PR #3949: final delta review

**VERDICT: SOURCE CLEAN, within the requested merge-resolution and eight-attribute scope.**
No actionable trusted-client correctness findings. Confidence: high for these source conclusions. Release readiness is not established by this review.

## Verified identity

- Authorized origin: https://github.com/Bannerlord-Coop-Team/BannerlordCoop.git
- Worktree: C:/Users/Andrew/CodexWindows/worktrees/BannerlordCoop/3781-ladys-knight-out
- Branch: issue/3781-ladys-knight-out
- HEAD, tracked upstream, live remote branch and live PR head: `3d1140bbd01d588e18655734f96da083fbf86b43`.
- Tree: `e6c31dde1f707d2ff806abf32f449ef8cdf28f2d`.
- Local origin/development and live remote development: `e4b38b62eadfa8ed1cc4d312cb8999f2f2c99e49`.
- Merge `03de64df55d300c908f2801103857830716fb80a` has parents `b57cac1124a4f04a634f56d164819a1055473cba` and that development commit.
- Working tree/index clean at initial and final source checks. No fetch, checkout, reset or other git mutation.

## Delta checks

The branch was compared with origin/development for context. The final commit changes six files with exactly eight added lines, all `[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]`:

- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/Patches/LadysKnightOutBranchPatches.cs:8`
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/Patches/LadysKnightOutEventPatches.cs:18`, `:58`, `:72`
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/Patches/LadysKnightOutGenerationPatch.cs:7`
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/Patches/LadysKnightOutHonorPatches.cs:7`
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/Patches/LadysKnightOutJournalPatch.cs:10`
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/Patches/LadysKnightOutLordPatches.cs:13`

Removing those exact lines yields identical line content to b57 in every file. The merge changed no Lady implementation or test content; the final commit changes no method, target, priority or test.

Both conflict resolutions are correct:

- `source/E2E.Tests/Services/HeroDevelopers/HeroSocialParameterCoopCommandTests.cs:90` matches development exactly and retains both constructor arguments. The constructor is at `source/GameInterface/Services/HeroDevelopers/Commands/HeroDeveloperCommands.cs:213`.
- `source/GameInterface/Services/Issues/IssuesModule.cs:43` retains Lady tournament progress; `:44` retains development's IssueDismisser registration.

The separate #4085 correction remains at `source/E2E.Tests/Services/Heroes/HeroConversationDebugCommandTests.cs:216`, supplying the resolver required by `source/GameInterface/Services/Heroes/Commands/HeroConversationDebugCommand.cs:33`. It is unchanged from b57 and remains a necessary branch difference from development.

The auto-merged `source/GameInterface/Services/Issues/Framework/Dispatch/StartIssueWithQuestDispatchPatch.cs:11` has the new category. Its request prefix at `:16`, successful skipped-original result at `:23`, and postfix `__runOriginal` guard at `:30` remain intact.

## Category loading and ordering

`source/GameInterface/GameInterfacePatchInstaller.cs:37` installs static fixes, then uncategorized patches, then quests only when enabled at `:40`. All eight classes retain HarmonyPatch annotations and enter that quest pass. `source/GameInterface/Services/Issues/Framework/Gating/QuestFeatureFlag.cs:10` enables it under TESTER; Debug defines TESTER at `source/GameInterface/GameInterface.csproj:24`, and nightly passes it at `.github/workflows/nightly-release.yml:229`.

This changes installation phases; identical global insertion order is not assumed. The effective Lady patch methods are preserved with the flag on. The relevant explicit order remains: Lady lord prefixes use Priority.First at `LadysKnightOutLordPatches.cs:19` and `:29`, ahead of the generic default-priority prefixes in `source/GameInterface/Services/Issues/Framework/Dispatch/LordSolutionDispatchPatches.cs:19` and `:27`. War-context clearing retains Priority.Last at `LadysKnightOutEventPatches.cs:34`, after the generic war relay. Other inspected shared-target callbacks expose no new Lady ordering dependency.

Harmony provenance: project dependency Lib.Harmony 2.4.2, local package XML, and upstream tag v2.4.2.0:
- https://github.com/pardeike/Harmony/blob/v2.4.2.0/Harmony/Public/Harmony.cs#L120
- https://github.com/pardeike/Harmony/blob/v2.4.2.0/Harmony/Serialization/PatchInfoSerialization.cs#L114

The latter sorts by priority first and insertion index only for ties. No runtime patch inventory or new vanilla behavior claim is asserted.

With the flag off, the installer omits all eight classes and `source/GameInterface/Services/Issues/Framework/Registries/QuestTypeRegistry.cs:18` leaves the registry empty. The four safety patches remain uncategorized and match development. `UnregisteredIssueBehaviorPatches.cs:93` blocks unregistered behavior event registration. `source/GameInterface/Services/Issues/Framework/Dismissal/UnregisteredIssuePurger.cs:86` removes idle issues and `:94` dismisses taken ones through IIssueDismisser.

Handlers still AutoActivate through `source/GameInterface/Services/ServiceModule.cs:34`; they are not disabled wholesale. Their constructors subscribe only. Lady requests/replays resolve through `source/GameInterface/Services/Issues/Framework/Resolvers/IssueResolver.cs:46`, which rejects the empty registry. Tournament updates still subscribe, but normal saved taken issues are removed by the server purger before play. No concrete flag-off correctness regression was identified within this delta.

## AGENTS conformance and evidence limits

Read root AGENTS.md, Issues/AGENTS.md, QuestLearnings.md and doc/CodeStyle.md in full. The eight attributes meet the quest-category rule. No new reflection, server AllowedThread scope, host-party assumption, framework modification or debug-command finding is introduced by this delta.

No product edits, builds, tests, CI operations, live runs, bench access, git mutations, GitHub posts, delegation or additional model calls were performed. This report is the sole filesystem write, outside the product tree. PR linkage was registered in T3 as required by the harness.

Readiness remains separate. The supplied up28 report and owner-tag assessment were read: four gameplay flows passed on b57; strict all-endpoint owner tagging remains raw PARTIAL, and owner disconnect/rejoin is UNRUN. No owner preservation after disconnect is claimed. Inherited archive-history behavior was not reopened.

The supplied 18:15Z retest waiver covers only the eight attributes. Up28 remains b57 runtime evidence, not a final-head live run. The brief reports final-head CI queued and requires final focused validation plus one fullRelease; those results were not checked or established here. Earlier b57 totals do not establish final-head passage.
