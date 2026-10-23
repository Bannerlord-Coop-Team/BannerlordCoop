# #3949 tech-debt review and report, b57cac112

Four lenses completed: fit, defensive, flow and tests. Source review was frozen at b57cac112/tree59747186; the later conflict merge and eight required category attributes are separately reviewed. No product edits or GitHub comments came from this review. QuestLearnings remains unchanged.

## Fix before merge

Nothing material. No demonstrated core-flow defect was found by this debt review; final correctness/category reviews and CI are separate gates.

## Follow-up issue

1. P3: source/GameInterface/Services/Issues/Quests/LadysKnightOut/LadysKnightOutJournalHandler.cs:53 and LadysKnightOutLordHandler.cs:107 flatten influence log TextObject to a server-rendered string. Messages.cs:66/:90 preserve only that string, so an owner using a different language sees server-language influence entries and changing language cannot relocalize them. Keep the existing owner-only issue transport but use TextObject fields and the existing TextObjectSurrogate, which preserves the localization value and attributes. This is a localization limitation, not an owner/history leak; tournament quest logs already use the shared localized route. The fit lens checked exact installed localization DLL/decompiled rendering. Parent verified both wire fields/callers and the existing surrogate. No multilingual live test claimed. Search for journal localization found no matching tracking issue; none filed under report-only scope.

2. P3: source/E2E.Tests/Services/Issues/LadysKnightOutTests.cs:939 covers a successful influence completion's server charge and one finalization, but omits full server/client reward assertions and repeated completion. A future reward omission/duplicate could pass that focused test. Extend that existing test with owner/bystander renown/relation deltas and a duplicate completion request; no new fixture required. Exact influence rewards were measured in up28, which reduces current risk but does not add automated regression coverage. Search for lord solution reward test found no matching tracking issue; none filed.

## Looks bad but is fine

- Pending-lord CampaignReady attempted-flag normalization is quest-specific saved-state handling, not a duplicate of #4055's generic journal archive ownership/filter.
- AllowedThread usages are client message-apply paths behind positive server guards; no authoritative server outcome runs there.
- Tournament participation/revision tracking consumes production tournament snapshots and does not create a second authority.
- Null snapshot/current-round checks handle incomplete external tournament state. No symptom-masking defensive mechanism found.
- Local Honor mirrors and local server-history suppression were removed in favor of #3918/#4055. Shared dependency #4085 constructor fixes are not Lady architecture debt.
- The large E2E fixture exercises acceptance, callbacks, state/order and bystander isolation; line count alone is not a finding.

## Open questions

Whether the shared lord coordinator should eventually capture client difficulty and support Lady's no-counteroffer completion policy is a framework design question. It is not currently a drop-in replacement, so no consolidation or Framework edit is recommended in this PR. Existing disconnect-cancels policy remains unchanged.

Detailed lens reports: tech-debt-b57cac112-fit.md, -defensive.md, -flow.md, -tests.md. Parent verified/deduplicated the two follow-ups. Eight category attributes do not modify the cited code.