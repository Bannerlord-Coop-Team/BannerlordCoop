# Factory-authority diagnostic, not two-playable-client acceptance

This explicit DEBUG mode is the next bounded experiment after the parent-owned successful single-client run `2719563f90144c07bff5131fb1a9dd77`. That run retained the factory-active anchored body, completed native deployment callbacks once each, and rendered ship/crew/native HUD. It did not accept intentional steering, orders, release/retake or contact continuity. `SingleClientNative`, `Activation` and `HeldHelm` retain their prior paths.

`FactoryAuthorityProbe` tests a different lifecycle, not another attempted inverse of `DisableDynamicBodySimulation`. It installs no new captain/UI/order integration, boarding, hull interactions, campaign fleet, recovery, admission or withdrawal policy. All ten actors remain synthetic infantry. `nativePass=false`, `deployApproved=false`; independent review and parent approval are required before deployment or launch.

## Lifecycle and limits

1. Both clients open empty naval missions. Their actual `AfterStart` sends existing `BattleMissionReady`; the existing server election selects the first scene-ready client and records the other as successor. No factory runs before epoch one and both original owners appear in that assignment. The dedicated campaign server opens no native mission.
2. On a later mission callback each client materializes its two hulls under that captured assignment. The elected host keeps factory-active bodies, anchored. **Anchoring is not a physics pause.** The follower disables each body only at successful `MissionShip.InitForMission` return, after checking valid entity, dynamic body, complete actuators/physics/formation/order, fixture origin and owning mission. No enable call or partial-body cleanup is added.
3. Hydration is sent only after both entire `SpawnShip` calls return, native assignments name those same hulls, all ten actors exist and all registry identities are installed. Scene-ready observations are separate from immutable operation receipts. Both hydrated receipts and unchanged original epoch-one membership precede release. The host then unanchors and publishes frames. Follower frame application explicitly retains the existing `SetGlobalFrame(isTeleportation:true)` and attached-navmesh update, **an experimental contact path**, not a supported follower-mode claim.
4. Before release, no frame publication or scripted control is accepted. Frames received before local hydration/release are rejected even if their queued application runs afterward. Epoch/owner changes, incomplete materialization, callback exceptions, refused native frame application or native blockers terminally hold the probe. No agent authority is adopted. A thirty-second local startup deadline and a 120-second local post-release lifetime bound the experiment. A stalled callback cannot execute its timeout/cleanup.
5. Stop/hold/leave/disposal clears managed release and terminally blocks frame publication/application before control cancellation. Complete-body hold is attempted even if cancellation throws; cleanup failures remain logged and in `factoryProbe.factoryControlCleanupFailure` / `factoryProbe.factoryHoldFailure`. An emergency receipt of `failed:factory_probe.terminal_cleanup` is failure, not a successful native hold. Stop still attempts mission end, and leave/disposal continues managed teardown. Hold targets only tracked complete hulls, never re-enables them, and retains isolation patches until process exit. Ship removal's existing pre-end hook holds before native removal; later disposal cannot reuse retired pointers. A partially initialized factory failure remains a failed disposable process, not a repaired scene. Original initialization exceptions are returned unchanged.

Existing operation budget, one-second helm/actor pulse deadlines, thirty-second probe window, eight-window cap, 64 retained samples and eight-record pages remain. The 120-second lifetime is an additional cap, not permission for eight windows. No fixture restart in the same process.

## What is actually instrumented

`native.factoryAuthorityProbe` includes role, attempted/materialized/released/terminal flags, bounded 16-row lifecycle trace, pre-completion-or-unattributed fixed entries, parallel fixed entries and active parallel fixed entries. Existing `fixedTicks`, `activeFixedTicks`, `forceApplications` are retained. Trace rows distinguish completed initialization from whole factory/assignment return; pointer identity is process-local only.

During this explicit probe's materialization-through-terminal lifetime, `NavalPhysics.OnFixedTick` and `OnParallelFixedTick` prefixes count entries. Complete known fixture physics may be queried for active simulation; unknown or not-yet-complete physics counts separately, with **no native body getter on a partial hull**. Any such follower entry conservatively fails startup. Any observed active follower fixed/parallel entry or `NavalPhysics.ApplyForceToDynamicBody` entry fails the probe. Prefixes only observe and set a blocker; no native tick or force call is suppressed. Failure-driven hold executes on the game thread.

The broader counters conservatively include any `NavalPhysics` callback in this otherwise empty diagnostic mission, rather than silently losing the factory interval because returned hull arrays are not populated yet. They are not measurements of force magnitude. No assertion is made that other native force/torque APIs or the solver itself were instrumented.

**Unobserved interval:** native prefab/body creation and native integration before managed `NavalPhysics` callback coverage. The factory can expose an active body before `InitForMission`. Zero observed counters do not prove zero interleaved native integration, safe collision support, atomicity or a parallel barrier. Pre-completion callback counts are not relabeled active integration counts. An original initialization exception does not trigger complete-hook disable.

