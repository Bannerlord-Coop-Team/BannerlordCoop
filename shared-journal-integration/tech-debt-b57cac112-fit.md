# PR #3949 tech-debt lens: fit

Reviewed commit: `b57cac1124a4f04a634f56d164819a1055473cba`.
Reviewed tree: `59747186e122f966fb96295a925d97207509fff0`.
Authorized origin: `https://github.com/Bannerlord-Coop-Team/BannerlordCoop.git`.
Diff base: `8c8e310fee0ed656f0ff43d22657eee9070bf3c0`, the merge base of the frozen commit and the available `origin/development`.

Scope is debt added or extended by this PR, fit lens only. The retained scan has no fit-tagged hits. Root AGENTS, Issues AGENTS, QuestLearnings, CodeStyle, the tech-debt skill and fit lens were read. Whole Lady production files and the connected quest acceptance, lord coordination, finalization, ownership and text serialization paths were inspected. Existing test source was read for intent; no tests were executed.

The shared checkout advanced externally during this pass, first observed at `03de64df55d300c908f2801103857830716fb80a`. Candidate locations were rechecked with `git show b57cac112...:<path>`. The journal handler, lord handler, message definitions, Lady test file, shared lord coordinator, shared finalization coordinator and TextObject surrogate have no differences from the frozen commit in that observed checkout. This report does not review the subsequent merge.

## Candidate findings

### FIT-1: Keep lord journal text in the existing localized TextObject format

- **Check:** Existing frameworks are used; avoid a parallel representation that loses information.
- **Primary location:** `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutJournalHandler.cs:53`.
- **Connected locations:** `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:107`, `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutMessages.cs:66`, `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutMessages.cs:90`, `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutJournalHandler.cs:66`, and `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:155`.
- **Debt and concrete trigger:** Both the influence start journal and later issue logs are rendered with `LogText.ToString()` on the server, sent as strings, then wrapped in new `TextObject`s on the owner client. With an English server and an owner using another supported language, these entries have already lost their localization keys and attributes. The owner therefore receives the server's rendered wording, and those stored entries cannot be translated when the owner's language changes. The shared quest journal already sends `TextObject[]` in `Framework/Finalization/NetworkIssueJournal.cs:14`, and `Surrogates/TextObjectSurrogate.cs:27` preserves `.Value` plus nested text, integer and string attributes. Lady's issue journal extends the older string-flattening pattern instead of using that existing capability.
- **Smallest fix:** Change `NetworkLadysKnightOutLordAccepted.StartLog` and `NetworkLadysKnightOutLog.Text` to `TextObject`, send the existing log text without `ToString()`, and pass the received objects directly to `JournalLog`. Retain the existing owner filtering and message/apply order. This does not require changing the pending-lord normalization or the shared journal framework.
- **Confidence:** High, 0.98. This is a source and installed-DLL finding; no multilingual runtime result is claimed. Parent should verify and deduplicate before disposition.

Installed vanilla evidence was inspected before making the rendering claim. The focus-safe local decompile fallback produced complete `TextObject` and `MBTextManager` sources from `TaleWorlds.Localization.dll`, SHA-256 `D75A5C89C26A09E587FAB55C182B2962BB36708B4766A922BDF1F46A28212628`, file version `1.0.0.0`, product version `1.0.0`, installed Native module `v1.4.8`.

- `tech-debt-b57cac112-fit-TextObject.cs:197`: `ToString` calls `MBTextManager.ProcessTextToString`.
- `tech-debt-b57cac112-fit-MBTextManager.cs:106`: rendering resolves the localized source text before processing variables.
- `tech-debt-b57cac112-fit-MBTextManager.cs:232`: localization uses the `{=...}` key and active language; already-rendered plain text has no such key.

No other material fit candidates.

## Looks bad but is fine

- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:119` and `:132`: clearing the saved tried flag before ownership arrives, then restoring it only for the owner, is the explicitly retained Lady-specific load normalization. It is not a replacement archive ownership mechanism. No removal suggested.
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:84` and `:86`: the local lord path cannot simply be deleted in favor of the unchanged shared coordinator. It carries client difficulty and completes an already accepted solution after influence drops; the shared request has no difficulty field and its refusal branch checks influence again. Existing Lady tests at `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:800` and `:1109` explicitly preserve those differences. Consolidation is an open question below, not a claim that the shared path is a drop-in replacement.
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutTournamentProgress.cs:32`: the participation map records which live match the quest observed and which revision was consumed. Results still come from the existing tournament snapshots. This is quest eligibility/freshness bookkeeping, not a competing tournament authority or registry.
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutDescriptor.cs:15` and `LadysKnightOutQuestSolutionAcceptStrategy.cs:7`: descriptor/strategy classes, including the empty capture strategy, follow the existing quest registration contract. Interface-per-class and Autofac conventions are not unnecessary layers here.
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/Patches/LadysKnightOutHonorPatches.cs:13` and `source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:435`: the private Honor mechanism is gone; the patch only gates the client crime body and the scenario reads the shared canonical store. The shared saved-journal path is likewise used by the late-join history scenarios at `LadysKnightOutTests.cs:1024` and `:1079`. Neither removed local workaround is reported as current debt.
- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/Patches/LadysKnightOutEventPatches.cs:64`: `AccessTools.DeclaredMethod` selects Harmony targets from method names. It does not introduce reflection-based field access in place of publicized bindings.
- `source/E2E.Tests/Services/Heroes/HeroConversationDebugCommandTests.cs:216`: the shared #4085 constructor adaptation is a supplied dependency. It is not a Lady-specific framework invention.

## Open questions

- `source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutLordHandler.cs:71` and `source/GameInterface/Services/Issues/Framework/AcceptCoordination/LordSolutionCoordinator.cs:90`: is a shared follow-up intended to cover client difficulty capture and the no-counteroffer completion policy, so Lady can reuse lord acceptance/completion transport? Both paths currently own request resolution, ownership checks, owner substitution, acceptance broadcasts and client replay. Future changes to those common contracts need updates in both places. The behavioral differences above are real, and a safe consolidation was not established in this report-only pass, so this is not asserted as a merge finding or a request to rewrite the framework in this PR.

## Evidence limits and write boundary

Report-only fit pass, with no ranking across lenses. No source edits, QuestLearnings edits, builds, tests, CI actions, gameplay, bench operations, git mutations or GitHub writes were performed by this reviewer. Only this external report and the two external decompile artifacts were created. App PR-link metadata was registered as required by the host. No additional agents or model calls were used. Existing disconnect cancellation is preserved. Runtime acceptance and current-head readiness belong to the parent/operator.
