# #3781 / PR #3949, up28 live operator report

STATUS: DONE-bench. All four gameplay flows PASS; server/B transferred-history filtering PASS. **PARTIAL: A's local completed archives have no persisted OwnerControllerId**, reported by the kit as `legacy-unowned`. Parent assesses whether that is the expected #4055 design. No product or shared-helper edits, no kit correction, no GitHub writes, no tests/CI execution, no delegation.

## Exact identity

- Clean pushed source: `b57cac1124a4f04a634f56d164819a1055473cba`, tree `59747186e122f966fb96295a925d97207509fff0`. Rechecked unchanged and clean after cleanup. Supervisor's explicit waiver preserves this evidence; later conflict-only merges are parent-owned.
- Native Windows/Cygwin Local cycle, dedicated server plus A=`testclient` and B=`testclient2`. Token `issue3781-20261023T171247`. One runtime attempt, no failed cycle or runtime relaunch.
- Server engine PID `39068`, server launcher `43196`, A `65108`, B `61488`; client launch host `52336`, client shell `62428`.
- Composed fixture `3781-shared-journal-r1`, `SHARED_KIT=1`. Manifest SHA-256 `3fbc476dcb9fc826b9522ca7533643cab319c4b827f940f73adfe3c502fb0027`; fixture source SHA-256 `662e7f2a72924b72a74b036d8b15c09f0337b122b69b6d30f66e0614db248863`. `fixture-stage/manifest.json` and `fixture.env` retained.
- Loaded `GameInterface` MVID `5a92f6da-e76b-4eb2-bac2-b2a8e3909493`, `Coop.Core` `33c896b0-e283-48bd-9e90-909b7f646832`, client `Coop` `f5289df7-94df-45d5-a765-a9ecebbee04d`. Shared modules match server/A/B. All six module MVIDs and file hashes are in `runtime-identity.json`; live status and build/readiness receipts retain provenance.
- Dedicated source `bb76bea12d5aacd11f5c2868b72440ebd9a58bc9`, tree `68d932b347bdc3e10ff20fbd92ca92f21dca03ae`.

## Flow results

All gold/renown/influence and Honor values below are authoritative server readings. Client raw readings are retained separately; their float display precision differs slightly. Honor comes from `CoopSession.PlayerTraitData.PlayerTraitXp` using the registered hero and `Id(DefaultTraits.Honor)` in the bound fixture, not the obsolete client PlayerTraitDeveloper. The getter does not separately print the registered trait ID.

**VillageCrime: PASS gameplay and server/B history.** Sora `Hero_lord_1_32`, offer `issue_1215`, quest `issue_1215_quest`. The live ownership query confirmed Shapeshte `village_K3_1` belonged to Sora's `clan_empire_north_2`. Sora was placed in neutral Vostrum `town_ES2`, security 40. War already existed before real acceptance; the redundant declare-war command refused, and no new war was needed. A accepted through dialogue, then clicked the real village **Take a hostile action → Force peasants to give you goods** menus. No direct ending or village-force substitute was used.

A Honor XP **150 → 100**, relation **-9 → -14**, gold **10900 → 11008**, renown **1187.4495 unchanged**, influence **64 unchanged**. The +108 gold accompanied the native village loot screen, not a Lady reward. The exact crime ending was selected in A's native journal. Raw receipts: `crime-before-{server,A,B}.json`, `crime-settled-{server,A}.json`, `crime-journal-{server,A,B}.json`, plus the actual UI action receipts.

**TournamentFailure: PASS gameplay and server/B history.** Fresh real Sora offer `issue_1216`, quest `issue_1216_quest`. Restored ordinary acceptance eligibility using server `set_effective_relation`; its effective NPC was Manteos. A used Vostrum's real arena **Join the tournament → Start tournament** route. Three AI-only matches offered Watch/Skip and used production Skip. A's actual match offered Join; A clicked Join, spawned in Battle mode, and `mission_lose` defeated the live player agent. The production controller settled the result after the remaining combat finished. The initial ongoing readings are retained and are superseded only by the settled failure readings.

A relation **9 → 4**; gold **11008**, renown **1187.4495**, influence **64**, Honor XP **100**, all unchanged. A's selected journal states that A failed to reach round 2. Raw settled receipts: `failure-settlement-read-2-{server,A}.json`; before/after UI and archive receipts are retained. The Danustica-only observer could not identify the Vostrum session, so no exact session ID is claimed for this flow; its output does identify the active production co-op tournament behaviors.

