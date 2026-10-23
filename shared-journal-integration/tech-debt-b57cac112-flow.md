# PR #3949 tech-debt review: flow

Frozen commit: `b57cac1124a4f04a634f56d164819a1055473cba`  
Frozen tree: `59747186e122f966fb96295a925d97207509fff0`  
Original merge base: `8c8e310fee0ed656f0ff43d22657eee9070bf3c0`  
Worktree: `C:/Users/Andrew/CodexWindows/worktrees/BannerlordCoop/3781-ladys-knight-out`  
Authorized origin verified: `https://github.com/Bannerlord-Coop-Team/BannerlordCoop.git`

Purpose: enable Lady tournament and influence solutions through the shared issue framework, with owner-authoritative outcomes, observer isolation and pending joins. This pass covers only added or extended flow debt: synchronization, temporary paths and dead code. No cross-lens ranking or full correctness verdict.

The initial checkout matched the frozen commit/tree and was clean. Another operator subsequently advanced it to `03de64df55d300c908f2801103857830716fb80a` and advanced `origin/development`. Remaining comparisons were pinned to the original commit/base. The Lady files, Lady tests, accept coordinator, outcome dispatcher, finalization coordinator, ownership snapshot receiver and tournament implementation read here were compared against the frozen commit; their contents did not change in that advancement. Changed framework context was reread with pinned `git show`. Later feature-gating changes are excluded.

## Candidate findings

Nothing material in the sync invariants, temporary code or dead code checks.

## Looks bad but is fine

- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutJournalHandler.cs:64`, `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:148`, `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:155`: the scan's three `AllowedThread` hits are client receive applies. They replay an owner log or cancel a detached losing optimistic quest. The handlers reject the server role and resolve issues inside `GameThread.RunSafe`. They do not wrap authoritative server outcomes. Confidence: high.

- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:83`, `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutTournamentHandler.cs:88`, `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutTournamentHandler.cs:97`: authoritative consequences run with patches live as the owning hero. The co-op snapshot path uses `RunAsOwners`, which supplies that same substitution scope. No host-party fallback was added. Confidence: high.

- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/Patches/LadysKnightOutEventPatches.cs:42`: the scan's `Hero.MainHero` hit compares the captured war initiator to the quest owner substituted by the shared world-event relay. It does not require a server player party. The captured value is thread-local and cleared by the last postfix. Confidence: high for the traced synchronous relay.

- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:118`, `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:132`: clearing the saved tried flag before ownership is known, then restoring it only for the local owner, is the explicitly retained quest-specific load normalization. It does not replace the shared saved journal owner/visibility implementation from #4055. Confidence: high.

- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/Patches/LadysKnightOutEventPatches.cs:74`, `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutTournamentHandler.cs:59`: the native tournament callback route is connected production code alongside the co-op snapshot route. Native start records the town; elimination or a matching native win publishes the event, the handler sends the request, and the server resolves the current owned issue and runs its branch as the owner. The test at `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:241` checks callback-driven finalization before sending the duplicate request. This is not an orphan message or a test-only compatibility shim. Confidence: high for source reachability; no live claim.

- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutTournamentProgress.cs:43`, `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutTournamentHandler.cs:112`: the server consumes match revisions; terminal outcomes consume their proof and remove quest progress, while session removal clears participation. The `started` output is exercised by focused tests and does not constitute an unused action. No material dead-code or unbounded-lifetime finding established. Confidence: high.

- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/Patches/LadysKnightOutHonorPatches.cs:12`: the retained patch blocks the client's crime consequence. It does not maintain a private Honor mirror; the server path uses the shared trait service under the owner scope after #3918. Confidence: high.

- `source/GameInterface/Services/Issues/Framework/AcceptCoordination/QuestSolutionAcceptCoordinator.cs:280`, `source/GameInterface/Services/Issues/Framework/Dispatch/StartIssueWithQuestDispatchPatch.cs:17`: deadline restoration undoes an optimistic accept only when the server supplied an available-offer deadline and no owner is known. The start guard preserves an existing taken issue, including a pending lord solution, and its postfix avoids sending another accept when the original was skipped. These address real stale-dialogue races rather than adding temporary fallback code. Confidence: high.

- `source/GameInterface/Services/Issues/Framework/Dismissal/ActiveIssueDismisser.cs:104`: disconnect/reload dismissal predates this PR and is explicitly preserved by the brief. It is not new persistence debt to charge to Lady. The #4085 command-constructor changes at `source/E2E.Tests/Services/HeroDevelopers/HeroSocialParameterCoopCommandTests.cs:89` and `source/E2E.Tests/Services/Heroes/HeroConversationDebugCommandTests.cs:216` are the supplied shared dependency, not a new runtime flow. Confidence: high.

## Open questions

Nothing material in this lens.

## Evidence limits

Read root `AGENTS.md`, Issues `AGENTS.md`, `QuestLearnings.md`, `doc/CodeStyle.md`, the tech-debt skill and flow lens, and the retained scan. Read the connected Lady files and shared coordination, dispatch, finalization and ownership paths. Used cached decompiles `3781/vanilla-LadysKnightOutIssueBehavior.cs` and `3781/up25/vanilla-IssueBase.cs` for native callbacks and issue lifecycle. Current campaign DLL SHA-256: `1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE`, matching the installed-source identity recorded in the frozen Lady recipe.

Source-only review. No source edits, builds, test execution, CI, live tools, bench operations, GitHub changes, git mutations, extra agents or model calls. QuestLearnings was not edited. The only file written by this reviewer is this report outside the repository. The concurrent checkout advancement was external to this reviewer and does not change these frozen-scope conclusions.
