# Two-client native UI lab

`TwoClientNative` is an explicit DEBUG continuation, not production multiplayer naval acceptance. It ports the single-client views, captain ownership, stock team tactics/power prerequisites and native deployment callbacks onto the factory-authority lifecycle. Previous Activation, HeldHelm, SingleClientNative and FactoryAuthorityProbe modes retain their paths. Use `two-client-native` as the fourth argument to `coop.debug.naval_lab.create`.

## Supported slice

Each client has its own synthetic main actor/captain, formation owner, player order owner and local helm. The native `NavalShipsLogic.PlayerControlledShip` getter is scoped to that client's actually occupied helm, regardless of which hull was assigned a Player controller last. The elected **playing client** alone consumes ship inputs and simulates both hulls. The dedicated authority has no mission, hero or party. Remote agents remain native None copies; original agent authorities/revision one are checked throughout.

The override-aware native control/order/status/equipment/target views are installed before deployment or use callbacks. Native hotkey/category and Gauntlet movie/VM availability are required. Supported controls after all gates: F helm take/release, movement axes, Z sails, C camera, ordinary owned main-agent movement. These are installed defaults, not guaranteed current bindings. Input comes from actual native view axes and its native row/rudder conversion, not scripted motion. Complete scalar input records travel through the existing reliable campaign network to the server, which validates owner/slot/incarnation/epoch/sequence/deadline and relays only to the elected host. A host-local owner uses the same route. No direct UI SetInput occurs on a follower. Duplicate/old/expired records and records received before readiness are rejected. One-second deadlines use sender UTC plus a host monotonic expiry; this local lab assumes the same machine clock. Loss of permission, window focus, top mission screen, modal/order/photo/ghost state sends neutral input; stalled publishers expire at the host. Stalled host callbacks still require external process cleanup.

Formation orders, selection, oars-allocation switching, dynamic station management/repair, boarding/cut-loose, capture and weapons are **unavailable**. The native order view is suspended/shortcuts closed, dispatch blocked, and naval feature prompts suspended. The UI is not evidence that full fleet orders work. Campaign entry, damage, capture and result/save isolation remain unchanged.

## Deployment and fixed oars

1. Both empty scenes use existing mission-ready election. Both original owners and epoch one precede factory creation. Host bodies stay factory-active; follower bodies are disabled only at validated successful initialization return. No re-enable or alternative frame setter.
2. Both complete hulls/ten registered actors precede hydration receipts and existing release. Native deployment and player input are separate gates. The existing release unanchors the elected hulls; anchoring and deployment are not physics pauses.
3. `complete-deployment` now targets **both clients** in this mode. Their shared native lifecycle checks views, invokes Mission.OnDeploymentFinished and OnAfterDeploymentFinished exactly once, keeps invulnerability, and enters Battle mode. Failure cannot retry native callbacks. Captains are preassigned separately from helm occupancy.
4. Each owner proposes its four original noncaptain combatants at the first two installed left and right oar stations. Stable keys are root-relative named child paths with sibling ordinals, never native pointers. Each local inventory requires exact ShipOarMachine/StandingPoint types, matching fixture hull, initialized oar and deployment cleanup component, and unique bounded keys. Missing/ambiguous content fails visibly.
5. The server accepts one bounded immutable manifest per owner after its deployment receipt. Only after both offers/deployments does it broadcast both commits. Each client preflights all four stations/actors before entry, refuses foreign occupants/reservations, and calls the real UseGameObject plus OnPilotAssignedDuringSpawn pair. No arbitrary weapon station or foreign displacement. Partial failure is terminal; no rollback repair or repeated use callbacks.
6. Each client observes exact agent/point/machine occupancy, sitting/spawn state and unchanged AI/None role on a later controller tick before acknowledging each manifest. All **four matching acknowledgements** precede native input release. Subsequent ticks keep checking occupancy; loss faults rather than silently reassigning. This observation is not an atomic native barrier or stable contact proof.