**REAL TournamentSuccess: PASS gameplay and server/B history.** Fresh real Sora offer `issue_1217`, quest `issue_1217_quest`, at Danustica `town_ES1`. A chose the tournament quest despite the influence alternative being available. A used the real co-op Join/Start route. `TournamentSessionIdentity_2` recorded A's played matches `round:0:match:0`, `round:1:match:0`, `round:2:match:0`, and `round:3:match:0`. Each offered Join, spawned through the production controller, and used `mission_win` only against the live opposing agents. One AI-only match used production Skip. No player match was skipped, no native tournament-join bypass or synthetic result was used.

Server/A/B agreed on **Completed, revision 13, bracketRevision 6**. Before leaving the victory screen, A gold **11008 → 11758 (+750)** and renown **1187.4495 → 1188.4495 (+1 quest reward)**. Relation **4 → 10**, observed +6 against expected base +5, so the observed model adjustment is +1. Influence **64 → 65** accompanied the tournament result. After the normal Leave, renown became **1191.4495**, separately proving the tournament's additional **+3**. Honor stayed 100. The native victory screen and selected success journal were inspected. Receipts: `success-quest-result-{server,A,B}.json`, `success-after-tournament-award-server.json`, `success-completed-session-{server,A,B}.json`, plus every played-match and UI receipt.

**PendingJoin: PASS gameplay and server/B history.** B disconnected cleanly. Live Nus `Hero_CharacterObject_2704` was eligible in Danustica, owned by A's clan. Server setup set influence 100 before baselines. A accepted the real influence option for `issue_1218`, difficulty 0.1, cost **15 + ceil(30 × 0.1) = 18**, and held the conversation open.

B restarted its co-op session, transferred the save, and reached campaign-ready while issue `1218` remained `SolvingWithLordSolution`, owner `testclient`. Server/A retained `tried=True`; B had `tried=False`. A still had the actual conversation open, and influence remained 100. The three completed A-owned archives and the pending Nus archive transferred with their exact server IDs, owner A, and `IsEnded=False` on B.

A then clicked **I must leave now**, continued the farewell normally, and entered Danustica through the existing authoritative movement setup to drive the ordinary menu callback. Closure alone still showed pending before that arrival; no claim attributes completion to closure alone. Server completed the issue with the exact Nus ending. A influence **100 → 82**, renown **1191.4495 → 1198.4495 (+7)**, relation **0 → 6**, observed +6 against base +5. Gold **11758** and Honor **100** stayed unchanged. Server archive `41045` ended with two logs; B retained the same archive un-ended with only the acceptance log. Receipts: `pending-before-*`, `pending-accepted-*`, `pending-joined-*`, `pending-rejoining-status-B.json`, `pending-after-*`, and `final-journal-*`.

**B isolation across all four flows:** gold **10000**, renown **547.8492**, influence **1.5**, canonical Honor store **uninitialized**, all unchanged. Sora relation stayed **-29**; Nus relation stayed **-14**. B never had a personal started Lady quest. Final server/A/B all report `startedLadyQuests=0`.

## Exact archive ownership and the PARTIAL observation

Archive IDs are endpoint-specific before transfer. Do not conflate A's local crime `41040` with server success `41040`.

| Flow | Issue / quest | Server archive | A local archive | B after transfer |
| --- | --- | --- | --- | --- |
| VillageCrime | `1215` / `issue_1215_quest` | `41032`, owner A, ended true, 1 log | `41040`, **legacy-unowned**, ended true, 2 logs | `41032`, owner A, ended false, 1 log |
| TournamentFailure | `1216` / `issue_1216_quest` | `41037`, owner A, ended true, 1 log | `41046`, **legacy-unowned**, ended true, 2 logs | `41037`, owner A, ended false, 1 log |
| TournamentSuccess | `1217` / `issue_1217_quest` | `41040`, owner A, ended true, 1 log | `41050`, **legacy-unowned**, ended true, 2 logs | `41040`, owner A, ended false, 1 log |
| PendingJoin | `1218`, issue-only | `41045`, owner A, ended true, 2 logs | `41056`, **legacy-unowned**, ended true, 2 logs | `41045`, owner A, ended false, 1 log |

Here A means persisted `OwnerControllerId=testclient`. `legacy-unowned` is the kit's label when an entry is not `PlayerJournalLogEntry`; it does not prove an OwnerControllerId field was present and empty. This owner-client observation is retained without a diagnosis or workaround. The strict all-endpoint owner-tag assertion is **PARTIAL**, while the server's persisted tags and B's filtering are **PASS**.

