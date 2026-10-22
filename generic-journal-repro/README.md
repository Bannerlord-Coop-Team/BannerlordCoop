# Generic completed-journal save leak

Source: e5d2345f99e7d785c9fa3391a1b323c81ec9add5, with the attached test-only patch. No product changes.

`ArtisanOverpricedGoodsTests.CompletedQuestDoesNotLeakThroughTheLateJoinSaveHistory` registers the native journal behavior on server/A/B, accepts as player-A, delivers all five goods through the real branch, then completes successfully. The ownership assertion, exactly one QuestSuccess finalization, A's single ended archive, and B's empty pre-transfer history all pass. Copying the server's GameActionLogs into B then fails the no-ended-archive assertion at line 215 with `Collection: [Quest status change]`.

Release execution: 0 passed, 1 failed. This is the intended behavioral failure, not a build or fixture failure. The retained log also contains existing fixture AutoSync lookup warnings; the load-bearing assertions above reached the transfer check.

The native `JournalLogsCampaignBehavior.OnQuestCompleted` creates or updates a global ended JournalLogEntry without a solver check. Installed TaleWorlds.CampaignSystem.dll SHA-256: 1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE (v1.4.8).

No Lady-only guard was written. PR #3949 remains draft pending shared journal-bookkeeping correction. PR #4053 was inspected at 8461b2d39008e6389f2ec86944fb5fcfee69b081: its IssuesCampaignBehavior.OnIssueUpdated authority change does not guard JournalLogsCampaignBehavior.OnQuestCompleted.

The supervisor's 07:02Z independent visual review accepted up27 retry1's crime, TournamentFailure and owner pending-join screenshots, but did not accept B pending-join. The later +2 diagnosis and this cross-quest reproduction supersede the original overall PASS claim. No final live acceptance is claimed.