Native `ShipOrder.ManageShipDetachments` otherwise prioritizes weapons/vacant helm and can remove an AI oarsman to fill them. In this mode only, the exact synthetic ShipOrder is excluded from automatic allocation from its first reachable call, including deployment. Matching uses retained managed origin/mission identity, not native pointer dereferences after disposal. The immutable manifest replaces allocation only; ship/order/oar/helm/native physics and individual AI/contact/mass ticks remain. No full native `AssignAndTeleportCrewToShipMachines` call is made in this mode because it allocates arbitrary detachments and weapons. Native spawn placement at the four oars is initial deployment, not support correction, a walking test or a later teleport repair.

The stock team tactics and fixed-population power provider are reused. Both formations are explicitly player-owned, not AI fleet commanders. NavalTrajectoryPlanningLogic subscribes to ship spawn/removal, initializes its avoidance simulator at deployment and removes its subscriptions at finalization; no campaign outcome listener is added. The campaign naval opener, end/capture/result behaviors and retreat removal behavior are not installed. Existing capture, sinking and damage guards remain process-lifetime for these disposable hulls.

## Native evidence and managed limits

Fresh installed decompilations under the task's native evidence directory cover NavalShipsLogic, MissionShip (InitForMission, OnTick, UpdateController, OnDeploymentFinished), NavalShipAgents.AssignAndTeleportCrewToShipMachines, NavalTeamAgents.AssignCaptainToShip, ShipOrder (Tick/ManageShipDetachments), ShipOarMachine (deployment/spawn/parallel tick), MissionShipControlView, MissionGauntletShipControlView, MissionGauntletNavalOrderUIHandler, NavalOrderController, TeamAINavalComponent, NavalTrajectoryPlanningLogic, Formation, ScreenManager and the complete input enums/record. ScreenManager.OnGameWindowFocusChange writes the directly publicized focus flag. MissionShip.InitForMission sets ShipsLogic and ShipOrigin before constructing ShipOrder. Exact installed DLL/source hashes are in the task artifact manifest.

Managed tests exercise actual native singleton patch dispatch, native allocator bodies with action/controller/engine boundaries substituted, production station apply with native placement substituted, failure/duplicate behavior, server ownership/ack validation and both election orders over existing queues. They do not establish UI visibility, stable native seating, effective oars/sails, actual keyboard input, safe moving follower collision, parallel cuts or physics resume.

The previous factory live run `7b57c7e9a7ca4bc390c889cb58f830b3` had host testclient2, zero observed follower active/force/precompletion entries, changing frames and ten supported 100 HP actors per client. Only 16 paired samples were exported. It did not include native UI/input/orders or accept propulsion/contact continuity. This source port does not retroactively expand that result.

## Runtime-owner protocol, after independent review

Runtime belongs to the delegated lab agent BannerlordCoop#3. The source worker must not deploy or launch. Use fresh disposable dedicated server plus two clients, prior memory/profile/modal checks, unchanged scoped opt-in and the six managed DLLs from the reviewed freeze only. Verify loaded MVIDs/hashes and catalog; do not deploy publicized/game dependencies. Look up controller IDs with server `coop.debug.players.list`. The current worked pair is `testclient1` / `testclient2`; use them only if actually listed. UUIDs below are single-use worked examples.

Server:

```text
coop.debug.naval_lab.create cbc06f1f-2f32-4103-b413-fb3cf04d275c testclient1 testclient2 two-client-native
coop.debug.naval_lab.receipt cbc06f1f-2f32-4103-b413-fb3cf04d275c
coop.debug.naval_lab.inspect
```

On both clients inspect only. Require epoch one, both scene/hydration receipts, two complete finite hulls, ten 100 HP error-free actors, original authorities, no blockers and zero follower activeFixedTicks/activeParallelFixedEntries/forceApplications/preCompletionOrUnattributedFixedEntries. Check `native.twoClientNative`: ownSlot, main/captain/formation, actual Gauntlet types/movie/data/category, callbacks zero and input false. Record screenshots, no input yet.

