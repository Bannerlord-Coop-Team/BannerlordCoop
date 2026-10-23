# Generic disconnect archive diagnostic

**PASS: ArtisanOverpricedGoodsTests, 36 executed, 36 passed, 0 failed, 0 skipped.** The added `DiagnosticOwnerDisconnectLeavesTheSharedHiddenUnendedArchive` actually executed and passed.

The tested owner-tagged, unended, empty server archive state also occurs in another merged quest and already exists before disconnect. The test establishes that narrow shared-state observation. It does not establish a real reconnect/UI defect or prove the Lady live row has the same cause.

## Frozen diagnostic source and job

- Job: `docker-ci-3a9386aa7e8c47cb8faf`, HIVE, `windows-native-v1`, focused Debug.
- Head: `b4e3ce28c19c49c818020dd67c98458214502aee`
- Tree: `479acc11582a508bd483c451b4add1457bef6f6c`
- Source archive SHA-256: `1557a89e094f07e2825e28c52e3b62c7e5d33a6e5dae8766aa592ba43281aeaf`
- Production base: `3d1140bbd01d588e18655734f96da083fbf86b43`
- Read-only git comparison confirms only `source/E2E.Tests/Services/Issues/ArtisanOverpricedGoodsTests.cs` differs from final 3d, with 41 added test lines. No production-source differences.
- Execution: 2026-10-23T20:25:46.303852+00:00 to 2026-10-23T20:28:03.870128+00:00.

## Actual diagnostic assertions

1. Register actual `JournalLogsCampaignBehavior` on each harness instance and use existing Artisan `Accept()`.
2. Before disconnect, assert exactly one server `PlayerJournalLogEntry` related to the accepted issue, owned by `QuestScenario.ControllerA`, `IsEnded() == false`, and zero entries.
3. Call `ClearPeer(ClientA.NetPeer)`, then publish `PlayerDisconnected`.
4. Assert exactly one `NetworkIssueFinalized` for that issue with `IssueOutcome.QuestCancel`.
5. After disconnect, assert the notable has no issue and exactly one related server `PlayerJournalLogEntry` remains owner-tagged, unended, with zero entries.

The before and after queries use the same accepted issue. The test does not assert reference identity of the row, actual hidden visibility, numeric row ID 41055, client UI state, serialization/load, or real reconnect. No claim is made that closed #3985 remains open or that #4068 failed.

## Execution evidence

- TRX: `ci-generic-disconnect-hidden-archive-diagnostic-result/artifacts/debug-test-supplement/0.trx`.
- TRX SHA-256: `b8ed574f7304fe30dd3ea23fa3b26d2fcebd5048204c86e36baed8b200792a37`
- New diagnostic outcome: `Passed`, duration `00:00:01.2823907`.
- Execution ID: `e74c053c-e57b-46bf-889a-cf1c0e0a2a95`; test ID: `4fe02836-f4f8-d292-05ad-48e1ef948e77`.
- All 36 raw `UnitTestResult` entries are Passed, matching counters, supplement class execution counts, terminal totals and raw log summary.
- Exact job/result/source/terminal/supplement identities match. Archive, script/helper hashes, requirement digest, TRX hash, and all 47 result-manifest file sizes/SHA-256 hashes independently verified.
- Debug supplement accepted. This focused diagnostic is not full Release/Stage 5 evidence.

## Cleanup and boundaries

Cleanup confirms `gameReferenceDetached=true`, `e2eSlotsRemoved=true`, and no retained failure workspace. No submission, resubmission, cancellation, source/helper/PR change, full Release/final-CI rerun, SSH, Local build/test, live/bench action, extra agent, or supervisor message was performed. Final 3d 89/7121 passes, reviews, b57 passes and prior hardware-fault receipts remain preserved.

## Saved receipts

Paths relative to `C:/Users/Andrew/CodexWindows/coop-live-orchestration/3781/`:

- `ci-generic-disconnect-hidden-archive-diagnostic/job.json`.
- `ci-generic-disconnect-hidden-archive-diagnostic-result/` raw result, source receipt, logs, supplement, TRX and manifest.
- `shared-journal-integration/diagnostic-generic-disconnect-history-receipt.json`.
- `shared-journal-integration/diagnostic-generic-disconnect-history-log-excerpts.txt`.
- `shared-journal-integration/diagnostic-generic-disconnect-history-final-queue-receipt.json`.
