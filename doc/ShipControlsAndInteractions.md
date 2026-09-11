# Ship controls and interaction prerequisites

Developer and tester reference for campaign-independent War Sails battles. Investigation: **2026-09-10**. Vanilla facts below come from the installed assemblies identified in the evidence appendix, not assumed controls or signature-only API documentation.

## Read this before testing

**Production naval entry remains deliberately denied. No playable co-op naval acceptance has been demonstrated.** See [War Sails battle foundation](WarSailsBattleFoundation.md) for scope and [headless integration matrix](WarSailsE2ETestMatrix.md) for managed coverage. Their older runtime instructions are not permission to repeat a failed physics experiment.

The latest supplied native run, `aa7054be45734205be48a441f7b26acf`, elected `testclient2`, epoch 1. Both clients became ready, but the host reported `ship.activation_unconfirmed:slot_0`. Startup `DisableDynamicBodySimulation` changed active simulation from true to false; the attempted host `EnableDynamicBody` did not restore it. Dynamic-body existence, raw body flags `327736`, and physics-state getter `true` did not distinguish these states. Both clients reported zero active fixed ticks and zero measured force applications. That run tested **no helm, keyboard input, walking, boarding or contact acceptance**. These observations neither prove sleeping nor an irreversible native operation. No supported inverse for `DisableDynamicBodySimulation` has been established; do not substitute flag restoration or another equivalent getter run.

An **owner-local, held-helm take/release implementation is in flight**, not reviewed or live-approved by this investigation. Source snapshots already contain candidate `held-helm`, `take-helm` and `release-helm` names. They are provisional, not an executable acceptance protocol. The intended test keeps ship bodies held and tests use lifecycle only; it is not keyboard/camera, propulsion, remote-use replication or recovery certification.

### The regression this document prevents

A script calling `PlayerShipController.SetInput` can reach ship actuators without putting an agent at the helm. Conversely, putting an agent at the helm does not establish active physics. These are different tests:

1. Actual player focus and **Action** input reach the native use path.
2. An agent occupies the correct helm and receives use/stop callbacks.
3. The correct local view reads keys and selects the intended ship.
4. Accepted inputs reach the elected simulator, not an independent follower simulation.
5. Crew, sails, rudder and water produce effective forces on an active body.
6. Followers reproduce usable collision/navigation/support, not merely a rendered hull.

Never label any one of these as proof of all six. In particular, **adding only `MissionShipControlView` is not complete naval keyboard support**: the Gauntlet override supplies the naval category registration and discrete action dispatch.

## Roles that must not be conflated

| Identity/state | Native meaning | Co-op implication |
| --- | --- | --- |
| Helm pilot | `UsableMachine.PilotAgent` is `PilotStandingPoint?.UserAgent`. | Occupancy of one concrete station, not permission to simulate ships. |
| Ship captain | `MissionShip.Captain` is `Formation?.Captain`. | Taking the helm does not assign captain. A label such as `syntheticCaptain` is not this native read. |
| Ship controller | `MissionShip.Controller`, with Player/AI/None controller selection. | `PlayerShipController` can store scripted input with no pilot. Native Player selection is not the co-op simulator election. |
| Agent controller | `Agent.Controller` / `IsPlayerControlled` / `IsAIControlled`; local `Mission.MainAgent` is separate identity. | Keep each participant's agent authority. A remote puppet must not become Player merely to satisfy native auto-selection. |
| Formation ship versus stepped ship | `AgentNavalComponent.FormationShip` is assignment; `SteppedShip` is observed support. `MissionShip.IsPlayerShip` uses the main agent's formation ship. | Walking onto another ship does not transfer admission, captain grant or physics authority. A bridge's combined island is not a unique support identity. |
| Elected shared simulator | Existing co-op client election and epoch, not a vanilla agent property. | One elected **client** integrates shared ships. The dedicated campaign server has no playing party, main hero or native mission simulation. Followers must not integrate independently. |

## Player action matrix

All keys below are **verified installed defaults, remappable**, not the tester's necessarily current bindings. Record actual bindings and active input device for every input test. `Generic`, `CombatHotKeyCategory` and `NavalShipControlsHotKeyCategory` are distinct contexts. Controller entries use the installed `InputKey` names, not assumed platform button glyphs.

