# PR #3949 defensive tech-debt pass

Frozen commit: `b57cac1124a4f04a634f56d164819a1055473cba`
Frozen tree: `59747186e122f966fb96295a925d97207509fff0`
Comparison base: `8c8e310fee0ed656f0ff43d22657eee9070bf3c0` (merge-base with the local `origin/development` ref).
Lens: `.agents/skills/tech-debt-review/lenses/defensive.md` only. Candidates are for parent verification; no ranking across lenses.

Purpose: tournament and influence solutions on the shared issue framework, with authoritative owner outcomes, observer isolation and pending joins. The shared trait and saved-journal replacements, intentional pending-lord load normalization, existing disconnect cancellation and #4085 test-constructor dependency are preserved.

## Candidate findings

Nothing material.

- Exception boundaries: no added production `try`/`catch`, log-and-rethrow chain, empty catch, retry or delay. New received-message applies use `GameThread.RunSafe`; they do not nest a second catch inside that boundary. Existing framework catches were checked in context rather than attributed to this PR.
- Always-valid validation: no material finding after tracing the two scan hits and the callers.
- Symptom fixes: no material finding after checking duplicate acceptance, optimistic-quest cleanup and the specified saved-state normalization.

## Looks bad but is fine

- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutTournamentProgress.cs:69`: `currentRound == null` is a valid outcome. The query requires the current unfinished match to contain this owner's slot. A live session can be running another contestant's match, even when this owner has chosen Join. Returning Ongoing avoids manufacturing participation. Confidence: high.
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutTournamentProgress.cs:40`: the snapshot null guard is redundant on the traced production server path: `TournamentSessionHandler.BroadcastSnapshot` filters null before publishing, and the Lady handler ignores client updates. This is one inexpensive redundant guard with no demonstrated bug or material future cost, so it is not promoted into a finding. No further guard or abstraction is warranted. Confidence: high for the traced callers.
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:80` and `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutTournamentHandler.cs:79`: resolving identity, type, ownership and current quest/issue state inside the game-thread action validates data that can actually be stale by application time. These are not null checks on Autofac dependencies or newly constructed values. Confidence: high.
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:143`: the already-applied check and optional stray-quest cancellation handle duplicate delivery/application and a losing optimistic direct accept. They repair the local issue representation using the authoritative accepted solution. The delayed and losing-accept E2E source cases exercise those distinct entry states; no runtime claim is made. Confidence: high.
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:119` and `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:132`: resetting the loaded pending-lord tried flag, then restoring it from ownership, is the explicitly intentional quest-specific normalization. It is not a second history-suppression system. Confidence: high.
- `source/GameInterface/Services/Issues/Framework/Dispatch/StartIssueWithQuestDispatchPatch.cs:17`: the repeat-accept guard prevents another unconditional vanilla quest creation from replacing an existing accepted solution. The pinned vanilla `IssueBase.StartIssueWithQuest` creates and assigns a quest on every call. Treating a repeated dialogue action as already accepted addresses that cause without a retry or delay. Confidence: high.
- `source/GameInterface/Services/Issues/Framework/AcceptCoordination/QuestSolutionAcceptCoordinator.cs:280`: the rejection carries the server's still-open offer deadline and restores it only when no winner owns the issue. This is concrete reconciliation of the optimistic accept, which replaced the local deadline with `CampaignTime.Never`. Existing server accept/rollback catches are preexisting; they were not copied into the new Lady handlers. Confidence: high.
- `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:483`: the new `try` blocks in the test file use `finally` to restore listeners or global context; they do not catch and suppress failed assertions. #4085's constructor updates remain the specified shared dependency. Confidence: high.

## Open questions

Nothing material.

## Evidence and scope limits

Read the root AGENTS, frozen Issues AGENTS, QuestLearnings, CodeStyle, review skill and defensive lens, retained scan, complete changed Lady production files and tests, and connected framework/tournament callers. Vanilla statements above use the existing full cached `pending-lord-fix/vanilla-IssueBase.cs` and Lady decompile. Their recorded CampaignSystem SHA-256 matches the DLL checked during this pass: `1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE`.

The checkout was initially clean at the frozen commit. Another operator advanced HEAD to `03de64df55d300c908f2801103857830716fb80a` during the pass. This report remains pinned to b57cac112: affected shared files were reread with `git show b57cac112:...`, and the Lady files, tournament callers, outcome dispatcher, accept coordinator and GameThread blobs were compared against the frozen commit and remained identical. The newer head is outside this verdict.

Only this external report was written. No source edits, git/GitHub mutations, builds, tests, CI, live/bench actions, extra agents or external model calls were performed. Git status remained clean at the final check. This is a defensive source-review result, not whole-PR correctness, CI or live acceptance evidence.
