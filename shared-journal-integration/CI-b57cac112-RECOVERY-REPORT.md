# CI recovery and full Release for #3949, frozen b57cac112

Focused recovery **PASS: 87 passed, 0 failed, 0 skipped**. The single authorized full Release **PASS: 7,113 passed, 0 failed, 3 skipped**. All eight E2E shards executed.

- Head: `b57cac1124a4f04a634f56d164819a1055473cba`
- Tree: `59747186e122f966fb96295a925d97207509fff0`
- Source archive SHA-256: `360a256ca91549e3ec38f698c1c51aa0fa2d4e3b577d8917eeb5ec01753e1ffe`

## Focused recovery

Job `docker-ci-72abd860f6aa49eea3f8`, HIVE, Debug. Ran 2026-10-23T17:06:36.423413+00:00 to 2026-10-23T17:09:44.322948+00:00. Its archive, script, helper and selection match the original focused package. Source identity, 111 artifact hashes, requirement hash, three TRX hashes and actual per-class results verified; Debug supplement accepted. This focused profile alone is not Stage 5 evidence.

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Coop.Tests, containers | 4 | 0 | 0 |
| E2E.Tests, selected classes | 68 | 0 | 0 |
| GameInterface.Tests, selected classes | 15 | 0 | 0 |

E2E selection: social command 4, conversation Debug command 12, Lady 49, journal history 3. GameInterface selection: patch constructors 7, issue layout 3, journal history 5.

## Single full Release

Job `docker-ci-480e780278484dd2ba8d`, HIVE, `windows-native-v1`, Release. Ran 2026-10-23T17:25:10.459696+00:00 to 2026-10-23T17:41:07.268155+00:00. Exactly one full submission; no rerun. Source identity and all 308 manifest-listed file sizes/SHA-256 hashes verified. Thirteen raw TRX files reconcile exactly to the terminal totals.

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Common.Tests | 394 | 0 | 0 |
| Coop.CrashReporter.Tests | 10 | 0 | 0 |
| Coop.IntegrationTests | 160 | 0 | 2 |
| Coop.Tests | 1373 | 0 | 0 |
| E2E.Tests | 2990 | 0 | 1 |
| GameInterface.Tests | 2186 | 0 | 0 |

| E2E shard | Executed / passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| 0 | 337 | 0 | 0 |
| 1 | 341 | 0 | 0 |
| 2 | 392 | 0 | 0 |
| 3 | 363 | 0 | 0 |
| 4 | 324 | 0 | 0 |
| 5 | 571 | 0 | 1 |
| 6 | 330 | 0 | 0 |
| 7 | 332 | 0 | 0 |

All eight shard commands exited 0 and each has an actual TRX, timing receipt, and raw log start/completion. Four isolated slots ran in parallel. E2E total: 2,991 cases, 2,990 executed/passed, one skipped. Test IDs are unique across shards. Release Lady results: 49 cases, 49 passed, shard(s) 7.

Skipped tests (derived from actual NotExecuted results; raw TRX notExecuted counters are zero):

- `E2E.Tests.Services.ItemRosters.ItemRosterSyncTests.Server_ItemRoster__count`: _count sync is disabled in ItemRosterSync.cs
- `Coop.IntegrationTests.Template.TestTemplate.ServerReceivesTemplateEventMessage_PublishesTemplateCommandMessage_AllClients`: Example Test
- `Coop.IntegrationTests.Template.TestTemplate.ClientReceivesTemplateEventMessage_PublishesTemplateCommandMessage_AllClients`: Example Test

Process-peer harness suite: PASS, 7 scenarios including expected negative controls. Its source matches the frozen head/tree. The verification-plan receipt reports `blocked-external-runtime`, `includesTestEvidence=false`; rendered smoke and full live remain UNRUN. CI success does not establish gameplay/live acceptance.

Both successful jobs confirm `gameReferenceDetached=true`, `e2eSlotsRemoved=true`, and no retained failure workspace.

## Preserved prior evidence and boundaries

Original `docker-ci-d322eb5e41164192b06f` failure, its report and receipts remain unchanged by this task. The supervisor ruling in `d322-supervisor-runner-fault-ruling.json` classifies it RUNNER FAULT, no budget charged and no source change. Those event-log findings are supervisor-provided. Old full `docker-ci-94ab42a5e0a9406d9adb` remains cancelled and was not reused.

No SSH, runner repair, further focused submission, product/shared-helper/PR edits, branch changes, Local builds, live runs/leases, delegation, or supervisor messages. Parent owns product, PR and live verification.

## Evidence paths

All paths below are relative to `C:/Users/Andrew/CodexWindows/coop-live-orchestration/3781/`.

- `ci-shared-journal-b57cac112-focused-runner-recovery/job.json` and matching `-result/` raw logs/TRX/source receipt/manifest.
- `ci-shared-journal-b57cac112-full-release/job.json` and matching `-result/` raw logs/TRX/source receipt/manifest.
- `shared-journal-integration/ci-b57cac112-recovery-focused-receipt.json`.
- `shared-journal-integration/ci-b57cac112-recovery-full-release-receipt.json`.
- `shared-journal-integration/ci-b57cac112-full-release-accepted.json` and `ci-b57cac112-full-release-submission.txt`.