| Intent / action | Context, action and default | Essential eligibility / result | Current co-op status |
| --- | --- | --- | --- |
| Take/release helm | Combat `Action` **13**, **F** / `ControllerRUp`, pressed | Focus usable helm standing point; able, unmounted agent; usable/vacant point. Press again while using to stop. This is latched use, **not holding F down**. | Previously bypassed by script input. Held-use candidate pending. |
| Steer and row | `MovementAxisX/Y`, copied from Generic: **A/D**, **W/S** / left stick | Native Player-controlled ship and control view; axes generate rudder and lateral/longitudinal rower input together. Oar request is not guaranteed thrust. | Existing lab `helm`/`probe` inject limited rudder/forward-row records; not keyboard tests. |
| Change rowing crew allocation | Naval `ToggleOarsmen` **111**, **X** / `ControllerLDown`, released | No active bridge and oarsmen level not locked; cycles full/half/off allocation. Does not itself command forward motion. | No accepted player action integration. |
| Furl/open sails | Naval `ToggleSail` **110**, **Z** / `ControllerLUp`, released | Intact sails, no active bridge; cycles sail setting, including mixed-sail intermediate state. | Not covered by lab rudder/row action. |
| Ship camera | Naval `ChangeShipCamera` **112**, **C** / `ControllerLRight`, released | Not in allowed ranged aiming; feature enabled. Cycles Back/Shoulder/Front. Back-camera distance: Combat **28/29**, **NumpadPlus/NumpadMinus**, held. | Native view/camera integration unaccepted. |
| Select own ship formation | Naval `SelectShip` **113**, **E** / `ControllerLThumb`, released | Focused, nonempty player-owned formation within 300 m and order UI available; selects formation, does not take its helm. | Not implemented as a lab action. |
| Order/cancel boarding | Naval `AttemptBoarding` **114**, **R** / `ControllerRThumb`, released | Focus eligible target; start predicate 50 m, current-target cancellation predicate 300 m. Earlier focus bounds are 100 m enemy / 350 m friendly, so enemy cancellation input is unreachable beyond 100 m; other gates below still apply. An order is not a connected plank. | Deferred; current fixture is noncombat and starts 60 m apart. |
| Order cut loose | Naval **111**, same **X** as oars | With an active bridge, eligible and not already cutting/disconnection-blocked, requests crew cut-loose order instead. | Deferred; no occupied-removal acceptance. |
| Hook/grapple station | Combat **F** to use; Combat `Attack` **9**, **LeftMouseButton** / `ControllerRTrigger`, released to throw | Usable `ShipAttachmentMachine`, valid hook animation and outward look; native throw/target/attachment state machine. | Deferred, not a lab bridge-connect command. |
| Cut a bridge at station | Combat **F** at available Bridge prompt | Bridge-connected attachment; break animation runs, then disconnect/stop. Main-agent use on a player-team ship also calls `OwnerShip.ShipOrder.SetCutLoose(true)` near animation start, coupling this route to the ship-wide X order. Destruction can occur in the same `OnTick` invocation as disconnect; no evacuation frame is guaranteed. | Deferred; do not test with unsupported occupants. |
| Aim/fire ship ballista from helm | Naval `ToggleRangedWeaponOrderMode` **115**, **RightMouseButton** / `ControllerLTrigger`, released; Combat **9**, left mouse / right trigger, released to fire | Eligible mounted weapon, allowed aiming view, crew user not struck; weapon must be ready for `Shoot` to succeed. | Weapons/damage excluded from current lab. |
| Use weapon station directly | Combat **F** at a valid weapon standing point; attack input while piloting | Concrete weapon/pilot/ammo/reload state, native siege-weapon aiming/view. Separate from remote-from-helm aiming. | Excluded; no ship-weapon replication acceptance. |
| Walk/turn/jump on deck | Generic **W/A/S/D**; normal look; Combat `Jump` **14**, **Space** / `ControllerRDown`; `ToggleWalkMode` **30**, **CapsLock** | Active owned main agent, normal input controller and usable support; release station first for a free-walking test. | Scripted walk/turn/jump pulses exist; no moving-deck acceptance. |
| Move in water | Same movement; in water the main-agent controller reads held **14/15**: **Space/Z** / `ControllerRDown/ControllerLDown` as Jump/Crouch flags | Native water movement mode. Managed input is verified; native vertical response is not established here. | Swim/dive and safe return-to-deck unaccepted. |
| Climb back aboard | Combat **F**, eligible Climbing Net standing point | Installed `ClimbingMachine`, `climb_end`, animations and deployment cleanup components. Swimming-only standing-point subtype rejects agents outside water. | Not an executable lab scenario. |
| Row at an individual oar | **No ordinary player binding offered** | `ShipOarMachine.IsFocusable == false`; `OnInit` disables its pilot point for players. AI crew operate these stations. | Do not add a player oar-use cheat and call it native player interaction. |
| Ship retreat / participant withdrawal | Native `ShipOrder.SetShipRetreatOrder`; **no naval retreat shortcut verified here** | Native boundary retreat differs from co-op participant departure/evacuation. Generic Tab is `Leave`, not proof of either lifecycle. | Production withdrawal/recovery deferred. Lab `stop` is teardown only. |

**Context conflicts are intentional:** Z is on-foot crouch/water Crouch versus naval sails; X is Combat weapon-mode versus naval oars or cut loose; R is Combat camera toggle versus naval boarding; E is Combat kick versus naval formation selection; RMB is Combat defend versus naval ranged-mode toggle. Do not bind by physical key alone or assume both consumers are harmless. Verify focus, active contexts, modal/order state and actual dispatched action.

## Native interaction lifecycle

The short type names in this section resolve to exact assembly/FQN owners in the appendix.

### 1. Establish the actor, scene and subscribers before use

- Identify the owned main agent, its native controller, formation, assigned ship, actual support, target machine, and **pilot standing point**. The usable object is the point, not a `MissionShip` or `ShipInputRecord`.
- `MissionMainAgentController.OnPreMissionTick` runs focus, control, use-state and look handling only for an active, non-AI main agent with screen/controller activation available, outside ghost/photo modes. `MissionMainAgentInteractionComponent.FocusStateCheckTick` requires Action pressed, interaction enabled and no radial/order menu. Focus discovery and standing-point eligibility must succeed; direct `UseGameObject` is not a test of them.
- `UsableMissionObject.IsDisabledForAgent` includes deactivation, mounted agent, player-disabled and able-to-use checks. `StandingPoint.IsUsableByAgent` separately checks its Player/AI controller mask. Record both eligibility and occupancy/reservation (`UserAgent`, `MovingAgent`), rather than forcing use through a disabled prompt.
- Native `Mission.OnDeploymentFinished` does more than set `IsDeploymentFinished`: it dispatches team-AI callbacks, then behaviors in reverse list order, then `DeploymentFinishedEvent`. `NavalShipsLogic.OnDeploymentFinished` visits ships; `MissionShip.OnDeploymentFinished` visits attachment, oar, climbing, helm and weapon children. A field assignment or `SetDeploymentMode(false)` is **not** this callback chain.
- Specifically, `ShipControllerMachine.OnDeploymentFinished` installs animation reset, hand-IK clear and damage components, caches `NavalShipsLogic`/`NavalAgentsLogic`, and marks opposite-side helm points AI-only. Ordinary helm ticking can read these collaborators. A bounded synthetic mission must deliberately provide the needed lifecycle, not claim that every native naval behavior is present.

Do not import the entire campaign mission opener to get these callbacks. The SP naval opener uses campaign fleets and outcome behaviors; `NavalBattleEndLogic` has campaign captured-ship writes. A campaign-independent lab must preserve its isolation gates.

### 2. Take the helm: distinguish player use from spawn placement

**Normal input route:** `MissionMainAgentInteractionComponent.FocusStateCheckTick` calls `Agent.HandleStartUsingAction(point, -1)`. Outside native `GameNetwork` client/replay mode, this calls `Agent.UseGameObject`. That establishes target/frame state and `CurrentlyUsedGameObject`, calls the point's `OnUse`, then `Mission.OnObjectUsed` broadcasts to mission behaviors. `StandingPoint.OnUse` and `UsableMissionObject.OnUse` perform component/user setup and target alignment; displaced AI/reservation handling is not equivalent to writing the `UserAgent` field.

**Native spawn shortcut:** `NavalShipAgents.AssignAndTeleportCrewToShipMachines` uses `agent.UseGameObject(machine.PilotStandingPoint)` **followed by** `machine.OnPilotAssignedDuringSpawn()`. The helm override ensures components, sets relaxed rudder action, teleports to the point, clears scripted movement and sets facing. This is valid evidence of a native spawn-use lifecycle; it explicitly bypasses walking, focus and the F input. The held-use candidate is in this category, not an authentic keyboard approach test.

