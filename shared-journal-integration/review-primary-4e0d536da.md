# CHANGES REQUESTED: PR #3949 / issue #3781

Exact-head source review: two P2 findings and one P3 conformance finding. The shared owner-scoped archive integration itself is sound on the traced paths; the retained quest-log transport conflicts with the new ending snapshot transport for clients using another language.

## Frozen scope and identity

- Repository: `https://github.com/Bannerlord-Coop-Team/BannerlordCoop.git`.
- Reviewed commit: `4e0d536da3a48a1c5cddf5b5aeeb7a8763cbc438`.
- Reviewed tree: `a617ca0e0493240971a8590549b6b149073177ef`.
- Frozen development/base: `7e59432341d1a5ed215943bd19ee1e563d5951a3`. At review entry, both `origin/development` and the merge base matched this commit. Reviewed all 24 PR paths against this frozen base.
- Focus: `1f7644df8c725cd3e021aa3ba7a73a780d665763..4e0d536da3a48a1c5cddf5b5aeeb7a8763cbc438`, plus the development merge from `e5d2345f99e7d785c9fa3391a1b323c81ec9add5` to `1f7644df8c725cd3e021aa3ba7a73a780d665763`. Lady source/tests were unchanged by that merge itself; shared framework changes were imported.
- Both `9e85bfe3c` and #4055 merge `b16d3d16ae7dd7325944ab3ea79c763d0a7d4bb1` are ancestors of the reviewed commit.
- Entry status was clean. No source edits, builds, tests, CI submissions, runtime/lane actions, GitHub mutations, or delegation were performed. This report is the sole authored filesystem artifact.

## Findings

### 1. P2, high confidence: prime participation before the two tournament-history cases

Anchor: `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:1097`.
Disposition: changes requested. New in the focus increment.

For `ending=1` and `ending=2`, the new theory sends only `Snapshot(..., finished: true, ...)`. Its fresh progress service has no participation record. `LadysKnightOutTournamentProgress.Observe` therefore cannot enter the recorded-match completion path at lines 43-59 and returns `Ongoing` at lines 63-65 because the snapshot phase is `Completed`. `LadysKnightOutTournamentHandler` consequently skips the branch. The history helper then requires a single `NetworkIssueFinalized` at `QuestJournalHistoryAssertions.cs:27`, but these two cases have not produced one.

Smallest fix: publish the matching unfinished/live snapshot first, then the finished snapshot, through the same production handler. The existing `TournamentResultRunsTheRealBranchAsItsOwner` test already does this at lines 192-196. Keep the helper's finalization assertion; do not replace the actual tournament path with direct completion or a seeded outcome. This conclusion is a static execution trace, not an executed test result.

### 2. P2, high confidence: remove the duplicate quest-log route before using framework ending snapshots

Anchor: `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutJournalHandler.cs:69`.
Disposition: changes requested. The handler predates the focus increment, but its conflict is introduced by merging the shared ending snapshot framework.

Trigger: an English-language server completes the owner's tournament quest while that client uses German, or another installed language. Lady's `OnQuestLogAdded` transport renders `LogText.ToString()` on the server and the receive path adds `new TextObject(data.Text)` at line 84. This gives the owner a literal English ending line. The new framework subsequently sends the original localized `TextObject` in `NetworkIssueJournal` before finalization. `TextObjectSurrogate` retains `Value` and attributes, including its translation ID. `FinalizationCoordinator.cs:211-222` reconciles by each entry's rendered `ToString()` on the client. The literal English line and the localized German line differ, so both are retained and archived.

This is a normal cooperative-client path. FIFO application does not prevent it. Same-language tests compare identical rendering and miss the mismatch; the new history helper does not count ending lines.

Smallest fix: remove Lady's quest-log event/postfix/send/replay path and let the shared ending snapshots deliver quest ending logs. The inspected vanilla Lady quest has its start log on the owner's accept path and server-generated logs on endings, which the framework captures. Retain the lord issue-log route: `FinalizationCoordinator.cs:185` explicitly excludes lord solutions from its snapshot transport. If the quest route is retained instead, it must carry a `TextObject` rather than rendered text and reconcile without creating a second ending entry.

Evidence: cached complete installed `TextObject` and `MBTextManager` decompiles show locale-sensitive rendering and literal strings without IDs staying literal. Installed SandBox language XML contains distinct English and German text for Lady success ID `QAg8DQy6`. No locale test was executed.

### 3. P3, high confidence: send the retained personal lord log to its owner peer

Anchor: `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutJournalHandler.cs:50`.
Disposition: conformance change requested. New in the full PR, already present before the focus increment.