Paired samples retain incarnation, epoch, source sequence/callback, probe operation, transmitted target, received/apply/next-observation callbacks and actual native observations/registry authority. Superseded, failed and unavailable samples remain explicit. Read-only support-root/local-position observations are not corrected or attached to the hull by the probe. Existing world-space puppet interpolation is unchanged; support-local agent replication is still unimplemented. Two-Hz samples cannot certify between-sample contact continuity or same-tick error budgets.

## Parent-only bounded protocol

Use a fresh isolated server plus two clients only after aggregate memory preflight, independent review and explicit parent approval. Preserve/restore canonical modules, profiles, saves and the unchanged scoped opt-in. Verify all six managed DLL hashes and loaded MVIDs, active DLC/capabilities, distinct connected controllers and dismissed current Call of the Oceans modal using the existing exact-widget protocol. Never deploy game/publicized dependencies. Inspect the live command catalog first.

The worked IDs `testclient1` and `testclient2` must both appear in server `coop.debug.players.list`. UUIDs below are single-use worked examples. All mutations are server commands; client commands are read-only. Do not issue keyboard ship/helm/order actions in this experiment.

```text
coop.debug.naval_lab.create d638a8d7-a859-48a6-8526-8420250d251d testclient1 testclient2 factory-authority-probe
coop.debug.naval_lab.receipt d638a8d7-a859-48a6-8526-8420250d251d
coop.debug.naval_lab.inspect
```

Require both `sceneReady` owners then both terminal `hydrated` receipts, elected epoch one, no failure, two complete finite hulls/ten actor rows on each client, correct immutable registry owners and no campaign/damage/save blocker. Record both actual roles, not an assumed client-one host. On the follower require zero `activeFixedTicks`, `activeParallelFixedEntries`, `forceApplications`, and `preCompletionOrUnattributedFixedEntries` from probe start; ordinary inactive fixed callbacks should continue. Inspect both lifecycle traces and export before proceeding. Any missing/error row, observed follower activation/force, partial initialization or deadline ends the test immediately. Startup success only permits this diagnostic; it does not close the unobserved-interval caveat.

After these checks, within the 120-second lifetime, issue one probe for the **nonhost owner's hull**. If elected host is testclient1, its remote hull is slot 1, so use:

```text
coop.debug.naval_lab.action 698bbd61-07f0-4d2b-90a2-f72df27552e4 probe 1 0.2 true
coop.debug.naval_lab.receipt 698bbd61-07f0-4d2b-90a2-f72df27552e4
```

If elected host is testclient2, the same operation uses slot 0 instead; slot identity always comes from this run's manifest. This is scripted host helm input, **not remote native captain/UI input**. A receipt proves dispatch, not propulsion. Do not proceed if host actual movement and active/force deltas are absent. No fake movement, extra physics flags or wake experiment may rescue that result.

During the thirty-second window export `coop.debug.naval_lab.samples 0` on each client, then page using its greatest returned sequence. Pair source identity, not wall-clock inspection time. Capture actual native support/local/world positions and screenshots of both separated decks. Before each pulse inspect `agentControl=inactive` on the target owner. The following examples target slot 1; invert the slot if the nonhost owns slot 0:

```text
coop.debug.naval_lab.action 24f6cae4-560c-46f2-9e1c-08a733892fb9 walk 1 0.25 false
coop.debug.naval_lab.receipt 24f6cae4-560c-46f2-9e1c-08a733892fb9
coop.debug.naval_lab.action f01c76cc-dc25-449c-aec5-eeed43f81137 turn 1 0.25 false
coop.debug.naval_lab.receipt f01c76cc-dc25-449c-aec5-eeed43f81137
coop.debug.naval_lab.action 2478d4e2-6b98-4b11-812e-a9df81359f80 jump 1 0 false
coop.debug.naval_lab.receipt 2478d4e2-6b98-4b11-812e-a9df81359f80
```

Optional only while the same window has time and an actual enumerated slot-five navmesh target exists: `coop.debug.naval_lab.action e49b8d4a-194f-4da6-85f4-43e54e3c703b crew 1 0 false`, then query that same receipt. This is one actor's native scripted movement, not formation orders or safe-placement certification. Do not extend an expired window or adjust budgets to finish the script. Do not approach/connect/collide hulls; native bridge and capture tests remain excluded. Stop on unexpected support loss, damage, nonfinite observations, force activity on follower or native errors. No unsampled continuity claim is available even if sampled positions look right.

Observe window expiry and neutral input, export remaining samples/inspect, then immediately stop (also on any earlier failure):

```text
coop.debug.naval_lab.action c9c0f005-4d5a-422a-a143-76f91bd795f4 stop 0 0 false
coop.debug.naval_lab.receipt c9c0f005-4d5a-422a-a143-76f91bd795f4
```

`outcomeUncertain` means query the same receipt, not a new mutation. Preserve per-process logs/screenshots before parent-owned shutdown/restoration. Process exit is mandatory, including after cancellation or body-hold failure. Managed exception handling guarantees a hold attempt, not successful native disable or a responsive native callback. Exit every owned disposable process even when a terminal receipt or native hold fails. A second election order requires a new disposable run and fresh operation UUIDs, not reopening this fixture. Managed tests cover both orders; neither is a native pass.