Captain assignment is separate: `NavalAgentsLogic.AssignCaptainToShip` delegates to `NavalTeamAgents.AssignCaptainToShip`, which transfers if needed and assigns the formation captain. The deployment variant also rearranges/teleports crew and stations. Do not use that broader operation as an assumed requirement for ordinary take-helm. Synthetic infantry is not shown to be disqualified by the reviewed use/helm methods, but synthetic infantry is also not proof of the real participant hero path.

**Success checks:** all three agree on the same object and actor: agent `CurrentlyUsedGameObject`, point `UserAgent`, machine `PilotAgent`. Capture `Formation.Captain` independently. Verify delivered `OnObjectUsed`, posture/hand IK, current action and retained occupancy on later callbacks. A screenshot of a man near the rudder or an `applied` receipt is insufficient.

### 3. Controller selection and input view are separate prerequisites

`MissionShip.UpdateController` notices a helm pilot whose `IsPlayerControlled` is true, disables formation AI, creates a Player ship controller and seeds it with previous input. Losing that pilot selects None/AI according to ship/formation state. `autoUpdateController:false` suppresses this selection, **not** the `Controller.Update(dt)` consumption in `MissionShip.OnTick`.

`PlayerShipController.SetInput` simply stores a record; `Update` returns it without a pilot check. `MissionShip.OnParallelFixedTick` passes input through `ShipInputProcessor`, `ShipActuators`, then `NavalPhysics.SetShipForceRecord`. This explains why old lab rudder injection was not a helm-use test.

Two view layers matter:

| Owner | Required work / ordering |
| --- | --- |
| `NavalDLC.View.MissionViews.MissionShipControlView` | `OnBehaviorInitialize` caches naval ship logic. `OnObjectUsed` recognizes the **main agent's** used standing point and records `ControllerMachine`/associated ranged weapon. `OnPreMissionTick` reads axes and handles camera. `OnObjectStoppedBeingUsed` clears use/camera-related references. Install/listen before take, or demonstrate deliberate state hydration; adding a view after the callback does not replay it. |
| `NavalDLC.GauntletUI.MissionViews.MissionGauntletShipControlView` | `[OverrideView(typeof(MissionShipControlView))]`. `OnMissionScreenInitialize` creates its movie/VM, finds order/crosshair/highlight/status views, registers `NavalShipControlsHotKeyCategory` on the scene input layer and builds key prompts. `OnMissionScreenTick` resolves the player ship, updates eligibility/focus/HUD and dispatches discrete actions in `TickInput`. It also manages first-person/crosshair transitions. Missing optional views can suppress individual functionality; do not assume the order UI exists. |

Native named naval view creation includes the control view. `TaleWorlds.MountAndBlade.View.NavalViewCreator.CreateMissionShipControlView` delegates to `ViewCreatorManager.CreateMissionView<MissionShipControlView>`, so active override resolution matters. `ViewCreatorManager` resolves named factories and overrides/default views; merely opening `NavalMissionState` with the arbitrary name **`NavalLab`** does not prove equivalent view composition. Inventory actual runtime behavior types, active override assembly, input category availability through `HotKeyManager.GetCategory`, scene-layer registration and loaded movie/VM. A category class existing in a DLL is not runtime registration evidence. The old/captured lab opener supplies no explicit naval control view; the held-use candidate must not be described as full keyboard support.

**Singleton trap:** base `HandleShipControls` chooses `NavalShipsLogic.PlayerControlledShip`, not the ship associated with `ControllerMachine`. `NavalShipsLogic.OnShipControllerChanged` maintains one reference. The fixture explicitly makes slot 0 then slot 1 Player on each client, so naive native view insertion can control the last ship instead of the local helm. Native auto-selection also sees remote agent copies as `None`, not local Player. Neither switching every copy to Player nor turning on auto-selection everywhere satisfies the co-op contract. This is a prerequisite to a future reviewed input integration, not a new architecture prescribed here.

### 4. Ongoing input, effective propulsion and release

- Base `HandleShipControls` reads axes, applies a 0.2 dead zone, derives rower directions and rudder, and writes a complete input record every pre-mission tick. `TickRowerInput` also computes a timing-dependent longitudinal-double-tap field; this document does not promise a boost or treat the lab boolean row argument as that input. Returning axes to zero changes the record; it does not guarantee instant hull stop.
- Base axis handling does not duplicate every Gauntlet modal gate. Gauntlet `TickInput` returns for absent player ship/input, photo mode, dialog/radial/order menu or ghost mode; features have further gates below. Do not infer that hiding HUD, suspending a feature or opening a dialog neutralizes all continuous ship inputs.
- `ShipInputProcessor` turns rudder/row/sail requests into actuator settings. `ShipActuators.FixedUpdateRowers` depends on floating/not-abandoned state, used oars, oar condition and submerged-height/force calculations. Sail input needs real sails and wind/actuator physics. A seated crew count, oar animation or input record alone is not measured propulsion.
- `NavalPhysics.OnParallelFixedTick` and `OnFixedTick` gate their force work on `HasDynamicRigidBodyAndActiveSimulation()`. This must be measured separately from use, `GetPhysicsState`, body existence, visual movement and wrapper calls. No explicit ship-body wake/enable operation was found in the reviewed take/release, helm animation or controller-selection bodies. Native side effects remain opaque.

**Normal release:** Action pressed while using reaches `Agent.HandleStopUsingAction`, then the normal `StopUsingGameObject` path outside native client/replay mode. `StopUsingGameObjectAux` invokes point `OnUseStopped`, clears agent use/target state, performs component cleanup and emits `Mission.OnObjectStoppedBeingUsed`. Point `UserAgent` becomes null. Helm components clear hand IK/reset animation. Base view clears controller/weapon references and listener blend; later camera handling restores offset/FOV/collision settings. The Gauntlet view restores its saved first-person/crosshair state on player-ship loss. Ordinary helm ticks can also stop use if animation cannot be maintained.

**Co-op release contract:** native cleanup must actually run on the appropriate owner/replicas, and any forwarded input/grant must stop or neutralize under the existing authority rules. Do not assume `StopUsingGameObject` alone clears an explicitly pinned Player controller's last record, or that a network stop packet will arrive while a process cannot tick. Verify deadline, release, fault, disconnect, epoch change and teardown independently. Stop callbacks are not evidence of body reactivation.

## Other interactions: prerequisites, cleanup and evidence

### Oars and sails: crew orders versus ship input

