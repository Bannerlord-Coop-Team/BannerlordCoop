# Final reviewed #3958 full Release: passed

Job `docker-ci-5ac79ce07653451c8953` on HIVE. Started `2026-10-23T10:20:58.500122+00:00`; completed `2026-10-23T10:37:12.931359+00:00`.

Head `bd74a2d5323edd542faebc14b4b9802e6db81184`; tree `311491b1b93a9b28a6edf83fc2dab4fdb5981cd6`; source clean and matching origin. Required ancestor9e85bfe3c verified. This is the one ordinary full Release authorized after final source reviews, with no selectors, focused Debug or Debug supplement.

Actual job-bound bundle `0fb4bda63dd3183048dc523e55bb5469dfc2d1eff28a14dd184fa4806a202cc9`. Archive `4221b0529a1247b592b9908c41fd7e87167bdd117d5a34819b4265dc92b60815`.

## Results

| Scope | Total | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Units | 4010 | 4008 | 0 | 2 |
| E2E | 2872 | 2871 | 0 | 1 |
| All completed TRX | 6882 | 6879 | 0 | 3 |

| Required suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| LesserNobleRevoltTests | 26 | 0 | 0 |
| EndingJournalTests | 13 | 0 | 0 |
| NestedQuestEndingTests | 7 | 0 | 0 |
| ScoutEnemyGarrisonsTests | 15 | 0 | 0 |
| ArtisanOverpricedGoodsTests | 31 | 0 | 0 |
| VillageNeedsCraftingMaterialsTests | 18 | 0 | 0 |

Both LNR timeout regressions and strengthened crafting timeout:
- Passed: `E2E.Tests.Services.Issues.LesserNobleRevoltTests.OwnersHourlyTick_DispatchesTheTimeoutToTheServerOnce` (`artifacts/e2e-2/aisha_HIVE_2026-10-23_05_23_14_net6.0.trx`).
- Passed: `E2E.Tests.Services.Issues.LesserNobleRevoltTests.ServerEnding_AddsItsJournalLineOnceToTheOwnersJournalOnly(ending: TimedOut, outcome: QuestTimeOut, payments: 0, traitChanges: 1)` (`artifacts/e2e-2/aisha_HIVE_2026-10-23_05_23_14_net6.0.trx`).
- Passed: `E2E.Tests.Services.Issues.VillageNeedsCraftingMaterialsTests.OwnersTimeout_RunsTheFailConsequencesOnTheServerOnly` (`artifacts/e2e-4/aisha_HIVE_2026-10-23_05_29_33_net6.0.trx`).

All six LNR ending-journal cases and both absent-owner neutral cancellation cases passed. The latter retain exact localized reason assertions and zero quest rewards/penalties, not a world-loot amount claim.

| E2E shard | Total | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| 0 | 287 | 287 | 0 | 0 |
| 1 | 316 | 316 | 0 | 0 |
| 2 | 337 | 337 | 0 | 0 |
| 3 | 590 | 589 | 0 | 1 |
| 4 | 343 | 343 | 0 | 0 |
| 5 | 334 | 334 | 0 | 0 |
| 6 | 355 | 355 | 0 | 0 |
| 7 | 310 | 310 | 0 | 0 |

Discovery: 2872 cases; TRX result rows 2872 (including the one existing skipped E2E row); missing 0; unexpected 0.

Required repository coverage:
- PatchTest: 7 passed, 0 failed, 0 skipped.
- IssuesLayoutTests: 3 passed, 0 failed, 0 skipped.
- ContainerBuildTests: 4 passed, 0 failed, 0 skipped.
- CoopTests: 1366 passed, 0 failed, 0 skipped.
- Issues: 374 passed, 0 failed, 0 skipped.
- IssuesUI: 174 passed, 0 failed, 0 skipped.

Plan/source/base/digest binding validated. Plan receipt `blocked-external-runtime`; process-peer `passed`, 7 scenarios and every required check true. External runtime profiles `['rendered-smoke', 'full-live']` remain separate from automated test evidence; parent retains the accepted up23/public-manifest disposition.

Failed tests:
None.

Existing skips:
- E2E.Tests.Services.ItemRosters.ItemRosterSyncTests.Server_ItemRoster__count (artifacts/e2e-3/aisha_HIVE_2026-10-23_05_23_14_net6.0.trx)
- Coop.IntegrationTests.Template.TestTemplate.ClientReceivesTemplateEventMessage_PublishesTemplateCommandMessage_AllClients (artifacts/unit/aisha_HIVE_2026-10-23_05_22_05_net6.0[4].trx)
- Coop.IntegrationTests.Template.TestTemplate.ServerReceivesTemplateEventMessage_PublishesTemplateCommandMessage_AllClients (artifacts/unit/aisha_HIVE_2026-10-23_05_22_05_net6.0[4].trx)

## Integrity and boundary

All 309 result-manifest members verified by size/SHA256, with zero duplicate paths, unmanifested files or duplicate execution IDs. Package archive/script/changed-path hashes, canonical archive source receipt, native ownership job hash, terminal result hash, TRX counters and terminal totals agree. Every TRX assembly and native E2E command uses Release.

Manifest SHA256 `cb12f827ae4f0870ced8ece764a8ba1cc02c9a8661355eb46987fcb8951f7443`. Result SHA256 `fcf6e1e6c8f9d3821a87823113c2c6f48c054fe2aa8a70bcf4be3bd044f03c1e`.

Native cleanup `{'schema': 'native-ci-workspace-cleanup.v1', 'jobId': 'docker-ci-5ac79ce07653451c8953', 'ownershipSha256': '9e963955fb4720c7b9c7ded88653f78a96d7d2dd252fb6b46ba96a244b9fc189', 'gameReferenceDetached': True, 'e2eSlotsRemoved': True, 'retainedFailureWorkspace': None}`. Single existing remote.sh wait/collect producer retained; both actions exit0 in producer-exits.json. The enclosing shell exit1 is separately preserved in producer-wrapper-exit.json: the final test comparison has a trailing CR in its literal 0, so it compares 0 with 0CR after successful wait/collect. No action was replayed. A custom background-launch attempt was rejected before execution; managed exec session74550 ran the existing wait/collect command instead. No duplicate submission or collector was started.

No source, development merge, local .NET, live/kit/bench, shared-helper, memory, child or PR-state changes were made. Legacy workflow1add revision44/pipeline remained untouched. Prior facfa6879-pass at6e268 remains historical and was not overwritten, as do prior34eb infrastructure and68160 failed/missing-shard receipts. The other worker owns #4068 and its independent FIFO job; none of its state was mutated. Parent owns READY gates, dismissal and reviewer requests. This result does not promote either PR.