Every lord completion log is broadcast with `SendAll`, although only the matching controller consumes it at lines 79-80. This contradicts QuestLearnings.md's explicit instruction to send owner-only journal/state messages to the owning peer and sends unused journal traffic to every other player. The quest-log broadcast at line 65 has the same issue if it survives finding 2.

Smallest fix: resolve the existing owner controller to its peer through `IPlayerManager.TryGetPeer` and use `network.Send(peer, message)`, following the shared finalization handler. No malicious-client or confidentiality claim is involved.

## Touched-rule dispositions and verified source paths

- Required root `AGENTS.md`, Issues `AGENTS.md`, `QuestLearnings.md`, and `doc/CodeStyle.md`: read in full. Authorized repository and native Windows scope verified.
- Shared archive ownership: statically satisfactory. Owner resolution maps substituted `Hero.MainHero` through the controlled-object/player registry. Direct accept, tournament branches, and crime run inside the owner's `MainHeroSubstitutionScope`. Lord start creates its first archive inside that scope. Vanilla completion reuses that archive; the quest overload avoids rewrapping an existing `PlayerJournalLogEntry`. The saved controller field survives independently of the active issue registry, and client `IsEnded` visibility compares the saved controller to the local controller. The server is not filtered out.
- Removed Lady `HistoryPrefix`: correct. It would suppress the server completion update that the shared persisted owner archive now needs. Do not restore it.
- Removed reward and notification gates: correct. Shared `DisableIssuesCampaignBehavior.OnIssueUpdated` blocks client rewards/cooldowns, and `ServerQuestNotificationPatches` supplies the server notification gates. Non-owner framework replay finalizes a questless mirror directly instead of attributing an issue update to the observer.
- Pending-lord clear/snapshot restore: no conflicting ending or reward path found. It normalizes the player-local attempted flag inherited from a server save; #4055's archive ownership does not normalize that active issue field. New non-owner replay also avoids the old issue-update leak independently. Retaining the normalization does not bypass server authority or alter persisted archive ownership. Both snapshot/finalize ordering cases remain present.
- Journal transport: lord issue logs remain necessary because the shared handler skips lord solutions. Quest ending transport duplicates a shared mechanism and has finding 2. Owner-peer routing has finding 3.
- Honor: prior correction preserved. Crime runs vanilla's `-50` through the shared trait service in the owner scope; the client crime prefix prevents local replay. No private Honor mirror is present or recommended.
- General structure: descriptors and strategies remain quest-local; shared acceptance changes are generic, not quest-type references. Receive-side game-state lookups are inside `RunSafe`; handlers unsubscribe through `IHandler`/`IDisposable`; snapshot arrays handle null; publicized game members are accessed directly. Patch target selection via Harmony's `MethodBase` lookup is not reflective access to a publicized state member. `git diff --check` passed for the frozen comparison.
- Tests: the strengthened lord archive assertions now require a real server-owned ended entry, observer invisibility, and owner visibility after transfer. The crime case drives the real server method. The two new tournament cases are invalid per finding 1. The history-transfer helpers copy log objects in process and do not independently establish serialized save/download/reconnect behavior.
- PR metadata: the live PR remains draft and retains `## Bot Changelog Entry`. At inspection it still pointed to `e5d2345f99e7d785c9fa3391a1b323c81ec9add5`; this report reviews the specified local integration candidate, not the published head.

## Vanilla identity and evidence limits

Installed Native module metadata is `v1.4.8`. Installed CampaignSystem SHA-256 matches `1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE`. Used the supplied full Lady, IssueBase, JournalLogsCampaignBehavior and trait decompiles. The complete cached QuestBase decompile matches installed CampaignSystem mtime/size. Complete cached TextObject/MBTextManager decompiles match installed Localization mtime/size (`1786373637`, `211416`); installed Localization SHA-256 is `D75A5C89C26A09E587FAB55C182B2962BB36708B4766A922BDF1F46A28212628`.

Source verdict: **CHANGES REQUESTED**. Readiness: **NOT ESTABLISHED**. Focused CI is queued per the supplied brief; no pass is inferred, and full Release results were not verified. Final-head live is **NOT RUN**. The original up27 overall PASS is superseded; historical crime/failure/Pending A observations and the separately reproduced generic archive bug are not final-head acceptance.

During review the checkout advanced externally to `610566fc1e7d186cf67aa0e6104d38c965279290`, and `origin/development` later advanced to `8c8e310fee0ed656f0ff43d22657eee9070bf3c0`. The observed checkout remained clean; its only changed paths versus the reviewed commit were two unrelated merged E2E fixture files. After noticing movement, remaining anchors and critical paths were checked with immutable `git show 4e0d536da:...`. This report grants no verdict to those newer commits or that newer base.