`MissionGauntletShipControlView.TickInput` cycles oarsmen allocation with `(OarsmenLevel + 2) % 3`: normally **2 full, 1 half, 0 off, 2 full**. `GetCanToggleOarsmen` requires no active bridge and an unlocked level; `ShipOrder.SetOrderOarsmenLevel` changes AI station assignment/removal. Forward row input is a separate field. With an active bridge, X instead requests `ShipOrder.SetCutLoose(true)` when not blocked/already cutting. `SetCutLoose` cancels boarding and changes detachment/machine tasks; it is not a synchronous physical detach acknowledgement.

Individual oar stations are AI crew stations in ordinary play: `ShipOarMachine.IsFocusable` returns false and `OnInit` calls `PilotStandingPoint.SetIsDisabledForPlayersSynched(true)`. This is the exact player-disabled mechanism, not a claim that this class calls `SetUsableByAIOnly`. `OnDeploymentFinished` installs reset/IK/damage-action components. `OnPilotAssignedDuringSpawn` sets sitting posture, placement and force multiplier; ongoing tick publishes `MissionOar.SetUsed` from the actual pilot and drives animation. Ramp registration can stop a user/reserved AI and deactivate an oar point; deregistration restores availability after the last disabling ramp. Observe each station and effective used-oar count, not five agents simply spawned on deck.

Sail toggle requires `ShipSailState.Intact`, no bridge and unsuspended feature. The cycle is **Full (open), SquareSailsRaised (square furled, lateen open, hybrid only), Raised (furled), Full**. Despite its name, `Raised` maps to actuator settings zero; it does not mean maximum thrust. `SetSailInput`/Gauntlet `SailControl` persist independently of neutral rudder/row axes. Record requested and actual sail settings, wind, sail condition and actuator output; release policy must not accidentally equate an all-default record with the desired sail state.

**Authority/status:** station occupancy and crew orders must be applied by the appropriate agent/shared-state authorities without changing player ownership. Ship physics remains elected-client work. The captured lab spawns four owned AI crew plus a synthetic main agent per ship but does not demonstrate fully crewed native oar detachments. Current `helm` has no sail or crew-allocation argument. These controls are unaccepted in co-op.

### Camera, on-foot movement, swimming and returning aboard

Naval E selection calls the available order UI's `SelectFormationAtIndex`, requiring that focused formation's `PlayerOwner == Agent.Main`. Observe the selected formation index and command UI; it does not acquire/release a station or transfer captain, agent or simulator authority. Subsequent troop orders need their own input/authority evidence. The lab `crew` pulse targets one AI agent with scripted movement, not this native formation-order path.

Camera mode changes are local presentation, not simulator election. `GetCanChangeCamera` blocks change during allowed ranged aiming or feature suspension. Base Back-camera zoom reads Combat 28/29; Front ignores collision, while release restores ordinary camera settings over later ticks. Verify the view's actual helm, camera mode, listener/offset state and post-release normal controls. A moving camera does not prove the hull moved.

`MissionMainAgentController.ControlTick` writes `MovementInputVector` and event flags; normal jump is an input edge, while in water held Jump/Crouch flags are collected. `AgentNavalComponent` observes water-surface/diving/land transitions, changes combat/weapon state and performs drowning/burning checks. Do not infer vertical swimming mechanics, safe dive duration or invulnerability from a key name. Native locomotion/contact remains an engine behavior to measure.

`AgentNavalComponent.OnTickParallel` observes `GetSteppedEntity` and updates `SteppedShip`/cached bridge support; `GetSteppedCombinedShipIsland` includes plank support. `OnTick` supplies agent weight/position to cached physics/support managers. For walking/jump tests record support entity/root, individual ship or bridge identity, local and world pose, movement flags, water/air/land state and controller identity. Visual hull-following or world-position displacement can be passive deck transport, not locomotion. Aggregate crew mass is not proof that remote-owned crew contributed correctly.

`TaleWorlds.MountAndBlade.Objects.Usables.ClimbingMachine` exposes the **Climbing Net** F prompt. `OnInit` requires a `climb_end` child and animation setup. `OnDeploymentFinished` installs attachment/gravity-reset and animation cleanup components and machine-managed user positioning. `OnUseAction` attaches the agent; `OnTick` advances climb target/animations, excludes gravity and eventually restores gravity and stops use. `StandingPointWithSwimmingLimit.IsDisabledForAgent` rejects an agent **not in water**, despite the potentially misleading type name. Confirm the actual point subtype/content on the chosen net. The native path is not a safe co-op evacuation API.

**Cleanup/status:** release machines before free movement, clear temporary owned input/scripted movement after lab pulses, and restore gravity/attachment after climb completion/interruption. Preserve individual and agent-controller identities through support transitions. Current walk/turn/jump/crew actions are script injection, not keyboard or formation-order evidence; moving follower decks, swimming and climbing remain unaccepted.

### Ship-mounted weapons

There are two routes, both outside the damage-disabled lab:

- **From helm:** use callback finds a ranged weapon beneath the helm's root. Naval RMB release toggles ranged mode; `MissionShip.OnSetRangedWeaponControlMode` updates `ShipBallistaAI` direct-control state and sail-rope visuals. Base view enables `SetPlayerForceUse` while allowed and supplies the siege-weapon view/camera path. `GetIsRangedWeaponAvailable` requires an installed, enabled, nondeactivated, nondestroyed weapon; allowed aiming also checks order state and the cached weapon. `GetCanShootBallista` additionally requires a user not in struck action. Left-mouse release requests `MissionShip.ShootBallista`.
- **Direct station:** use its actual standing point through the ordinary F lifecycle. Base naval view records `DirectlyControlledRangedSiegeWeapon` and tells the ship to enter ranged-control mode. `RangedSiegeWeapon.OnTick` outside native client/replay advances weapon state, reads pilot attack flags and handles aiming/reload. This is not a remote-from-helm toggle.

`RangedSiegeWeapon.Shoot` succeeds only in `WeaponState.Idle`; it initiates a firing state, not an immediate projectile. Ammo loading, animation timing and `ShootProjectile`/`Mission.AddCustomMissile` are downstream. Observe concrete weapon identity, pilot/load/ammo stations, ammo/state transitions, aim and single projectile creation on the responsible side. Do not claim generic support for every installed siege weapon from the ballista path.