Server:

```text
coop.debug.naval_lab.action 65a64859-0316-48a6-95a4-4cd95366ae92 complete-deployment 0 0 false
coop.debug.naval_lab.receipt 65a64859-0316-48a6-95a4-4cd95366ae92
coop.debug.naval_lab.inspect
```

Require both deployed receipts, server nativeControls.ready and both clients inputEnabled, callbacks exactly one each, two station manifests/four genuine oar users per ship on every process and unchanged AI/None authority. In client `coop.debug.naval_lab.samples 0`, nativeControls exposes received input sequences/deadlines and acknowledged station count (no paired probe is promised in this keyboard mode). Inspect `storedInputs` for actual host scalar records. Observe continuing callbacks/occupancy for ten seconds and a usable deck/main-agent view; stop on water-only/missing HUD rather than forcing camera state.

**Manual keyboard acceptance, separate from the synthetic command test below:** within the existing 120-second post-hydration lifetime, take each owner's **own helm** with F, inspect pilot/main/singleton/view identity, then apply W/A/D briefly, Z and C. If host is testclient2, first test testclient1 slot 0; if host is testclient1, first test testclient2 slot 1. Match sentInputSequence and elected-side receipt/record, not screenshots alone. Follower controllers must not receive those records or accrue force counters. Inspect supported agents and actual motion separately; no thrust attribution without a controlled observation. Release F, open/close an ordinary options modal, switch focus between clients, and verify neutralization/one-second deadman plus restored camera/use state. No X allocation, order menu gameplay, weapons, bridge or capture test. Require unsupported controls absent/disabled, not false success messages. Stop if the native heap/controller/standing-point assumptions differ; do not force occupancy or change physics flags.

Server stop on success or any failure:

```text
coop.debug.naval_lab.action 6d0ceefe-f60c-4a4b-9c3f-87d4b4c90e39 stop 0 0 false
coop.debug.naval_lab.receipt 6d0ceefe-f60c-4a4b-9c3f-87d4b4c90e39
```

Preserve each process's logs and before/during/after inspect/screenshots; shared log names can overwrite other clients. `outcomeUncertain` means query the same receipt, never send a fresh mutation. Always terminate the verified run-owned processes and restore canonical modules/profiles via the runtime owner's established cleanup, even after native hold/cancellation failure. Never reopen a fixture in the same process. A reverse election needs a fresh run/UUIDs. Native acceptance, safe follower contact/parallel barrier, full fleet orders, boarding/combat/results, late admission, migration and withdrawal/recovery remain later gates, not abandoned requirements.

## Synthetic native helm commands (not keyboard acceptance)

Only in `TwoClientNative`, the registered server `action` command accepts `native-take-helm` and `native-release-helm`. These target the selected slot's **immutable original owner main actor**, not the elected simulator or its remote puppet. Both require rudder `0`, row `false`, current incarnation/epoch one, deployment, all station acknowledgements, unchanged agent authority and a live game-thread mission. The dedicated server does not use a station. `helm-status` is read-only on both sides; the server has `no_local_native_view`.

The installed ordinary Action/F route calls `Agent.HandleStartUsingAction(point, -1)` or `HandleStopUsingAction()`. The synthetic command uses those exact APIs, **not** `OnPilotAssignedDuringSpawn`, teleportation, controller promotion or an occupancy write. It preserves the normal available/active main-agent interaction screen, no modal/order/photo/ghost state, and for take requires the actual focused **interactable** own point, native range and usability/agent eligibility. Foreign users, reservations and other used objects are rejected rather than displaced. These commands replace only the physical key press. They cannot approach the helm or focus it. If unattended tooling cannot establish normal range/focus, record the rejected precondition and stop that scenario; do not force focus, camera, position or occupancy to make it pass. Synthetic sail commands still have their existing independent view/focus permissions.

