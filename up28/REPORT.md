# #3781 / PR #3949: final live report

**STATUS: DONE-bench. Four gameplay flows PASS; server/B history PASS. A-local owner tags PARTIAL, for parent assessment of #4055's expected design.**

One native Windows/Cygwin Local runtime, dedicated server plus A=`testclient` and B=`testclient2`. No product/helper/kit edits, no tests or CI execution, no GitHub writes or delegation. QuestLearnings.md and the existing disconnect-cancels-active-quest policy remain unchanged.

## Identity

- Clean pushed head `b57cac1124a4f04a634f56d164819a1055473cba`; tree `59747186e122f966fb96295a925d97207509fff0`, rechecked unchanged after cleanup. Supervisor's waiver preserves this evidence; later conflict-only merges are parent-owned.
- Token `issue3781-20261023T171247`. Server engine/launcher PIDs `39068`/`43196`; A `65108`, B `61488`; client launch host/shell `52336`/`62428`.
- `SHARED_KIT=1`, revision `3781-shared-journal-r1`. Fixture manifest SHA-256 `3fbc476dcb9fc826b9522ca7533643cab319c4b827f940f73adfe3c502fb0027`.
- Loaded GameInterface MVID `5a92f6da-e76b-4eb2-bac2-b2a8e3909493`; Coop.Core `33c896b0-e283-48bd-9e90-909b7f646832`; client Coop `f5289df7-94df-45d5-a765-a9ecebbee04d`. Shared modules matched all endpoints. Full DLL hashes/MVIDs and dedicated-server identity: `runtime-identity.json`.

## Actual results

All values below are server readings. Canonical Honor came from `CoopSession.PlayerTraitData.PlayerTraitXp` using registered hero/trait IDs in the fixture, not client PlayerTraitDeveloper.

| Flow | Real path and identity | A result |
| --- | --- | --- |
| VillageCrime **PASS** | Real Sora dialogue, `issue_1215` / `issue_1215_quest`; live-owned Shapeshte `village_K3_1`; real hostile-action/force-goods menus | Honor **150→100**, relation **-9→-14**; no quest reward. Gold **10900→11008** came with village loot. |
| TournamentFailure **PASS** | Fresh Sora dialogue, `1216` / `issue_1216_quest`; Vostrum co-op arena Join/Start, AI-only Skip, A's own **Join** match, native `mission_lose`, production result | Exact failed-to-reach-round-2 ending; relation **9→4**; gold **11008**, renown **1187.4495**, influence **64**, Honor **100** unchanged. |
| REAL TournamentSuccess **PASS** | Fresh Sora dialogue, `1217` / `issue_1217_quest`; Danustica production co-op session `TournamentSessionIdentity_2`; A joined and won actual rounds 0–3 using live-agent `mission_win`; Completed revision **13**, bracket revision **6** on all endpoints | Quest gold **11008→11758 (+750)**, renown **1187.4495→1188.4495 (+1)**, relation **4→10 (+6 observed, base +5)**. Separate tournament +3 renown settled after Leave to **1191.4495**; tournament influence **64→65**. |
| PendingJoin **PASS** | Nus `Hero_CharacterObject_2704`, `issue_1218`, difficulty **0.1**; B disconnected; A accepted actual influence choice and held conversation; B transferred save and reached campaign-ready while pending | Owner A; server/A `tried=True`, B `tried=False`. After normal Leave/Continue and Danustica menu arrival: influence **100→82**, cost **18=15+ceil(30×0.1)**; renown **1191.4495→1198.4495 (+7)**; relation **0→6 (+6 observed, base +5)**. Gold **11758**, Honor **100** unchanged. |

B remained gold **10000**, renown **547.8492**, influence **1.5**, canonical Honor store **uninitialized**. Sora relation remained **-29**, Nus **-14**. No B rewards or personal Lady quest. Final server/A/B report `startedLadyQuests=0`. Every client action used freshly inspected actual choices; no A match was skipped, no synthetic result or direct quest ending was used.