On helm stop, clear force-use/view state; on direct-station stop, clear direct weapon reference and ranged-control mode. Normal point cleanup must free reservations/users and animations. Shared projectile, ammo, damage, casualty and weapon state need their own co-op authority/replication contract. Native `GameNetwork.IsClientOrReplay` and native synchronized setters/messages do **not** automatically map to this mod's clients/transport. No current lab weapon test is authorized or accepted.

### Boarding order, hook throw, plank crossing and cut loose

These are distinct actions and must have distinct observations:

1. **Boarding order at helm:** Gauntlet focus selection must identify the intended ship. `UpdateFocusedShip` runs before `TickInput` and passes 100 m enemy / 350 m friendly bounds to `CheckFocusableShip`, which rejects out-of-range targets by battle side before hit testing. `GetCanAttemptBoarding` checks feature suspension, target permanent connection block, target `ShipOrder.IsBoardingAvailable`, absence of an already active bridge to it, distance and ranged-aim mode. Its start predicate uses 50 m; cancellation of the currently attempted focused target uses 300 m. That 300 m predicate does not extend focus: enemy-target cancellation through the native input route is unreachable beyond 100 m. Friendly focus can extend to 350 m, but cancellation still requires at most 300 m and all other gates. Temporary target connection blocks/same-target logic can disable starting. `ShipOrder.SetBoardingTargetShip` schedules crew/detachment work and preferred attachment targets; cancellation sets target null. No bridge is guaranteed by the order receipt.
2. **Manual hook throw:** F uses the available `ShipAttachmentMachine.PilotStandingPoint`; its `OnDeploymentFinished` installs animation reset and extra-weapon removal and configures point locks. With no attachment, `OnTick` equips the hook extra weapon and plays ready action. Player attack release with outward-facing look requests release animation; only after its animation parameter does `ConnectWithAttachmentPointMachine(null, ..., connectionInitializedByPlayer:true)` start the player throw. AI instead selects an attachment target. A direct forced-bridge call skips this interaction and is not a player throw test.
3. **Actual connection:** `ShipAttachment` moves through throw/pulling/bridge/failure/broken states. Candidate checks in `ComputePotentialAttachmentValue` include distance when requested (40 m), facing and intersections. These station checks differ from the helm's 50 m order threshold. Geometry, target slots, rope/bridge state and blockers still decide the outcome. Do not treat 50 m as a guaranteed connection radius.
4. **Crossing:** a real bridge has navmesh, collision, stepped-agent manager and constraint/attachment state. `BridgeConnected` transitions notify both ships. Walk/AI traversal must show ship-to-bridge-to-ship support and retained ownership, not only a plank render or combined-island match.
5. **Cut loose:** helmsman's X issues a crew order. At an available Bridge station, F begins break animation; when the pilot is `Agent.Main`, action progress is below 0.1 and the owner ship is on the player team, `OnTick` also calls `OwnerShip.ShipOrder.SetCutLoose(true)`. The input routes differ, but station cutting can therefore cancel boarding and change ship-wide attachment tasks, not just cut the selected bridge. `OnTick` calls `DisconnectAttachment` on completion, then stops use. Disconnect marks `BrokenAndWaitingForRemoval`, immediately changes ramp/connection visibility and notifies both ships. The same `OnTick` invocation can then destroy the attachment; there is no guaranteed intervening frame or evacuation window. Instrument the transition, destruction and support removal rather than expecting a separately inspectable pending state. Preflight all affected connections and occupants before authorizing either route. `OnTick` deactivates unavailable/blocked points; do not force use through this gate.

**Cleanup/authority/status:** normal stop removes the extra hook weapon and resets animation, but stopping a user is not equivalent to destroying an already-created connection. Shared attachment identities, endpoint graph, constraints and removal must agree on the elected simulator and followers; local agents keep their own authorities while crossing. Current fixture teams are explicitly made nonenemy, takeover is disabled, and no accepted bridge creation/traversal/retirement path exists. Ordinary enemy-target logic cannot be assumed to connect this friendly fixture unchanged. Future boarding scenarios are **not executable** with current accepted lab actions.

### Retreat, capture and withdrawal boundaries

`ShipOrder.SetShipRetreatOrder` is a native ship movement order. `TaleWorlds.MountAndBlade.ShipRetreatLogic.OnMissionTick` checks retreating ships at five-second intervals after deployment. Near/outside mission boundaries it detaches assigned off-ship agents from the retiring ship, marks reserved origins routed and calls `NavalShipsLogic.RemoveShip`. This is not proof of safe evacuation of foreign occupants or of the required co-op participant-wide removal. No direct player naval-retreat binding or complete UI-to-retreat path was verified in this bounded research; do not invent one from Generic `Leave`/Tab.

Enemy/vacant helm use can also enter `ShipControllerMachine.OnTick`'s capture action/timer and `OnShipCapturedByAgent`, subject to takeover, vacancy, connection and role gates. That is distinct from friendly helm control. Do not test it in the current fixture: `SetCanBeTakenOver(false)` and campaign-isolation restrictions are deliberate.

Co-op withdrawal must preserve nondeparting occupants, remove departing participants' agents globally even on another hull, release stations/climbing/bridges, and recover shared authority first when necessary. These are the existing foundation requirements, **not implemented by calling vanilla RemoveShip or by lab stop**. No safe destination means hold/block under current lab policy, not deletion. Boarding retirement, occupied withdrawal, admission and recovery remain deferred; future tests require approved lifecycle and safety implementation before execution.

## Evidence required for a real user-interaction test

Capture before input, during sustained use and after release/timeout:

- Run/process identity, loaded adapter/Coop/Missions MVIDs, installed DLL/content fingerprints, fixture incarnation/mode, elected client/epoch, both ready owners and current blocker.
- Actual active input contexts and bindings, input device, focused/interactable object, visible enabled prompt, scene/UI focus, modal/radial/order/photo/ghost state and runtime behavior/view inventory. Capture the concrete base/override view types, not a claimed feature flag.
- Local main agent GUID/combatant, owner/revision, native controller, active state, formation/captain, assigned ship, actual support, ship/child identity and point eligibility/reservation.
- Action press/release/axis observations with local callback/time; `OnUse`/`OnObjectUsed` and stop callbacks; agent/point/machine occupancy; view's `ControllerMachine`; singleton player ship; actual input destination and stored record. Record the entry route explicitly: **keyboard**, **native spawn-use shortcut**, **scripted SetInput**, or **AI order**.
- Separately, accepted co-op sequence/epoch/owner routing, elected-side input consumption, follower no-integration counters, body active state, actual force deltas, velocity/pose and contact/support. Force-call count alone is not force magnitude or an atomic solver observation.
- Release/neutralization, animation/IK/camera restoration, cleared reservations/extra weapon/gravity attachment as relevant, no stale input after timeout/disconnect, and no damage/campaign/save/result write.

