# Final three ending captures, bd74

DONE: SuccessByAgreement, SuccessByBattle and FailureByRelease passed on `bd74a2d5323edd542faebc14b4b9802e6db81184`, tree `311491b1b93a9b28a6edf83fc2dab4fdb5981cd6`. All three were completed through their native branches in one campaign. Nine strict server/A/B checks passed. No R4 was requested or repeated. The retained up23 R4 evidence keeps its original source identity.

## Identity and scope

- Actual run: `issue3783-20261023T114021`. Native Windows/Cygwin, dedicated authoritative server engine PID 60368, wrapper PID 71700. Observer A/testclient PID 56212, `Hero_Player` / `MobileParty_Player`; owner B/testclient2 PID 50816, `Hero_Player2863` / `MobileParty_Player1238`. The server has no player party.
- Operator selection verified through `t3_thread_configuration`: cliproxyapi-codex, gpt-6.1-sol, high effort, default service tier. No children.
- Product worktree remained clean and pinned. Required `9e85bfe3c` ancestor verified. Compared with `6e268638332dc9c89111b04aea9640b67f6e8ec6`, only two test blank lines were removed.
- `dependency-18-product-blobs.json` compares all 18 production paths supplied by #4068's retained final-head gates against `78dc159175e19491a7825fcc8f6348ffcc24f32a`. Every tree entry matches, including `ArtisanJournalHandler.cs`, absent in both trees. No own-head #4068 live claim is made.
- No product edit, commit, push, development merge, .NET test suite, CI operation, PR write, draft-state change, publication, memory write or legacy workflow mutation. Parent owns publication and #3958 READY.
- Private test desktop only; no foreground/global input. Launcher finished with activations=0, restoreFailures=0, crashDialogs=0 and private desktop closed.

## Preparation, source and kit

The current external up23 kit needed one minimal chaining correction: after `coverageProbe.Dispose()`, set `coverageProbe = null`. This permits a new probe only after the previous probe's final counters were saved and its observers disposed. No running probe was reset. The file writer also removed the own source file UTF-8 BOM and changed the adjacent Dispose line ending; the composed compilation input still has the make_fixture BOM. These byte changes are retained in kit.delta.diff and in the exact compile hashes. Only `kits/IssueLiveTestKit.3783.cs` changed; shared helpers and the shared kit were unchanged. `own-kit.before.cs`, `own-kit.final.cs` and composed fixture bytes are retained.

Corrected own kit compiled through the unmodified `build_slot.ps1` before acquiring the bench: exitCode=0, errors=0, kitErrors=[], 11:36:16Z to 11:36:28Z. Own SHA256 `729b24091697fe187a63e143086c7b1ff1f9c591162df5b94c8b0abfa728c682`; shared `00f5828a89211d7f0a19877ea0bbbf6782e67e16d56c0144a311eb762bf1897d`; composed/staged `3f6aa4daac5de52bf44a15bb4f5858c1264076b8b5040aa0e51515decc819197`; manifest `9062046353303b8bd1fab707ac3f8a8d24f7a05f833096ec55e79fcb2a5f3019`.

Bench 3783 was explicitly granted and reserved. The operator acquired it, verified build slots empty, and ran `cycle.sh` directly with SHARED_KIT=1. Normal DebugAutoConnect build/deploy passed. Build identity `b355a0408a1493c198d97df52d3e5e3358ed87bd9c491560927217cf5793042b`, bundle `3d1ca557f5c92e817b88ed78400aee389913b514fdfdaa071adc9a243043b062`. All three runtime statuses report `0.1.7+bd74a2d5323edd542faebc14b4b9802e6db81184`. Loaded GameInterface SHA256 `9c7dcc74e5b2f743a3c19d5b23ab94ddd0cc395f7e355bb48c67ef346be1301e`, MVID `5d700a00-55d9-435d-be23-cdda79e095f8`, agrees on all three peers. Complete loaded paths, hashes and MVIDs are in `loaded-dll-hashes.json`.

Fresh basename `seed3783up24bd74` was checked absent and copied from immutable Remote saveauto1: SAV `78784b6e6b98757e3b761d8c65e702c1c771bb01dd043d3033cfdf385758fd84`, JSON `6bdc7df74f5a5eaa2da057ebb203d63c7b4baa8a0e88d573c9ca0dceae694b7c`. No autosave-mutated seed was reused. Config/source provenance precedes launch; final named-seed hashes are separate. Do not reuse this named seed for another fresh campaign.

