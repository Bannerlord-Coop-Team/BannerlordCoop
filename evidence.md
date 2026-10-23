# Lesser Noble Revolt: R4 owner loss and observer isolation

Tested source `6e268638332dc9c89111b04aea9640b67f6e8ec6`, tree `5d81be457582f5ea3fb3539d435b86729052f06c`. Local up22 `issue3783-20261023T073410` and up23 `issue3783-20261023T082604` used a dedicated server and two real clients. B/testclient2 owns the quest; A/testclient observes.

Current main `bd74a2d5323edd542faebc14b4b9802e6db81184` changes only two blank lines in tests. Every other tracked blob is identical to the tested source. Release and live results keep their actual tested SHA; no new-head run is claimed. Dependency #4068 own snapshot is `3de45705a7ec72f6364eef301f66d8fb3c6b3d5c`, separate from these combined-head results. Its FinalizationCoordinator blob matches the tested combined source; its new tests await parent adoption and final Release.

## Receipt-backed before and after

| Observation | Before | After | Retained receipt |
| --- | --- | --- | --- |
| Owner acceptance | Real choice sent | B owns issue_170_quest, ongoing and registered | step-035, step-037/039 |
| Observer personal quest | None | None; no Tuvul native journal entry | step-038, step-081, observer-journal-no-entry |
| Owner gold | 1,000 | 950; ordinary battle loss 50, no quest gold reward | step-080/082/083 |
| Owner renown | Baseline | Delta 0; no quest renown reward | step-080/082 |
| Owner giver relation | 0 | -5 | step-080/082 |
| Owner Honor XP | 0 | -15; derived/replicated level remains 0 | step-080/082 |
| Observer gold, renown, relation, Honor XP | Baseline | All deltas 0 | step-080/081 |
| Owner journal | Active quest established by receipt, not a before-journal image | Old Quests 3; Tuvul failed; opening and complete loss ending each once | step-039/082, owner-journal-select, owner PNG |
| Observer journal | No quest established by receipt | Active 0, Old 2 seed Mitunis entries; no Tuvul | observer-journal-no-entry, observer PNG |
| Exact revolt party | Active, world and both registries present | Native destruction callback 1 on each peer; absent from all three worlds and both ID/handle registries | coverage-baseline/before/after/final |
| Real rural notable | Mitunis of Tubilis, Hero_CharacterObject_2275; both player relations 0 | Both remain 0 on server/A/B | rural-before/after/final |

Logical server send counts are one each for the matching NetworkApplyDestroyParty, NetworkDestroyInstance and NetworkIssueFinalized; A/B each receive one. OwnerBattleEnded reaches B once and A zero times. These are logical invocations/publications, not raw UDP datagram counts. Gold loss 50 is ordinary battle loot loss and must not be described as a quest penalty. Quest gold and renown rewards are zero.

The five images below were independently opened by the actual parent and accepted for their stated captions. The two 40-r4-accepted images are context only: they show neither an active quest nor its journal. Acceptance and active state are supported by the strict receipts. No journal-before image or unviewed penalty image is approved.

## Parent-inspected images

![owner B: pre-accept offer](shots/30-r4-offer-rd-testclient2-21176.png)

Oragur visibly offers 5,400 gold and the player can choose 'I'll find them and put a stop to what they're doing.' Gold is 1,000. This is the offer before acceptance.

SHA256 `ca8d69bd73f330f3ce890ea72c7a8a7a779c037dd6481c510d21205c90a7bd48`.

![observer A: pre-ending context](shots/40-r4-accepted-rd-testclient-69600.png)

Observer A on the Quyaz map, gold 1,000. Pre-ending context only. Associated strict receipts establish B owns an active quest and A has no quest; those states are not visible in this image.

SHA256 `41c0a1e717c9e56040eb8ea563b88093fffcc2e8543a51470540758279312ec8`.

![owner B: pre-ending context](shots/40-r4-accepted-rd-testclient2-21176.png)

Owner B in the Ortongard town menu, gold 1,000. Pre-ending context only. The accepted, active quest is receipt-backed; neither a quest nor its journal is visible here.

SHA256 `7cc1a94af901aefa8bdf0a4d70a4000f11d9db3322a6d65e2431c3a9dfc2b48d`.

![owner B: after ending](shots/owner-failed-Tuvul-ending-21176.png)

Owner B: Old Quests 3 and Tuvul's Revolt failed, with its opening and full battle-loss ending each once. The other seed row stays highlighted while the details show Tuvul.

SHA256 `9dcf196524084017652c9498050288f0111d472bf0bbfabd524d89b1d4f4d300`.

![observer A: after ending](shots/observer-no-Tuvul-entry-69600.png)

Observer A: Active Quests 0, Old Quests 2, only existing Mitunis seed entries. No Tuvul entry. The specific journal lookup also returned no matching native entry.

SHA256 `19708bc3cb20f55a56b335251a12d2459d9fc9a14aa2b6298ea4efe1f4fc29bb`.

## Release and limits

Full Release HIVE docker-ci-facfa60976e24ed39c5c at tested 6e268: 6,879 passed, 0 failed, 3 existing skips; all eight shards and all 2,872 discovered E2E cases executed. LNR 26, EndingJournal 13, NestedQuestEnding 7, Scout 15, Artisan 31 and crafting 18 pass, including both LNR timeout regressions. No supported focused Release selector exists; these checks came from the one full Release job. Source, repaired runner bundle0fb4bda6 and all309 manifest members were verified.

Up23 RESULT, the original267-member manifest, selected strict receipts and source-equivalence receipts are included. Cleanup verified all owned processes absent and the bench released. No new CI/live/kit run was performed for this package. Final Codex/Muse review and the parent's adoption/final Release remain pending. This image selection proves the R4 loss/owner-isolation flow; it does not establish every enum branch, accepted-quest rejoin, or full live coverage at #4068's own SHA. Earlier-source evidence is not relabeled final-source.

Run-local publication is explicitly authorized by Supervisor09:58. Legacy workflow1add revision44 and its canonical pipeline are untouched. This package contains no broker acceptance or replacement workflow.