Use screenshots/video as corroboration, not identity or simulation proof. Input acceptance is not physical success. Local synthetic use is not remote multiplayer control. Paired frame messages must retain source/apply/observation ages and superseded/unavailable rows; asynchronous callbacks are not a same-tick cut, synchronized owner snapshot or a recoverable state.

## Targeted two-client scenarios

These are acceptance definitions, **not authorization to deploy or launch a run**. Parent owns reviewed candidate selection, memory gate, isolated profiles, teardown and restoration. Query the live command catalog before using actions; client commands remain read-only.

Captured fixture: scene `battle_terrain_opensea_northern`, hull `nord_medium_ship`; two ships initially at `(250,250,0)` and `(310,250,0)`, five synthetic combatants per ship, no campaign hero origin. With create owner order `testclient1 testclient2`, slot 0 belongs to `testclient1` and slot 1 to `testclient2`; each client's first owned combatant is its main agent, four others are AI and the remote five are native `None` copies. Ship/combatant UUIDs are generated per incarnation; obtain them from inspect, never hard-code prior-run IDs. Election is read from readiness, not assumed from slot order. Source details are provisional snapshots, not a coherent frozen candidate.

Read-only observations available in the captured command source: `coop.debug.players.list` (existing server identity lookup), `coop.debug.naval_lab.inspect` on each process, and `coop.debug.naval_lab.samples 0` on clients for an existing probe (subsequent cursor is the greatest returned sequence). Server has no native controller. Operation receipt lookup is server-only and takes the actual returned operation UUID. `outcomeUncertain` requires looking up that same operation, not sending a fresh mutation.

| Scenario | Concrete action and required evidence | Execution status |
| --- | --- | --- |
| H0 Held-use lifecycle | After approved held-mode startup, owner `testclient1` takes **slot 0** using its own main agent and native spawn-use pair; record identity/occupancy/callbacks, release and verify cleanup. Repeat `testclient2` on **slot 1**, including deadline/stop. Bodies remain held; no propulsion claim. | **Pending writer handoff, independent review and parent-approved native run.** Candidate action names are not accepted runtime commands yet. |
| H1 Actual keyboard helm | On each owner, approach its own point, verify F prompt, press remapped Action, use movement axes and release Action. Confirm correct helm/view/singleton destination, forwarded input and camera cleanup. Test UI/modal gating and no wrong-slot control. | **Not executable as an accepted lab case.** Requires reviewed view/override/input routing. H0 does not satisfy it. |
| H2 Remote owner versus simulator | If election is `testclient2`, `testclient1` uses slot 0 while `testclient2` remains shared simulator; if election reverses, invert remote test. Prove original owner input reaches elected side without remote puppet promotion or follower forces. | **Not accepted.** Scripted helm dispatch tests do not prove user interaction. |
| P1 Propulsion and moving deck | After an independently resolved active-body lifecycle, drive slot 1 from its owner, walk/jump both owned main agents on supported decks, then release. Observe effective oars/wind/rudder, host-only forces, follower support and identity. | **Blocked by unresolved activation/contact.** Do not repeat exhausted enable/disable vector or bypass its guard. |
| C1 Crew/sails/camera | In a separately approved player-input fixture, cycle X allocation, Z sails and C camera; measure station occupancy, actuator settings and cleanup. Repeat blocked feature states; distinguish X with/without bridge. | **Future, not executable with accepted action set.** |
| B1 Boarding/net/withdrawal | Future approved compatible two-hull placement: test boarding cancellation within enemy focus and loss of enemy focus beyond 100 m despite the 300 m cancellation predicate; distinguish friendly 350 m focus from 300 m cancellation eligibility. Board/cross both ways and return via net. Before cutting via helm or station, inspect all connections and occupants affected by the ship-wide cut-loose order, not just the selected empty bridge; instrument same-invocation disconnect/destruction with no assumed evacuation frame. Separately test foreign occupant evacuation and blocked capacity before participant departure, completing required evacuation before disconnect. `testclient2`'s agent on slot 0 must remain its agent. | **Future, not executable.** Current friendly, 60 m fixture is not a ready native enemy-boarding test. No invented bridge/withdrawal commands. |
| S1 Stop/departure | Parent routes existing stop or controlled departure; inspect surviving owner for cleared use, held authority and no stale input; obtain both teardown receipts when available. | Stop exists; native held-use cleanup still requires H0 evidence. Managed departure coverage is not occupied-withdrawal/recovery acceptance. |

Managed tests in `WarSailsE2ETestMatrix.md` can prove routing, identity, duplicate/deadline handling and held authority transitions with mock adapters. They cannot prove focus/prompts, native callbacks, animation/IK, input view composition, physics, contact, weapon projectiles or bridge/native teardown. This documentation investigation ran no builds, tests or games.

## Required regression checklist before adding any lab action

- [ ] **Action binding and view:** identify exact input action/context, verified default and remapping; enumerate live view/override/category registration and UI gating. Do not install only a base view and claim complete controls.
- [ ] **Callbacks:** identify initialization/deployment/use/stop consumers and record their delivery in order. Do not substitute an `IsDeploymentFinished` flag or late subscriber for notifications.
- [ ] **Actor and station identity:** match main agent, original/current owner and revision, native controller, formation captain separately, ship generation/child slot and actual point eligibility.
- [ ] **Occupation and cleanup:** preflight existing user/reserved AI, use native entry points and pairing, observe occupancy, release through normal callbacks; verify animation/IK, camera, extra weapon and attachment/gravity cleanup. For either cut-loose input route, inspect all affected connections and occupants, including station-triggered ship-wide orders. Complete required evacuation before disconnect; instrument transition/destruction/support removal because destruction can occur in the same invocation with no guaranteed intervening frame.
- [ ] **Actual input path:** label keyboard, scripted native use, injected controller record and AI order separately; prove the source action reached the intended destination, not just `SetInput` or an accepted receipt. For boarding, separate the 50 m start / 300 m cancellation predicates from earlier 100 m enemy / 350 m friendly focus bounds; test reachable cancellation and loss of enemy focus beyond 100 m.
- [ ] **Simulator/follower contract:** preserve elected client/epoch and per-agent authority; local pilot/captain permission never silently enables follower integration. Resolve singleton/view targeting before two-player input tests.
- [ ] **Activation and contact measured separately:** record active-body and force outcomes, collision/navmesh/support, actual locomotion and actuator effectiveness. No wake claim from holding use or a body flag; no same-tick claim from asynchronous callbacks.
- [ ] **Stop, timeout and disconnect:** neutralize or end owned controls, clear occupation/reservations, stop frame publication as required, reject stale input and preserve support until safe teardown. Test stalled callback limits honestly.
- [ ] **Managed versus native acceptance:** list which assertions are mock-only and which need native evidence. Preserve logs before restart; blocked/unavailable/superseded observations never count as passed.
- [ ] **Isolation and scope:** no campaign ship/roster/result/save writes; no production admission, combat, capture, occupied removal or recovery inferred from a bounded local action.

