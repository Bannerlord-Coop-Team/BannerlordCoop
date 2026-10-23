# Classification of up29/retry1 raw archive41055

Disposition: shared accept/dismiss archive residual, not a Lady-specific disconnect bug. It is not visible in either native quest list and does not represent an active or completed quest. #3985 is historical context only: it was CLOSED by #4068 on2026-10-23 at15:25Z, and that fix is already in3d1140bbd. No claim is made that #3985 remains open or that its normal-ending replay fix failed.

## Observed Lady behavior

On final3d1140bbd, real owner disconnect cancelled Darya issue_1228 once. Server observed1QuestCancel, connected B received1NetworkIssueFinalized, offline A received0. Supervisor20:25Z accepted A0 as correct. A loaded a fresh server campaign on reconnect, had Active0/Old54, no attached issue or quest discussion and no changed rewards. B remained Active0/Old46 and unchanged. Raw server and rejoined A have owner=testclient archive41055, IsEndedfalse, zero logs. B has no such archive. Native lists and raw identity/state are recorded in up29/retry1.

## Cross-quest test

Isolated test-only commitb4e3ce28c19c49c818020dd67c98458214502aee is based on final3d1140bbd. Production sources are identical; only41test lines in ArtisanOverpricedGoodsTests differ. It registers actual JournalLogsCampaignBehavior and accepts the merged Artisan quest through the existing scenario. BEFORE disconnect, a related PlayerJournalLogEntry already exists with ownerA, IsEndedfalse and zero logs. ClearPeer plus actual PlayerDisconnected dispatch produces one QuestCancel and detaches the issue. AFTER disconnect the same issue still has one related owner-tagged, unended, empty archive state.

HIVE diagnosticdocker-ci-3a9386aa7e8c47cb8faf passed36/36, including that new case. Exact source/47artifact hashes verified. This is an in-process shared-state comparison, not a second live rejoin test and not proof of numerical Lady row41055 creation timing or row reference identity. It establishes that this raw shape is generic and can predate cancellation.

## Mechanism and limits

IssueBase.StartIssueWithQuest emits OnIssueUpdated while accepting. ServerQuestJournalPatches generically wraps server archive entries with ownership; it does not end them. ActiveIssueDismisser iterates every registered type and uses shared IssueDismisser.Dismiss, which calls IssueFinalized directly rather than running a quest ending branch. That shared silent-disconnect policy clears the issue and announces QuestCancel. There is no Lady-specific server archive handling on this path; Lady's local history workaround was removed after #4055.

The view distinction is explicit: active UI rows come from actual started quests, and old rows require IsEnded. A detached zero-log raw entry with IsEndedfalse appears in neither list. Row41055 therefore does not contradict the accepted ended-or-cleared policy. Raw-row storage cleanup, if later desired, belongs in the shared layer rather than a Lady-only guard. The diagnostic does not establish a general missing-ending-line defect or reopen #3985.

Product head remains unchanged3d1140bbd; the diagnostic is not cherry-picked, pushed to the issue branch, or substituted for final CI/live/reviews.