After the preceding deployment and station gates, inspect `coop.debug.naval_lab.helm-status` on each client. Match incarnation, epoch, owner, slot, ship ID, main actor, point ID/runtime-generation flag, user/pilot, used-object identity and native view. The `preconditions` object includes distance, native interaction distance, point permissions/reservation, focused/interactable point IDs and active/top-screen/window-focus state. Read failures are unavailable, not successful preconditions.

Worked slot 1 example, only if the actual manifest maps `testclient2` to slot 1. Run mutations and receipts on the **server**, focused status on **testclient2**. Keep this inside the existing 120-second fixture lifetime:

```text
coop.debug.naval_lab.action b732cfed-e451-4f02-b266-cb2e03d490c5 native-take-helm 1 0 false
coop.debug.naval_lab.receipt b732cfed-e451-4f02-b266-cb2e03d490c5
```

Then on the owner:

```text
coop.debug.naval_lab.helm-status
```

`dispatched:synthetic_native_helm_pending_observation` is an immutable **dispatch-only receipt**, never a taken/released receipt. Await that exact operation's `phase=observed_taken` in owner status after a later mission tick. Require the current native user/pilot and main-agent used object to still match the exact requested point, and `viewMatchesOwnHelm` plus `inputPermitted` before any sail request. The historical phase does not replace the independent current identity read. Inspect the other client separately; remote pilot replication is explicitly `unimplemented` and must not be marked passed. The sail examples below may then run without repeated Z presses, but are still not keyboard evidence.

Release on the server:

```text
coop.debug.naval_lab.action af0fa5d0-0f0a-4e28-bceb-87a7d58dc9cb native-release-helm 1 0 false
coop.debug.naval_lab.receipt af0fa5d0-0f0a-4e28-bceb-87a7d58dc9cb
```

Await this release operation's owner `helm-status` with `phase=observed_released`, then independently require both point user/pilot and agent used object clear and the view detached. Only a **later tick with both identities clear** completes release. A callback count, native method return, elapsed timer or receipt never suffices. A take similarly needs both exact identities. Observation is bounded to two monotonic seconds; timeout, contradictory foreign identity, mission/authority loss or uncertain native exception faults terminal hold, not retry/repair/rollback. Partial identity remains pending only within that bound. Terminal cleanup does not re-dispatch an uncertain synthetic native operation. The exact native stop/capture guards remain installed; native spontaneous stops are not suppressed.

A conflicting new operation is rejected while pending. Duplicate operation IDs never re-dispatch through the action store, including after observation; query the original receipt on uncertain transport outcomes. Status retains the current operation through failure/hold until teardown. Its bounded `lastOutcome` snapshot keeps the last completed/failed operation readable while a later request is rejected or pending; a later completion replaces that snapshot. No helm lease timer implicitly releases an occupied helm; the existing fixture lifetime/stop policy still applies. After any failed phase preserve receipt/status/current identities and logs, stop and exit the owned processes. No in-process recovery or reverse-election reuse. Repeat slot 0 with fresh UUIDs only after verifying its owner; reverse election requires a fresh reviewed run. Actual F, keyboard Z, camera and rendered/contact acceptance remain separate manual gates.

## Sail feedback and focused commands

The follower sail icon now presents the elected simulator's observed `MissionSail.TargetSailSetting`, using the same categorical mapping as the native HUD. This is the **actuator target**, not the command echo and not a measured cloth deployment percentage or proof of propulsion. The existing 20 Hz frame message carries at most two ship-id/state/type entries plus a one-second UTC deadline. Incarnation, epoch, increasing frame sequence, original-owner readiness and successful frame application precede acceptance; the adapter checks exact ship IDs and its current native readiness again. This local lab uses the same machine clock. Expiry is also tracked locally with a monotonic clock. No follower sail, controller, actuator or physics state is written.