## Source evidence appendix

### Installed content identity and reproduction

All listed files reported **assembly version 1.0.0.0 and file version 1.0.0.0** on 2026-09-10, so the SHA-256 matters more than the version label. Paths below are relative to the installed `mb2` root. Hashes were re-read during this investigation; core/NavalDLC hashes match the supplied prior helm evidence.

| Installed DLL | SHA-256 |
| --- | --- |
| `Modules/NavalDLC/bin/Win64_Shipping_Client/NavalDLC.dll` | `146e16de9ac242ef63047cc337c858ffaf24a7b153f7bd7e18f1ccca1d268a94` |
| `Modules/NavalDLC/bin/Win64_Shipping_Client/NavalDLC.View.dll` | `4d074b0558467b534e06aa132c5b9ade0c0e431007a42a754fe61e39674fab8b` |
| `Modules/NavalDLC/bin/Win64_Shipping_Client/NavalDLC.GauntletUI.dll` | `f4f783adb2b3d905abd186e18f24c7293f23979c575e664c5b1daf60b6680547` |
| `Modules/NavalDLC/bin/Win64_Shipping_Client/NavalDLC.ViewModelCollection.dll` | `ef993b21bf6e408d5e6d223f499bc60690a14b5e58fcf781a18a7c53aea4309b` |
| `bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll` | `19387f31557ff840d14f378f6bbdf1d58fcff406ab9ff2294dfbb3e49a50b87e` |
| `Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll` | `ffaac5ed867cd299f8c14f58ffee64f86e50d8b197db5f966759524c3e574ba1` |
| `bin/Win64_Shipping_Client/TaleWorlds.InputSystem.dll` | `9551e6fdaf8f003e6da24e8399ad6a5a5cef38a724e1f5b889e2399954572d95` |

Decompile skill was read. Installed ILSpy CLI 10.1.0.8386 was used, with the installed core binary directory as reference path for module types. No game files were modified. Evidence was copied/freshly decompiled into `%TEMP%/warsails-controls-document/`; `installed-dlls.json` stores metadata. Scratch is not a build/document dependency: regenerate the exact types/methods below from these DLLs after an update. Line numbers refer to these captured decompilations, not source/PDB lines and not older reports with different decompiler output.

### Durable native owners