`final-journal-server.json`, `final-journal-A.json`, and `final-journal-B.json` contain the raw rows and exact ending text. `archives.json` parses all baseline/intermediate/final archive readings without replacing those raw receipts. The save contains older deliberately visible unowned entries, notably Sora failure archive **40508**, dated seven days ago. B selected that old entry throughout; it did not display this run's four completed A entries. A Old Quests count rose 46→50, B stayed 46 even after its total raw history rose 75→79. Counts are supplementary to exact identities and text.

## Selected paired screenshots

All eight decisive native-journal images below were opened and visually inspected. Their selected text matches the raw local archive and giver. These are sequential captures with individual completion times, not simultaneous frames.

- VillageCrime: `VillageCrime-journal-A.png`, `VillageCrime-journal-B.png`.
- TournamentFailure: `TournamentFailure-journal-A.png`, `TournamentFailure-journal-B.png`.
- TournamentSuccess: `TournamentSuccess-journal-A.png`, `TournamentSuccess-journal-B.png`.
- PendingJoin: `PendingJoin-journal-A.png`, `PendingJoin-journal-B.png`.

`captions.json` inventories **40 screenshots**, hashes, dimensions, capture IDs, completion times, raw capture/image receipts, captions, and which supplementary images were visually reviewed. `PendingJoin-transferred-history-{A,B}.png` shows A still conversing while B's loaded native journal selects only the old unowned failure. `TournamentSuccess-result-A.png` shows the actual champion bracket, +750 message, and separate +3 tournament renown. `first-renderable-{A,B}.png` records initial save-transfer screens. Baseline native journals and every flow's raw archive baseline are retained; later flow baselines reuse the preceding settled archive checkpoint.

## Retained setup corrections and limits

- No cycle/build/launch attempt failed. The native startup watchdog emitted `Cannot load` lines during slow startup; the same server subsequently passed readiness and loaded the matching modules. Those early logs are retained. The premature blocker commentary was corrected before any cleanup or restart.
- Server `CommandCatalog` returned `method_not_found`; client catalog and individually successful server commands supplied the documented fallback.
- An operator request used nonexistent layer `MapMenu`; reinspection identified `MapMenuView`. The exception's layer receipt and original request are retained. No stale reference was clicked blindly.
- Talk attempts while a settlement encounter still existed refused. Normal Leave, then a fresh talk, succeeded. One immediate post-Leave talk preceded the asynchronous exit and was retried after it completed.
- Sora's real dialogue refused at effective relation -14. A base `set_relation` alone did not restore that effective relation; server `set_effective_relation` did, and both server/client getters reported the effective NPC Manteos. New reward baselines were captured afterward.
- Raw bridge `Disconnect` returned `method_not_found`; the existing `coop.debug.connection.disconnect` / `.reconnect` commands succeeded. All unsuccessful responses remain in `retained-setup-responses.json`; they were not converted into passes.
- Native registry Equipment diagnostics during tournament UI construction are retained in `final-runtime-A.log`; they did not prevent the exercised flows. No broader diagnosis or clean-log claim is made.
- The additional active-owner disconnect/rejoin cancellation check requested after core cleanup is **UNRUN**. At that point the bench was reserved next for 3763, and supervisor then explicitly prohibited reacquiring. No active-owner quest was disconnected in this run, so cancellation/no-resurrection of an active owner quest is not established. The existing policy and shared Framework remain unchanged.
- QuestLearnings.md remains unchanged. Tech-debt review, CI and any conflict-only merge remain parent-owned. Supervisor reported final-head HIVE focused 87/87 and fullRelease `480e7802` 7113 passed / 0 failed / 3 skipped, 8/8 shards; this operator did not run or independently re-audit CI.

## Cleanup and handoff

Token-bound `local_down.sh` completed **2026-10-23T17:55:20.4029853Z**, `verifiedAbsent=true`, `remainingProcessIds=[]`, and printed `BENCH_LOCK_RELEASED 3781`. A subsequent inventory confirmed all six owned game/launcher PIDs absent. The visual engine profile was restored exactly; launcher crash verdict has exit 0, zero crash dialogs, zero close failures.

- Cleanup: `cleanup-issue3781-20261023T171247/cleanup.json`.
- Shutdown output: `local-down.log`.
- Independent PID absence: `owned-pids-absent.json`.
- Immediate bench-free receipt: `bench-after-cleanup.txt`.
- Engine restoration: `crash-dialogs/engine-profile.json`.
- Launcher verdict: `crash-dialogs/launcher-crash-verdict.json`.
- Full machine handoff: `result.json`, `result-manifest.json`, `readings.json`, `archives.json`, `captions.json`, `runtime-identity.json`.

The bench was released before report generation. No more live/runtime operations are authorized or performed. No supervisor or other thread was messaged. All evidence is under this `up28` directory; earlier runs were recipe context only.

STATUS: DONE-bench