The exact owner view's native `UpdateShipValues` postfix updates only `SetSailState` and `SailType`. Missing/stale/unready feedback hides the icon (installed prefab has exact type selectors 0/1/2, not -1) and shows **Sail status unavailable** above the HUD. Commands are not disabled just because feedback is unavailable. The noninteractive label belongs to that view's UI context, is unique across refreshes and removed before screen finalization. Single-client native and the elected simulator retain their stock readout. The native tick's later input handling changes request state only, not the displayed sail VM.

For focused observation run `coop.debug.naval_lab.sail-status` on either client. On the server the same read-only command returns fixture/election identity and `no_local_native_view`, correctly, because the dedicated server owns no ship. Require the same incarnation/epoch and exact manifest ship ID. Client fields include owner/slot, requested sail (0=Raised, 1=SquareSailsRaised, 2=Full), host-observed states for both hulls on the elected simulator, its last received input sequence/state/deadline, follower received state/sequence/remaining lifetime, VM presentation/type, and unavailable-label state. Historical received inputs are explicitly not proof of current actuator state. `feedbackFresh` describes the received sample; `ownerView` independently describes whether this owner's helm is actually displayed. Read-only inspection does not refresh or correct the UI. A VM value may be from the prior render tick; take another read after a tick if required.

After the preceding deployment/occupancy gates and either actual F helm use or a matching `observed_taken` synthetic operation below (with current owner/view identity independently confirmed), synthetic sail input can replace repeated Z presses while investigating delivery. These actions still require the owner's live permitted native view (focus, top screen, no dialog/photo/ghost mode, current helm and supported sail feature). They set the view request and call the **same** native converters and owner input relay, never directly applying simulator input. They do not take the helm, bypass readiness or prove keyboard acceptance. State-changing commands run on the server only; keep the target client focused when issuing them remotely. Reuse the action/receipt interface, not a new mutation protocol.

Worked example with the previously looked-up `testclient1` (slot 0) and `testclient2` (slot 1), if the actual manifest still maps them that way:

```text
coop.debug.naval_lab.sail-status
coop.debug.naval_lab.action 819bd17a-5fda-45c4-9bd2-fd8945ac7590 sail-full 1 0 false
coop.debug.naval_lab.receipt 819bd17a-5fda-45c4-9bd2-fd8945ac7590
coop.debug.naval_lab.action e95ec62b-f38a-490e-bd6a-83625801f2c7 sail-raised 1 0 false
coop.debug.naval_lab.receipt e95ec62b-f38a-490e-bd6a-83625801f2c7
```

Use a new UUID per intended action; uncertain outcomes use the **same receipt**, not a new command. `requested:synthetic_input_not_keyboard_evidence` means the owner requested it, not that sails moved. On both clients use `sail-status`, not broad snapshots, to match the elected simulator's `hostReceivedInput[1]` then `hostObserved[1]` to the follower's matching ship `received`, `presentation` and hidden unavailable label. There can be a native actuator tick between receipt and observation. Inspect after at least one frame, and do not accept the request alone. `sail-square-raised` also exists for the native categorical input; the fixed hull may have only one sail type. Slot 0 uses the same commands with `0` instead of `1`, after verifying ownership. Repeat the follower test under reverse election in fresh processes.

Keep the follower ticking while the elected simulator stops publishing (owned-process suspension/termination under the runtime owner's existing protocol only). Within one second of the last sample, require type -1, invalid categorical state and visible **Sail status unavailable**, never the last valid icon. Resume only if the original fixture remains valid; otherwise terminal hold/process exit is mandatory. Verify actual Z keyboard input separately, icon recovery on fresh valid samples, wrong/no helm hiding, no duplicate labels after view reinitialization, modal visibility and that the label does not intercept input. Capture one screenshot per visible transition plus focused command outputs. No nativePass or deployment approval follows from managed tests or this candidate freeze.