| Assembly / exact type | Methods and captured evidence |
| --- | --- |
| MountAndBlade: `TaleWorlds.MountAndBlade.CombatHotKeyCategory` | `RegisterGameKeys`, `CombatHotKeyCategory.cs:140-164`: Action, Jump, Crouch, Attack, camera and walk defaults. |
| MountAndBlade: `TaleWorlds.MountAndBlade.GenericGameKeyContext` | `RegisterGameKeys`, `GenericGameKeyContext.cs:44-59`: movement defaults, axes, Leave. |
| NavalDLC: `NavalDLC.HotKeyCategories.NavalShipControlsHotKeyCategory` | Constructor, `NavalShipControlsHotKeyCategory.cs:25-39`: copied axes and six naval action/default registrations. |
| MountAndBlade.View: `TaleWorlds.MountAndBlade.View.MissionViews.MissionMainAgentController` | `OnPreMissionTick` 203-239; `ControlTick` jump 475-479, movement 630, walk/water 719-733, in `MissionMainAgentController.cs`. |
| MountAndBlade.View: `TaleWorlds.MountAndBlade.View.MissionViews.MissionMainAgentInteractionComponent` | `FocusTick`, `FocusStateCheckTick`, `MissionMainAgentInteractionComponent.cs:318-345` for input/use dispatch. |
| MountAndBlade: `TaleWorlds.MountAndBlade.Agent` | `StopUsingGameObjectAux`/`HandleStopUsingAction`/`HandleStartUsingAction`, `Agent.cs:3946-4052`; `UseGameObject` 4170-4204. |
| MountAndBlade: `TaleWorlds.MountAndBlade.UsableMissionObject`, `.StandingPoint`, `.UsableMachine` | Eligibility/use/stop: `UsableMissionObject.cs:250-257,360-415`; `StandingPoint.cs:163-180,269-296,418-443`; pilot selection/property `UsableMachine.cs:83,382-390`. |
| MountAndBlade: `TaleWorlds.MountAndBlade.Mission` | `OnObjectUsed`/`OnObjectStoppedBeingUsed`, `Mission.cs:5035-5050`; `OnDeploymentFinished` 6740-6756. |
| NavalDLC: `NavalDLC.Missions.Objects.UsableMachines.ShipControllerMachine` | `OnDeploymentFinished`/component setup 221-239; `OnPilotAssignedDuringSpawn` 242-251; `OnTick`/capture/description 254-397 in `ShipControllerMachine.cs`. |
| NavalDLC: `NavalDLC.Missions.MissionLogics.NavalShipAgents` | `AssignAndTeleportCrewToShipMachines`, `NavalShipAgents.cs:426-486`: use-before-spawn-callback and broader crew placement. |
| NavalDLC: `NavalDLC.Missions.MissionLogics.NavalAgentsLogic`, `.NavalTeamAgents` | Captain assignment: `NavalAgentsLogic.cs:399-416`; `NavalTeamAgents.cs:298-327,771-777`. |
| NavalDLC: `NavalDLC.Missions.Objects.MissionShip` | Captain/IsPlayerShip 339/367; `OnDeploymentFinished` 609-637; `SetController` 680-699; `OnTick` 1907-1930; `OnParallelFixedTick` 2021-2027; `UpdateController` 2554-2605; ranged control/fire 3259-3289, in `MissionShip.cs`. |
| NavalDLC: `NavalDLC.Missions.MissionLogics.NavalShipsLogic` | `OnDeploymentFinished` 185-191; `OnShipControllerChanged` 242-253; deployment-mode setter 335-351, in `NavalShipsLogic.cs`. |
| NavalDLC.View: `NavalDLC.View.MissionViews.MissionShipControlView` | Initialization/use/stop 96-151; row/rudder/axis/sail 172-289; camera 291-395, in `MissionShipControlView.cs`. |
| NavalDLC.GauntletUI: `NavalDLC.GauntletUI.MissionViews.MissionGauntletShipControlView` | Override 25; screen setup 74-100; screen tick 141-184; `TickInput` 209-297; focus selection/bounds 307-373; prompts 489-538; `GetCan*`/`GetIs*` gates 540-672, in `MissionGauntletShipControlView.cs`. |
| NavalDLC.View: `NavalDLC.View.NavalViews`; MountAndBlade.View: `TaleWorlds.MountAndBlade.View.ViewCreatorManager` | `NavalViews.OpenNavalBattleMission`, `NavalViews.cs:24-66`; named/override/default resolution `ViewCreatorManager.cs:114-139,170-249`. NavalDLC.View's `TaleWorlds.MountAndBlade.View.NavalViewCreator.CreateMissionShipControlView`, `NavalViewCreator.cs:30-33`, uses the override-aware factory. |
| NavalDLC: `NavalDLC.Missions.ShipControl.PlayerShipController`, `NavalDLC.Missions.ShipInput.ShipInputProcessor`, `.ShipInputExtensions` | Stored input `PlayerShipController.cs:16-24`; actuator translation `ShipInputProcessor.cs:31-89`; sail cycling `ShipInputExtensions.cs:33-98`. |
| NavalDLC: `NavalDLC.Missions.ShipActuators.ShipActuators`, `NavalDLC.Missions.NavalPhysics.NavalPhysics` | Actuator tick 210-231; `FixedUpdateRowers` 718-922, used-oar count 1093-1112 in `ShipActuators.cs`; active-body force gates `NavalPhysics.cs:435-476`. |
| NavalDLC: `NavalDLC.Missions.Objects.UsableMachines.ShipOarMachine` | `IsFocusable`, `OnInit`, `OnDeploymentFinished`, `OnPilotAssignedDuringSpawn`, `OnTickParallel2`, ramp registration/deregistration; `ShipOarMachine.cs:71-112,155-204,215` onward. |
| NavalDLC: `NavalDLC.Missions.AgentNavalComponent` | `GetSteppedCombinedShipIsland` 154-169; water/support observation 171-222; `OnTick` 224-254; drowning 327-350, in `AgentNavalComponent.cs`. |
| MountAndBlade: `TaleWorlds.MountAndBlade.Objects.Usables.ClimbingMachine`; NavalDLC: `NavalDLC.Missions.Objects.UsableMachines.StandingPointWithSwimmingLimit` | Climbing prompt/init/use/deployment/tick `ClimbingMachine.cs:37-161`; water eligibility `StandingPointWithSwimmingLimit.cs:7-14`. |
| MountAndBlade: `TaleWorlds.MountAndBlade.RangedSiegeWeapon` | State/ammo initialization and tagged stations 529-595; `OnTick` 992-1029; `Shoot` 1663-1677; projectile path 1711-1748; `SetPlayerForceUse` 2036 onward, in `RangedSiegeWeapon.cs`. |
| NavalDLC: `NavalDLC.Missions.ShipOrder` | Oar allocation 417-500; `SetCutLoose` 578-628; `SetBoardingTargetShip` 644-698; retreat 716 onward, in `ShipOrder.cs`. |
| NavalDLC: `NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine` | `ConnectWithAttachmentPointMachine` 2916-2938; deployment/tick/disconnect 3013-3186; candidate geometry 3350-3384; prompts 3396-3410, in `ShipAttachmentMachine.cs`. Nested `ShipAttachment` state/visibility 936-945,1016-1058 and broken-state tick return 1074-1084 explain immediate transition cleanup before same-invocation destruction. Nested `ShipAttachment`/`ShipBridge` own constraint/navmesh state; this investigation is not a complete recovery inventory. |
| NavalDLC: `TaleWorlds.MountAndBlade.ShipRetreatLogic` | `OnBehaviorInitialize`, `OnDeploymentFinished`, `OnMissionTick`, `ShipRetreatLogic.cs`: timer, boundary and removal path. |
| NavalDLC: `NavalDLC.Missions.NavalMissions`, `TaleWorlds.MountAndBlade.NavalBattleEndLogic` | Campaign-dependent `OpenNavalBattleMission`, `NavalMissions.cs:37-112`; captured-ship writes in `NavalBattleEndLogic.OnMissionEnd`, `NavalBattleEndLogic.cs:269-310`. |

### Mutable repository evidence and limitations

Authorized remote: `Bannerlord-Coop-Team/BannerlordCoop`; branch `feature/warsails-battle-foundation`, HEAD `01b429fe04211f65585e2ab615ac224f47969581`. Another writer was actively changing held-use code. Files were captured **before line-based reliance** under scratch `source/`; do not treat the captures as one buildable candidate or cite these line numbers against the eventual branch without checking again.

| Repository file / captured name | Evidence / SHA-256 of capture |
| --- | --- |
| `source/Missions.Naval/NavalMissionAdapter.cs` / `source/NavalMissionAdapter.cs` | Open/view list and direct input 49-87; fixture/owners 186-240. `a4c417fb7080ec4e1dee77ae8044384b81c2a8372df13b80f14fc9b6428a268c` |
| `source/Missions/Battles/NavalMissionAdapter.cs` / `source/NavalMissionAdapter-contract.cs` | Manifest/mode/interface, scene/hull/crew constants 9-43. `fa3008ef8c6e644080499536a94b75ebbcc7468be6db4e3170f5138ebfacbb4b` |
| `source/Missions/Battles/NavalLabCommands.cs` | Provisional create mode/action names and read-only observations. `4be6df527422200e9288e22b3e3e483ee4dce35031752d79596a02238ef0f40d` |
| `source/GameInterface/Services/MapEvents/Handlers/BattleMissionStartHandler.cs` | Both production denial paths 160-165 and 727-731. `62a27f81e002b07e0f8b92a7bcc647d80beead9d61ebe8e105244d05b17d97fc` |

The initial native helm investigation and activation-vector runtime report were also read; their load-bearing conclusions and exact run identity are included above so this document does not depend on temporary report links. Preserved supporting directories are `%TEMP%/warsails-actual-helm`, `%TEMP%/warsails-helm-20260910/native` and `%TEMP%/warsails-live-20260910-activation-vector`. No new native run, public API inverse, follower-contact solution or production naval enablement is claimed.
