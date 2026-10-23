# up29/retry1: functional PASS, DONE-bench

The real owner-disconnect/rejoin flow passed on pinned source3d1140bbd/treee6. A accepted a fresh Lady quest, disconnected to the native main menu, and rejoined in the same process. The exact issue was cancelled once by the server and received once by B. It remained detached, A had no resurrected active quest or stale quest discussion, B stayed connected with no personal quest, and canonical rewards were unchanged.

**Packet-count difference for parent/supervisor:** A's actual counts after rejoin are observed0/received0. Server is observed1/received0, B observed0/received1. Offline A counter read returned `session_not_ready`. No claim that all three received one packet. No event replay, re-arm, workaround or extra ending was used. Functional PASS follows the explicit ruling to complete the flow and report A0 for adjudication.

## Identity and method

- Source `3d1140bbd01d588e18655734f96da083fbf86b43`, tree `e6c31dde1f707d2ff806abf32f449ef8cdf28f2d`, clean before/after. Local token `issue3781-20261023T193531`.
- Ignored kit revision3781-rejoin-r2, staged manifest SHA256 `579743f72c062b927cc8ba1eebba1141703f3165fd72abb27dff6245b4433353`. First compile by authorized cycle passed unchanged. Source/fixture/DLL SHA256 and loaded MVID receipts: runtime-identity.json, fixture-stage/manifest.json, identity.env, loading-discover.json, final-discover.json, cycle/build/cache logs.
- Operator Codexgpt-6.1-sol high, serviceTier=default. No extra agent/model call, HIVE SSH, CI, review, product/shared-helper edit or new commit. Up28 four flows were not repeated. Original up29 blocked result remains intact.
- Dedicated server40000, wrapper71636; A54164/testclient, B67600/testclient2. A process start2026-10-23T19:44:32.2225903Z remains identical before/after reconnect. B connectionInstanceId247ef0a4626d496aa2552ad132159366 remains identical.
- Current eligible giver selected by installed own kit: Darya, registeredHero_CharacterObject_2705/stringCharacterObject_2705. Requested Nus was not eligible. Darya staged at neutral Vostrumtown_ES2/security40; ownerclan_empire_south_1, leaderlord_1_14.

## Functional observations

1. Read-only finalize_watch armed on server/A/B before acceptance, exact issue1228 baseline observed0/received0 everywhere. Real inspected dialogue accepted `Then I will enter the tournament and reach round 2 my lady.` A's issue1228_quest was ongoing and started, owner=testclient, with one native acceptance log naming Darya. Server had its unstarted mirror. B had no attached or started Lady quest. Full native A/B journal pages captured without any row-selection prerequisite. A page visibly names Darya, Active1/Old54; B Active0/Old46.
2. Journals popped back before teardown. Installed disconnect.ps1 on A54164 returned `Client session is returning to the main menu.` Before reconnect, disconnected-discover.json proves AInitialState/campaignLoadedfalse/coopRunningfalse and server roster only B. Server party_state testclient returned connectedfalse/activefalse; testclient2 returned connectedtrue/activetrue. Native A main-menu PNG retained.
3. Exact issue1228 removed from Darya and currentQuestAttachedfalse, owner=- on server/B. Server observed `issue_1228|observed|CharacterObject_2705|QuestCancel` exactly once. B received `issue_1228|received|Hero_CharacterObject_2705|QuestCancel` exactly once. These are separate string/registered giver IDs, preserved as emitted. No synthetic cancel, timeout, direct ending or forged message.
4. Installed reconnect.ps1 on the SAME A54164 returned `Client co-op session restarted after teardown.` A reached MapState, Coop.Core.Client.States.CampaignState, campaignLoadedtrue and readyForCampaignTeststrue, actual loadedCampaignIdTL0aP1afDGZ1. No named save was requested, requestedSaveNamenull and loadedSaveNameConfirmedfalse; no named-save confirmation is claimed. Reloaded archive41055 and full actual campaign state were observed.
5. After rejoin all endpoints had Darya liveIssuenone, owner=-, currentQuestAttachedfalse. A startedLadyQuests0, B0. Final server/B counts remain1, A0. Server's ignored-kit retained historical quest reference still prints ongoing=True but is detached; it is not an active authoritative quest. Native Darya discussion offers only general discussion and leave, with no active quest option. The initial discussion probe found A in its loaded village encounter, then inspected Leave and successfully opened Darya dialogue; both responses retained.
6. Paired post-rejoin native journals show AActive0/Old54, BActive0/Old46. Selected detail is pre-existing archive34210 and is not captioned as a new ending. Raw Darya history was empty before; after rejoin server and A each have archive41055 owner=testclient, IsEndedfalse, logs0; B has no Darya archive. This is reported as active quest cleared, not a new completed archive. No optional row selection was attempted.

## Rewards and isolation

Canonical server baseline, accepted, disconnected, rejoined and final rows are identical. A gold14308, renown1191.4495, influence100, Honorlevel0, canonicalHonorXP90, Darya relation0. B gold10000, renown547.8492, influence1.5, Honorlevel0, canonicalHonorXPuninitialized, Darya relation-14. Client localHonorXP remains0 on A/B. Exact endpoint rows before/after compare equal, client/server renown representation differs only in printed precision. See rewards.json and raw state receipts.

B's connection instance remained unchanged throughout A disconnect/rejoin. Its personal quest count stayed0; journal history still has85 rows and no new Darya archive. Existing old unowned entries remain visible. Full archive ID/owner/IsEnded/log rows before/after are in archives.json and raw journal receipts.

## Cleanup and evidence

local_down.sh immediately followed final functional reads. cleanup-issue3781-20261023T193531/cleanup.json completed2026-10-23T20:12:46.5959685Z, verifiedAbsent=true, remainingProcessIds=[]. local-down.log ends BENCH_LOCK_RELEASED3781. Independent owned-pids-absent.json confirms40000/71636/54164/67600/49052/62440 absent. bench-after-cleanup.txt says free/no processes. Launcher exit0/crashDialogs0; engine profile restored exactOriginalRestoredtrue. Cleanup completed before this report was written.

Six PNGs with captions, SHA256 and completed capture handshake receipts are listed in captions.json. Paired acceptance: accepted-native-full-page-A/B.png. Paired rejoin: rejoined-native-full-page-A/B.png. Supplemental: owner-disconnected-mainmenu-A.png and rejoined-no-stale-discussion-A.png. Raw command/UI/connection/status/journal/counter reads remain in this folder. retained-tool-responses.json records the unavailable offline counter and loaded village encounter. No runtime setup/build failure on retry1. The own post-cleanup report writer had a corrected missing-brace parser error, retained in report-writer-failure.txt; no runtime was active. No reacquisition after cleanup.

STATUS: DONE-bench
FUNCTIONAL: PASS
A packet receipt: actual0, parent/supervisor adjudication pending if all-three1 is desired.