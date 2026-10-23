# Final CI for #3949 on 3d1140bbd

Focused Debug: **89 passed, 0 failed, 0 skipped**. Single full Release: **7,121 passed, 0 failed, 3 skipped**. All eight E2E shards executed.

- Head: `3d1140bbd01d588e18655734f96da083fbf86b43`
- Tree: `e6c31dde1f707d2ff806abf32f449ef8cdf28f2d`
- Source archive SHA-256: `22ed1a8d3bb6d254a6859fc92b3b721bdfc7cb03b1e088e0d0ce0de50a129fa3`
- CI script SHA-256: `7eddda58d6e9438811ce7ea98c904e8d217664b88492080999125d473d00f540`

## Focused Debug, previously verified

Job `docker-ci-4524a63b7deb41d78349`, HIVE, Debug; 2026-10-23T18:09:10.266602+00:00 to 2026-10-23T18:12:16.526736+00:00. Existing verified evidence consumed without repeating tests. Source identity, 111 manifest-listed artifact hashes, requirement/TRX hashes and actual per-class execution match. Debug supplement accepted. This focused profile alone is not Stage 5 evidence.

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Coop.Tests | 4 | 0 | 0 |
| E2E.Tests | 68 | 0 | 0 |
| GameInterface.Tests | 17 | 0 | 0 |

Eight-class selection: containers 4; social command 4; conversation Debug command 12; Lady 49; E2E journal history 3; GameInterface patch constructors 8, issue layout 4, journal history 5.

## Single full Release

Job `docker-ci-6c925df2849f48ef9674`, HIVE, `windows-native-v1`, Release; 2026-10-23T18:29:24.922955+00:00 to 2026-10-23T18:45:33.287579+00:00. Exactly one final full submission. The existing accepted ID and package were retained across the interruption. No resubmission, cancellation or rerun.

Job/result/source/terminal receipts match the frozen source. The local archive, CI script, changed-paths digest, and all 309 result-manifest file sizes/SHA-256 hashes were independently verified. 13 raw TRX files reconcile to the terminal receipt: 7,124 cases, 7,121 executed/passed, 3 skipped.

| Release suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Common.Tests | 394 | 0 | 0 |
| Coop.CrashReporter.Tests | 10 | 0 | 0 |
| Coop.IntegrationTests | 160 | 0 | 2 |
| Coop.Tests | 1374 | 0 | 0 |
| E2E.Tests | 2992 | 0 | 1 |
| GameInterface.Tests | 2191 | 0 | 0 |

| E2E shard | Executed / passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| 0 | 313 | 0 | 0 |
| 1 | 382 | 0 | 0 |
| 2 | 352 | 0 | 0 |
| 3 | 385 | 0 | 0 |
| 4 | 345 | 0 | 0 |
| 5 | 571 | 0 | 1 |
| 6 | 321 | 0 | 0 |
| 7 | 323 | 0 | 0 |

All eight execution commands exited 0. Each shard has an actual TRX, timing receipt, and raw log start/completion. The execution commands use Release assemblies; discovery commands are excluded. Four isolated slots ran in parallel. E2E: 2,993 cases, 2,992 executed/passed, 1 skipped. Test IDs are unique across shards. Release Lady: 49 cases, 49 passed, shard(s) 7.

Skipped tests (counted from actual NotExecuted results; the raw TRX notExecuted counters are zero):

- `E2E.Tests.Services.ItemRosters.ItemRosterSyncTests.Server_ItemRoster__count`: _count sync is disabled in ItemRosterSync.cs
- `Coop.IntegrationTests.Template.TestTemplate.ServerReceivesTemplateEventMessage_PublishesTemplateCommandMessage_AllClients`: Example Test
- `Coop.IntegrationTests.Template.TestTemplate.ClientReceivesTemplateEventMessage_PublishesTemplateCommandMessage_AllClients`: Example Test

Process-peer harness suite: PASS, 7 scenarios including expected negative controls, exact head/tree. Verification-plan receipt: `blocked-external-runtime`, `includesTestEvidence=false`; this is selection/handoff evidence, not rendered smoke or gameplay/live acceptance.

## Cleanup and scope

Both passing jobs confirm `gameReferenceDetached=true`, `e2eSlotsRemoved=true`, and no retained failure workspace. This task contains no live/bench run or owner disconnect/rejoin evidence; that checkpoint and PR work remain parent-owned.

Prior b57 reports, passes, and original d322 hardware-fault receipts are retained. Merge-only `docker-ci-02d64c686c784a7dbc1d` was cancelled while queued and is not counted as executed evidence. No source/shared-helper/PR edits, SSH, runner repair, Local builds/tests, additional jobs, cancellations, delegation, or supervisor messages occurred during this wait/collect continuation.

## Evidence

Relative to `C:/Users/Andrew/CodexWindows/coop-live-orchestration/3781/`:

- `ci-shared-journal-3d1140bbd-focused/job.json` and matching `-result/` raw evidence.
- `ci-shared-journal-3d1140bbd-full-release/job.json` and matching `-result/` raw evidence.
- `shared-journal-integration/ci-3d1140bbd-focused-receipt.json`.
- `shared-journal-integration/ci-3d1140bbd-full-release-receipt.json`.
- `shared-journal-integration/ci-3d1140bbd-full-release-accepted.json` and `ci-3d1140bbd-full-release-submission.txt`.
- `shared-journal-integration/ci-3d1140bbd-release-log-excerpts.txt` and `ci-3d1140bbd-final-queue-receipt.json`.