## Raw owner-tag evidence: PARTIAL on A only

| Flow | Server archive: owner A, ended true | A-local archive: ended true, **legacy-unowned** | B transferred archive: owner A, ended false |
| --- | --- | --- | --- |
| Crime | `41032` | `41040` | `41032` |
| Failure | `41037` | `41046` | `41037` |
| Success | `41040` | `41050` | `41040` |
| Influence | `41045` | `41056` | `41045` |

Owner A means persisted `OwnerControllerId=testclient`. The kit labels an entry `legacy-unowned` when it is not `PlayerJournalLogEntry`; no missing field value is invented. Parent assesses this expected-design question. **Archive IDs are endpoint-specific**: A-local crime 41040 is not server success 41040.

Raw proof: `final-journal-server.json`, `final-journal-A.json`, `final-journal-B.json`; pre-completion transfer proof: `pending-joined-journal-B.json`. B loaded all four exact server IDs but none became ended/visible. Its selected native entry remained old unowned Sora failure **40508**, seven days old. Old unowned history is intentionally visible. A Old Quests rose **46→50**; B stayed **46** after raw history **75→79**. Exact IDs/text, not counts alone, establish isolation.

## Selected paired screenshots

All eight decisive journal images were opened and inspected. A selects the new exact ending; B selects old unowned 40508. Individual completion receipts and hashes are retained; captures are sequential.

- `VillageCrime-journal-A.png` / `VillageCrime-journal-B.png`.
- `TournamentFailure-journal-A.png` / `TournamentFailure-journal-B.png`.
- `TournamentSuccess-journal-A.png` / `TournamentSuccess-journal-B.png`.
- `PendingJoin-journal-A.png` / `PendingJoin-journal-B.png`.

`captions.json` records all **40 screenshots**, captions, capture IDs/times, hashes, dimensions, and visual-review status. `PendingJoin-transferred-history-{A,B}.png` shows A still conversing while B's loaded journal excludes A's new endings. `TournamentSuccess-result-A.png` shows the actual champion bracket, +750 message and separate tournament +3 renown.

## Corrections, additional request and cleanup

No cycle failed and no runtime was relaunched. Retained operator corrections include nonexistent `MapMenu`→observed `MapMenuView`, talk refusal during an existing/finishing encounter, effective-relation setup after Sora refused, and unsupported raw bridge Disconnect→existing connection commands. Server CommandCatalog used the documented `method_not_found` fallback. All raw unsuccessful responses are in `retained-setup-responses.json`; details are in `DETAILS.md`. Slow native startup emitted load diagnostics before the same server passed readiness; no crash or startup retry is claimed.

The later **active-owner disconnect/rejoin cancellation/no-resurrection check is UNRUN**: core cleanup had already released the bench, the next reservation was 3763, and supervisor explicitly prohibited reacquiring. This run's B rejoin had no active B-owned Lady quest, so it does not prove active-owner cancellation. No preservation change or Framework edit was attempted. Supervisor reports final-head HIVE focused **87/87**, fullRelease `480e7802` **7113 passed / 0 failed / 3 skipped, 8/8 shards**; CI was parent-owned.

Cleanup completed **2026-10-23T17:55:20.4029853Z**, **verifiedAbsent=true**, no remaining game processes, `BENCH_LOCK_RELEASED 3781`. Independent inventory confirmed all six owned game/launcher PIDs absent. Engine profile restored exactly; launcher exit 0, zero crash dialogs. Report generation happened after release.

- `cleanup-issue3781-20261023T171247/cleanup.json`
- `local-down.log`, `owned-pids-absent.json`, `bench-after-cleanup.txt`
- `crash-dialogs/engine-profile.json`, `crash-dialogs/launcher-crash-verdict.json`
- Machine handoff: `result.json`, `readings.json`, `archives.json`, `captions.json`, `runtime-identity.json`

STATUS: DONE-bench
