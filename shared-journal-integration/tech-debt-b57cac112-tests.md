# PR #3949 tech-debt lens: tests

Report-only candidate handoff for parent verification and deduplication. No cross-lens ranking.

Frozen source: `b57cac1124a4f04a634f56d164819a1055473cba`, tree `59747186e122f966fb96295a925d97207509fff0`.
Authorized origin: `https://github.com/Bannerlord-Coop-Team/BannerlordCoop.git`.
Worktree: `C:/Users/Andrew/CodexWindows/worktrees/BannerlordCoop/3781-ladys-knight-out`.
Comparison: merge base `8c8e310fee0ed656f0ff43d22657eee9070bf3c0` against frozen source, 26 files, +2222/-13. The requested working-tree diff was read while the tree initially matched the frozen source. Another operator advanced HEAD to `03de64df55d300c908f2801103857830716fb80a` during this review. Frozen citations and scope were rechecked with `git show` and an explicit frozen-commit diff; the later merge is excluded.

Read root AGENTS.md, Issues AGENTS.md and QuestLearnings.md, doc/CodeStyle.md, the tech-debt skill and tests lens, the retained scan, changed tests and connected quest/framework/harness code. The scan contained no tests-tagged leads. Tests in Coop.IntegrationTests, E2E.Tests, GameInterface.Tests, Coop.Tests and Common.Tests were searched for existing coverage. Findings below concern added Lady integration, not shared dependencies #3918/#4055/#4085.

## Candidate findings

1. **Missing assertion coverage for the lord solution's complete owner-only outcome.**
   - Location: `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:939`, with the final assertion at `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:945`.
   - Check: missing behavioral tests, rather than test volume or style.
   - Debt: `LordSolutionChargesOnlyTheAcceptedPlayersInfluenceAndCompletesOnce` drives completion once, checks one finalization and the server owner's influence, but does not check the lord reward's renown/relation or their client replicas. The other added lord cases check timing, influence, journals and finalization, not those rewards. The full owner/observer gold-renown-relation assertions at lines 302-315 cover the tournament route, which uses different vanilla consequence code.
   - Concrete future cost/trigger: a regression in the new lord completion path can omit its +7 renown/+5 relation consequence, apply it twice locally, or fail to replicate it, while these tests still pass as long as the server charge and finalization remain correct. Ordinary repeated menu completion is also unexercised by the test named `CompletesOnce`; the duplicate at line 694 replays acceptance, not a completion request.
   - Smallest fix: extend the existing successful lord case, reusing the tournament test's captured hero IDs and before/after values. Assert the owner's influence charge, +7 renown and +5 giver relation on server and both clients after flushing replication, with the other player's values unchanged. Send the same `RequestLadysKnightOutLord` completion a second time and assert the same final values and a single finalization. This needs no new fixture or framework abstraction.
   - Confidence: **high** that these assertions and duplicate completion coverage are absent; **medium** that this warrants a merge-blocking disposition. This is a regression-protection candidate, not an observed runtime defect.
   - Vanilla evidence: cached `pending-lord-fix/vanilla-LadysKnightOutIssueBehavior-honor.cs:237` contains the +7 renown/+5 relation consequence; `pending-lord-fix/vanilla-IssueBase.cs:867` charges influence and invokes it. The installed CampaignSystem DLL hash was checked and matches the cached review provenance: `1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE`.

Nothing else material for this lens.

## Looks bad but is fine

- `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:281`: the native callback test asserts finalization before sending a duplicate request at line 292, then checks actual owner/observer rewards across instances. The old callback-masking concern is resolved on this frozen source.
- `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:57`: the large shared fixture supports real acceptance, patched callbacks, owner context and cross-instance state. Its setup is justified by those paths; splitting it merely to reduce line count would add upkeep.
- `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:501`: the Honor test exercises canonical owner/non-owner stores and checks one notification plus its owner handle, rather than retaining the removed local Honor mirror. Missing standalone retests of the shared #3918 trait service are not new Lady debt.
- `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:994`: CampaignReady and both completion/snapshot orderings exercise the intentional Lady pending-lord tried-flag normalization. Removing that quest-specific normalization is not a recommendation.
- `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:1054` and `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:1104`: copied history and the shared helper provide bounded in-process owner/visibility checks. They do not prove native save serialization/rejoin, but the recipe distinguishes automated coverage from live flows; inherited #4055 persistence machinery is outside this lens's added-debt scope.
- `source/E2E.Tests/Services/Issues/QuestSolutionAcceptRaceTests.cs:58`: pausing the server-to-B link makes the losing optimistic accept reachable after the new known-owner guard. This is a deterministic interleaving, without timing sleeps.
- `source/GameInterface.Tests/Services/Issues/LadysKnightOutTournamentProgressTests.cs:15`: elimination, intermediate progress, spectators, replacement NPCs, previously finished matches and cleanup are valuable state-transition tests, not getter/constructor tests.
- `source/E2E.Tests/Services/HeroDevelopers/HeroSocialParameterCoopCommandTests.cs:89` and `source/E2E.Tests/Services/Heroes/HeroConversationDebugCommandTests.cs:216`: constructor injection repairs are the supplied shared #4085 dependency. No new debug-command test coverage is requested.
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutMessages.cs:56`: the E2E router serializes network messages through `SerializeForWire` before delivery. Successful lord acceptance/difficulty/log paths and stale-offer deadline recovery already exercise their wire fields; duplicate standalone tests of simple message constructors are unnecessary.
- `source/GameInterface/Services/Issues/IssuesModule.cs:40`: the new dependency is registered in GameInterface and used by its quest services. No new Coop.Core server-handler dependency requiring a TestComponentBase mock was added.
- `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:853`: disconnect cancellation is explicitly covered and remains the intended framework behavior. No reconnect redesign is proposed.

## Open questions

Nothing material requiring an author answer within this lens. Exact-head native save/load, real dialogue and LOCAL/up28 runtime acceptance remain the owning operator's evidence responsibility; this source-only pass supplies no runtime verdict.

## Boundaries

No source edits, builds, test execution, live commands, bench operations, CI actions, git mutations, GitHub writes or extra agents/model calls were performed. Only this requested external report was written. Worktree status was clean when checked; its HEAD changed externally as recorded above. QuestLearnings was left unchanged. T3 PR attachment metadata was registered as required by the host instructions; no PR content was modified.