One initial `setup_players - 4` and Pause established both clients in Khuzait with 60 healthy men and 1000 gold. Two seed offers were cleared on the server. Both clients then cleanly disconnected/reconnected once before accepting anything; all three endpoints subsequently reported offers=0 taken=0. No later kingdom shuffle, relation reset or CanTake bypass occurred. Three distinct eligible givers and distinct issues were accepted sequentially through real giver dialogue after the previous quest had completed and both journals were closed.

## Branch results and numeric evidence

`numeric-before-after.json` retains all server/A/B player values. The authoritative per-player Honor XP values and deltas are in each `*-before-state-server.json` / `*-after-state-server.json`. Client trait logs were checked independently; client levels do not substitute for server XP.

| Ending | Actual quest and party | Owner B before -> after | Non-owner A |
| --- | --- | --- | --- |
| SuccessByAgreement, DONE | Oragur, Tuvul's Revolt, issue_170; MobileParty_Created_1913, handle 42707 | Gold 1000 -> 6400, renown 0 -> 1, Honor XP 0 -> -10, giver relation 0 -> 8; Honor level 0 -> 0, one owner reputation log | Gold 1000, renown 0, XP 0, giver/rural relations unchanged; no quest/archive/log |
| SuccessByBattle, DONE | Tulag, Olorun's Revolt, issue_171; MobileParty_Created_1915, handle 42737 | Gold 6400 -> 11650, renown 1 -> 6.0472302, Honor XP -10 -> 20, giver relation 8 -> 17; Honor level 0 -> 0, one owner reputation log | Same gold/renown/XP, giver and rural relations unchanged; no matching quest/archive/log |
| FailureByRelease, DONE | Suran, Huskur's Revolt, issue_172; MobileParty_Created_1917, handle 42777 | Gold 11650 -> 11650, renown 6.0472302 -> 6.0472302, Honor XP 20 -> 20, giver relation 0 -> -5; no owner reputation log or quest reward | Same gold/renown/XP and relations; no matching quest/archive/log |

Expected branch behavior was refreshed from the exact installed vanilla type into `vanilla-LesserNobleRevoltIssueBehavior.cs`: agreement RewardGold, +1 quest renown, +8 base giver change and Honor -10; battle RewardGold, +3 quest renown, +8 base giver change, visited rural notables -2 and Honor +30; release fail, no gold/renown/Honor reward, giver -5 and visited rural notables +1. The kit computes the giver relation model rather than assuming displayed changes equal base changes. Battle's observed +9 giver change includes that model's factor/rounding.

Battle total renown is +5.0472302, including ordinary native battle rewards as well as the quest +3. The current strict kit asserts the branch minimum, not that the whole observed total came from the quest. Gold matched RewardGold exactly. This run does not separately count native renown method invocations.

Agreement used the existing native persuasion choice driver. Its first call attempted the listed persuasion choices and returned `PERSUASION_RESULT=unknown`; that was not accepted as a pass. A fresh conversation read showed the native successful NPC agreement, normal continuation closed the conversation, and the actual branch paid once. The driver's repeated same-frame option attempts are retained in `ra-persuasion-actual-choices-testclient2.json`; no persuasion success setter, branch call or generic completion was used.

Battle used a native authoritative village visit, setup of three opponents, the actual first-talk threat/fight choices, Send troops, native attacker victory, shared `sim_done` (EndBattleSimulation/OnFinished), and `loot_done` while InventoryState was actually open. No forced battle result, mission kill, generic completion, duplicate Done or invented inquiry was used. B ended free on the map with 59 healthy men.

Release used a native visit and the actual first-talk `Well, I have indeed been misinformed. You may go on your way...` option. Native failure text and QuestCompleteDetails.Fail were asserted. No failure setter or generic quest completion was used.

Nonempty rural evidence on all three peers:

- Agreement: independent real RuralNotable Mitunis of Tubilis, `Hero_CharacterObject_2275`, `castle_village_A1_1`, both player relations 0 -> 0. The full giver-clan relation probe also contains ten real notables per player, twenty rows, not an empty sample; no village was visited for agreement.
- Battle: real visited `village_K4_4` / Urunjan; Kazam `Hero_CharacterObject_2669`, B relation 0 -> -2 and A 0 -> 0 on server/A/B. Both actual village notables show -2 in the full relation receipts.
- Release: real visited `village_K2_2` / Tismil; Chaghan `Hero_CharacterObject_2642`, B relation 0 -> +1 and A 0 -> 0 on server/A/B. Both actual village notables show +1 in the full relation receipts.

## Exactly-once cleanup and journals

Each ending had a separately armed zero-baseline probe for its actual issue/giver/party id/handle. Its final counts were saved and observers disposed before the next probe was armed. For EACH of the three endings:

- Server logical send invocation counts: NetworkApplyDestroyParty=1, NetworkDestroyInstance=1, NetworkIssueFinalized=1.
- Each client received/published each of those messages once. Native MobilePartyDestroyed=1 on server/A/B.
- Local server DestroyPartyApplied=1 and InstanceDestroyed=1; corresponding client patch-generated local event counts=0, distinguished from their one received message and native callback.
- Same captured party inactive, absent from Campaign.Current.MobileParties, absent by registered id and absent by handle on all three peers. Final stopped counters equal first post-ending counters.
- Only battle sends NetworkOwnerBattleEnded: server=1, owner B receive=1, observer A receive=0. Agreement/release counts=0.

These are scoped logical transport calls, broker publications and native callbacks, not UDP datagram/retransmission counts. They do not count every reward method. Strict owner check receipts separately require endingCountOnOwner=1 and opening/ending/archive text once; observer checks reject a matching quest/archive.

Native journal sequence: B began with two seed old entries; its old counts after the three endings were 3, 4 and 5, active=0 and selected stages=2 each time. A remained active=0, old=2; shared journal_select returned `No native journal entry matches <actual title>` for every ending. All six final PNGs were opened and visually inspected at full resolution. Each owner PNG shows the full opening and ending, success/failure mark and actual reward where applicable. Before-context active journals are retained too.

All journal exits used `coop.debug.ui.pop_state` with explicit arguments `[]`, followed by fresh exact MapState/MapScreen status, actual conversation inactive read and encounter probes on both clients before proceeding.

## Retained deviations and limitations

- Initial preparation was stopped during rotation to add the one-line probe lifecycle correction. It had no runtime PIDs; cleanup verified absence. Original compile and identity receipts remain under `preparation-stopped-before-runtime`.
- Next server preparation refused a moved dedicated main reference: expected `9c72c762be7cb66c26d74bae45747584995e876e`, got `bb76bea12d5aacd11f5c2868b72440ebd9a58bc9`. It was cleaned with no runtime. Receipts remain under `preparation-server-ref-moved`. The successful normal cycle used the refreshed `bb76bea...` server identity; Coop remained bd74. No shared helper was changed to bypass the refusal.
- Retained old plans had a stale kit revision and doubled regex escapes; only the external run-local plan fields were aligned. Gameplay assertions were not relaxed.
- Initial closed-conversation `conv` inspection hit its pre-existing null-sentence bug. The retained failure was replaced by the existing read-only `coop.debug.hero_conversation.state` against registered Oragur, which reads actual IsConversationInProgress even when no hero is selected. No outcome was advanced on the failed read.
- An operator call used the wrong `coop.debug.game.pop_state` prefix. It was refused without effect; the correct existing `coop.debug.ui.pop_state []` then succeeded and fresh map/closed-conversation guards passed.
- Paged journal UiInspect can invalidate between pages. Stale responses were retained, and later captures used successful fresh first-page bridge snapshots plus full PNG inspection. No complete paged-text-tree claim is made. The initial agreement owner PNG is visibly complete despite a stale auxiliary text dump. Required final six capture times and hashes are retained in RESULT.json.
- Logs contain managed diagnostics, including the closed-conversation probe error. No clean-log or product-root-cause claim is made. No crash dialog was detected. Server mod log is empty on this branch; source-bound command receipts supply authoritative observations.

## Cleanup and handoff

`local_down.sh` completed 12:09:44.8920862Z. `cleanup-issue3783-20261023T114021/cleanup.json` has verifiedAbsent=true, remainingProcessIds=[], inventoryErrors=[], stopErrors=[]. `teardown.log` confirms BENCH_LOCK_RELEASED 3783. Fresh `final-inventory.json` at 12:10:08Z confirms no run/engine/client/cycle/relaunch process and no bench lock. No bootstrap/relaunch was started after cleanup. Engine visual profile was restored by the launcher.

Server config restored byte-for-byte. All five comparisons in `config-seed-final.json` passed: server config, shared local saveauto1 SAV/JSON and immutable Remote source SAV/JSON. Named seed retained separately. Product still clean at bd74; installed own/shared and staged kit final hashes match the compile receipts.

`audit.py` passed all 3 branches, 9 strict peer checks, 9 scoped counter sets, 9 real rural before/after samples, 6 inspected ending journals, cleanup/config/source and all 18 dependency-path comparisons. `RESULT.json` contains structured branch stats, counters, journal captions, capture timestamps, client PIDs and screenshot SHA256s. `EVIDENCE-MANIFEST.json` freezes the run-local files.

STATUS: DONE. Parent owns evidence publication and #3958 READY. No PR state was changed.
