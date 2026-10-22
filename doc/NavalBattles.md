# Naval Battles

The one page to read before touching co-op naval battles (War Sails). It covers the mission side only: hulls, crews,
departures and the end of the battle. Campaign ships, ports, storms, rafts and loot are Jordan's (`Coop.Naval`,
`GameInterface/Services/Ships`) and only appear here as dependencies.

## 1. Model

### Ownership is the party

- Every hull in the mission belongs to the party that fielded it. The player who owns the party simulates the hull: its
  physics, helm, crew AI, reserve queue and weapon. That client is the hull's authority (`NetworkShipInfo.CurrentAuthority`,
  `Missions/Battles/NetworkShipRegistry.cs`).
- `OriginalOwner` is the player who fielded it. `IsNpcParty` marks an AI party's hull.
- Every client spawns its own hulls through vanilla team setup. They register on spawn and are announced to the others at
  the first deployment commit (`NetworkSpawnBattleShips`).
- Every other owner's hull is a copy on this client: spawned from the record with its dynamic body, no controller, and
  driven only by its owner's streams (D3 option B). It replays the owner's helm input, and `NavalSteeredCopies`
  (`Missions.Naval`) steers it every fixed physics step toward the owner's latest sample extrapolated by the owner's
  velocity and yaw rate for the sample's age on this clock (at most 0.3 s): target velocity = owner velocity + clamped,
  soft-zoned correction, blended in with a time constant. The default lead past the age is 0: the age already covers
  the time since the sample arrived, transit on a LAN is about 0, and R42 measured the old 0.1 s lead as pure overshoot
  (copies ran speed x 0.1 s ahead of their owners). No clean one-way transit time exists on the client (samples ride the
  reliable channel, direct or relayed through the server, whose owner-to-server hop this client can't see), so
  `copy_steer lead=` stays the tuning knob. Steering reads the body's physics pose (`GetBodyWorldTransform`), as
  vanilla's fixed-tick code does. A large error or a jump between samples queues a snap that the next mission tick
  applies, outside the physics step: a teleport to the extrapolated position and heading (`NavalShipsLogic.TeleportShip`,
  which carries the crew), along with any other agent standing on the copy, such as a local main agent that boarded it,
  then the owner's velocity and yaw rate.
  Heave, pitch and roll come from this client's water, which can differ from the owner's (see "Copies float on local
  water" in section 6). One rule decides a copy's mode at every sample and step: steered
  while its body simulates, its owner's frames written otherwise (warning `has no simulating body` once per copy;
  `bodyMode` in `inspect`). `NavalSteeredCopies.Track`/`Forget` are the only writers of `NavalForeignHulls`, the static
  copy set the patches read; spawning, releasing, replacing and removing a hull all go through them.
- A copy collides with this client's own hulls, but its owner stays the authority of everything that happens to it:
  contact and ram damage on a copy is routed to the owner (`BattleShipDamageRouter`). Two copies never touch here
  (`CopyPairCollisionFilterPatch`, a prefix on `MissionShip.CanPhysicsCollideBetweenTwoEntities`), since no client
  simulates either; each owner resolves its hull against the other's copy.

### Streams

- `BattleShipReplicator` sends each own hull's frame, body velocity and yaw rate, helm input and rope state every 0.05 s
  (20 Hz, `SendIntervalSeconds`; a long frame's overshoot carries into the next interval, capped at one, so heavy frames
  keep the rate without a burst). Each accepted sample goes straight to the engine as the copy's steering target. A
  stream counts as expired after 1 s without samples on the receiver's clock, and the steering stops feeding the
  owner's velocity forward after the same time.
- `BattleShipConditionStream` sends a hull's damage state (HP, sails, fire, sinking) and its fieldable reserve whenever
  either changes. A late joiner gets the last one on entry.
- `BattleShipWeaponReplicator` sends a ship weapon's aim and ammo state at 10 Hz plus each shot. A copy replays the shot
  with a projectile that deals no damage.
- `AgentStationUseReplicator` sends seat and release events (helm, oars, weapon). Puppets are seated on copies the way the
  owner seated them. A dropped item on a deck is no station: its pickup goes through `WeaponPickupHandler`, the use and
  release patches never publish it, `NavalShipEngine` neither describes nor resolves it as a station, and a seat is only
  re-seated while its point is still a station of its hull (R41a-2 W2). A deactivated machine is still seated, since its
  state is recomputed locally and can lag the owner's. A use published on an agent tick worker is announced on the game
  thread.
- A hit on a hull that another client simulates is routed to that owner, which applies it once through the vanilla damage
  method (`BattleShipDamageRouter`).
- An on-foot agent on a registered hull sends its pose hull-local (`NavalDeckPoses`). Vanilla's on-ship check has no
  distance (`MissionShip.GetIsAgentOnShip`) and `SteppedShip` latches through the jump-off cooldown, so a pose more than
  15 m outside the hull's physics box (`NavalShipEngine.TryGetFootprint`, `PhysicsBoundingBoxWithoutChildren`) goes as a
  world pose instead (R43: a corsair 470 m off its deck ship). A deck puppet out of walking reach teleports at most once
  per owner update and walks the rest (R43: 2034 teleports of one puppet).
- Only an agent's owner drowns it. Vanilla's 5 s breath check (`AgentNavalComponent.CheckAgentDrown`) queues a 10 damage
  self-blow (`Mission.MissionTickAction.RegisterDrownBlow`). `NavalPuppetDrowningPatch` skips it on a puppet, and
  `BattleBlowInterceptPatch` never routes a puppet's self-blow, so a non-owner's pose under water can't drown the real
  agent a second time (R43: 10 routed self-hits killed a pirate).
- Every stream is ordered by `AuthorityStamp` (host epoch, then revision) and drops traffic from a controller that is not
  in the mission.

### AI hulls follow the battle host

- The battle host fields the AI fleet late, once the NPC reserve reached it and its deployment is over
  (`Missions.Naval/NavalNpcFleetSpawner.cs`, enemy side only).
- Only AI hulls have `FollowsBattleHost`. On every host change all clients move them to the new host at the same ordered
  event (`TransferHostHulls`), and their samples carry the host epoch so a stale host's traffic is dropped. A player's hull
  never changes owner: it stays with its player or leaves with it.

### The DLC seam

- `Missions` holds every replicator and binds to no NavalDLC type. Every hull operation goes through
  `INavalShipEngine` (`Missions/Battles/INavalShipEngine.cs`).
- `Missions.Naval` implements it (`NavalShipEngine`), holds the launcher, the end logic and the patches, and registers them
  in `NavalMissionModule`. That module is loaded only while NavalDLC is active.
- `NavalBattleServices` composes the ship services per mission and ticks them in a fixed order: ships, fate reports,
  deck poses, stations, weapons, migration reserve.
- The E2E harness implements the same interface with `FakeNavalShipEngine`, so the real replicators run headless.

### Snapshot ships, not campaign ships

- The mission never spawns the party's campaign `Ship`. `CoopShipSnapshotBuilder` builds a detached copy (same owner, hull,
  pieces and damage) and the mission damages that (`Missions.Naval/CoopShipSnapshotBuilder.cs`).
- `CoopShipSnapshotPatches` keeps snapshot HP writes out of Jordan's campaign ship sync, keeps `DestroyShipAction` from
  running on a snapshot at 0 HP, and grants no Boatswain XP for snapshot damage; it sums the damage the ship's upgrades
  mitigated instead, for the fate report.
- So the mission never writes a campaign ship. What happened to it reaches the campaign only through the ship fates the
  server applies; see section 3.
- Each hull's spawn record carries the object-manager id of the campaign ship it stands for (`CampaignShipId`,
  ProtoMember 19; 18 stays retired), so a copy, and the hull a promoted host swaps in for it, can report that ship.

## 2. Lifecycles

### Deployment hold

- Own hulls are announced at the first deployment commit, not at spawn.
- A copy is held until this client's team setup is over and `NavalShipsLogic.IsDeploymentMode` is false
  (`BattleShipReplicator`, log `Holding foreign hull ... until local deployment ends`). Vanilla's deployment finish pools
  removed hulls and runs over every hull; copies spawned during deployment are the leading suspect for the R38 crash.
- Held records keep the latest state, so a copy spawns at its current frame and condition.
- A hull can't be swapped during deployment either: the swap waits in its first phase until deployment mode is off.

### Host migration swap

- A client that takes a hull over replaces its copy with a freshly spawned simulated hull of the same ship, keeping the
  ship id (`Missions/Battles/NavalHullAuthority.cs`). The swap dates from kinematic copies, whose body could not be
  re-activated, and is live-proven. Follow-up: replace it by a controller switch on the steered copy.
- One phase per mission tick:
  1. `ParkHullAgents`: everyone aboard stops using machines, is lifted clear with physics force-closed and untracked from
     the hull.
  2. `ReplaceHull`: vanilla `RemoveShip`, then `SpawnShip` at the same frame under the AI controller, then the condition
     is carried over and the trajectory planner rebuilt. The registry rebinds the ship id.
  3. `BoardHullAgents`: the parked agents go back to their deck positions on the new hull and vanilla seats the helm and oars.
- If the replace fails or throws, the parked agents go back aboard the old hull. A client that hands a hull away turns it
  back into a steered copy (`ReleaseNpcHull`).
- `coop.debug.naval.reswap` runs a swap without a migration.

### Departure

- **Retreat (Tab, Retreat):** enabled per player. The leaver's mission end removes its hulls and announces each one as
  `NetworkShipRemoved` with `missionEnding`; AI hulls stay with the host.
- **Per-ship Retreat order:** vanilla `ShipRetreatLogic` runs only on the owner and removes the hull at the map edge. Its
  removal is announced without `missionEnding`.
- **Leave:** the same as retreat on the leaver. If its removal never arrives, the server's `MissionPeerLeft` removes its
  hulls on every client (`RemoveHullsOf`).
- **Disconnect (D4):** a leave. The server sends a naval disconnect to the remaining members as `MissionPeerLeft`
  (`ServerMissionMembershipHandler`, `IBattleHostRetentionPolicy.DisconnectWithdraws`), so every client runs the leave
  path above; no client code tells a naval drop apart. The server's own `MissionMemberDeparted` still says it was no
  retreat, and it withdraws the party's reserve (`WithdrawPartyReserves`), so the host never gets it. A disconnected
  host's AI hulls move to its successor with the migration; its own hull leaves.
- **Reconnect:** a late join. The returning player lands in the encounter menu and re-enters through Attack (BR-054); its
  launcher fields fresh hulls from a fresh, casualty-reduced reserve, announced like any late joiner's. Nothing of its old
  hulls comes back.
- **Every removal co-op code makes takes the same path** (`BattleShipReplicator.RemoveHulls`, then
  `NavalHullAuthority.Remove`):
  1. release seats on the hull;
  2. move other owners' agents off its deck to their own hull, or the nearest friendly one, never onto a hull already
     being removed or mid-swap;
  3. next tick, park everyone still aboard;
  4. the tick after, withdraw the departing crew, remove the hull through vanilla `RemoveShip` with its ropes broken, and
     unpark everyone else.
- Which crew withdraws (`DepartingCrew`):
  - `All`: the owner's mission ended with the hull, or the owner left or disconnected. Every crew member but the local
    main agent is deregistered and faded, the way vanilla fades a removed hull's crew.
  - `None`: the owner stays in the battle (per-ship retreat). The owner fades its crew and that reaches peers as routs.
- **Hull removal first, party withdrawal after (R41a-2).** At sea the ship replicator owns a player's departure. On
  `MissionPeerLeft` it reads whether the leaver was the host, queues `RemoveHullsOf` and keeps a pending withdrawal for
  it. Its tick, before the hull step, calls `IBattlePartyWithdrawal.WithdrawParty` once
  `NavalHullAuthority.IsSettledFor` the leaver: no removal of its hulls queued or under way (one in flight from
  `NetworkShipRemoved` included) and no swap holding agents parked, so it runs the tick after the removal finished. So
  the hull removal withdraws the crew, and the party withdrawal only fades what was left off the leaver's hulls, selected
  by ownership across every holder. A re-entry drops the pending withdrawal, which would otherwise fade the returner's
  fresh troops of the same party. `BattleAuthorityMigrator` still does its bookkeeping at once (the provisional
  hand-off, the withdrawn-host mark the successor's adoption reads) and fades the party at once only on land. Before
  this, the migrator subscribed first and faded the crew before the replicator's removal, which then parked and withdrew
  0 of 0 (R41a-2 W2, D, E).

### The removal invariant

When a client removes or replaces a hull:

- nobody stands on its deck or sits at one of its stations;
- afterwards nobody is left parked (physics force-closed) from the removed hull or parked while crew of it, and nobody
  stands on it.

Agents may be parked during the removal itself. The swap holds them for the fresh hull, and a removal unparks the ones
it doesn't withdraw in the same step.

Why it matters: the R33 and R40 native crashes both broke it.

- R33: seated crew were faded on a departing hull.
- R40: the leaver's crew survived the removal of its copy, parked and force-closed.

Neither leaves a managed exception. The game dies in native code. The R38 crash (deployment pooling over copies spawned
too early) is not a removal; the deployment hold covers it (`NavalDeploymentHoldScenarioTests`), not this check.

The E2E harness enforces it on every client of every naval scenario (`FakeNavalShipEngine.AssertRemovalInvariant`,
called after each `NavalPeer.Tick`, before a rejoin replaces a client, and at battle dispose). A test that stands in for
vanilla removing an owner's own hull calls `VanillaOwnerRemoveShipForTest` instead, which is exempt and refuses a copy.

## 3. End of battle

- **Detection is host-only.** `CoopNavalBattleEndLogic` (`Missions.Naval/CoopNavalBattleEndLogic.cs`) runs vanilla
  `NavalBattleEndLogic`, but only the battle host checks the end, and only once both sides fielded troops. That hold is
  latched on every client, so a successor starts released.
- **Tally.** Side depletion and hulls out of action are counted by `NavalSideTally` (`Missions/Battles/NavalSideTally.cs`)
  with vanilla's rules:
  - every live human counts, swimmers included;
  - crew counts only on afloat hulls;
  - reserve is what each owner reported for its hull;
  - a sunk or departed hull's reserve doesn't count.
- **Other clients** end with the host's replicated result. A side counts as retreated only once its players' hulls left
  with them and none is left.
- **Results.** Each client reports the resolved result once per host epoch (`BattleResultCommitter`). The server
  reconciles every mission member, then concludes the map event once. Casualties come from each agent's owner
  (`BattleCasualtyHandler`), since the host's vanilla casualty accounting is off.
- **The host stays once it reported (R41a).** A naval host that reported a resolved result is not replaced when it
  leaves. Its report still concludes the battle once the remaining members match it, and an instance that empties
  without them concludes with it (`BattleCompletionTracker`). Until then the server refuses the departed host's own
  finalize, which would drop the result, and its Attack, which would reset it, and leaves it out of another member's
  mission start (`BattleMissionStartHandler`); the conclusion closes its encounter. These rules live in one place,
  `IBattleHostRetentionPolicy` (`GameInterface/Services/MapEvents/BattleHostRetentionPolicy.cs`). Land still promotes a
  successor that must report again.
- **A missing own reserve ends the mission.** `CoopNavalSetupGuard` checks the reserve in `EarlyStart` and ends the
  mission from its first `OnPreDisplayMissionTick` (an `EndMission` in `EarlyStart` is undone by `Mission.AfterStart`).
  A player team with no troops gets no ship, and a reserve without the player hero (a wounded hero left out of the
  rebuilt reserve, R41a-2 S) has no player troop, so the allocation prefix in `CoopNavalTeamSetupFailurePatches` skips
  vanilla's initial player allocation, which would throw every tick, and has the guard end the mission. The player gets
  a message for the reason (the reserve one only for a missing reserve) and goes back to the map, as in vanilla where a
  wounded hero can't enter a battle.
- **A failed naval team setup ends the mission.** A throw in `DeploymentMissionController.SetupTeams` leaves
  `TeamSetupOver` unset, so vanilla re-runs it every tick and deploys every side again (R41a-2 S: 161 hulls, then a
  native crash). `CoopNavalTeamSetupFailurePatches` marks the guard's `SetupFailed` on the first throw, which ends the
  mission, and skips the deployment controller's tick from then on.
- **Written back (M6).** Vanilla writes naval damage into the campaign ship on every hit and destroys a ship at 0 HP
  (`Ship.OnShipDamaged`). Coop reports it and applies it at fixed points instead:
  - The client that simulates a hull (its owner, or the battle host for AI hulls) reports the campaign ship's fate: HP,
    sail HP, sunk (sinking or 0 HP) and the damage mitigated since its last report (`BattleShipFateReporter`,
    `NetworkBattleShipFates` over the relay). A sinking hull reports at once, other changes at most every 2 s, and a
    removed hull, the mission's end (`NavalShipsLogic.MissionEndEvent`, before `MissionLeft`) and every result report
    (the committer publishes `BattleResultReporting` just before `NetworkBattleResultReady`) flush the rest;
    `NavalBattleServices` wires all three.
  - The server holds each fate (`ServerBattleShipFateHandler`, `IBattleShipFateLedger`) when the sender is in the
    instance and reports its own party's ship, or is the battle host at the current epoch and reports an AI party's ship.
    The newest by host epoch then revision wins, a sunk ship stays sunk, and fates are dropped once the results were
    calculated.
  - `IBattleShipFateApplier` writes them with patches live, never under `AllowedThread`, so every write replicates:
    the owner's Boatswain gets the summed mitigated damage once (vanilla's 0.1 XP per point), then a sunk ship goes
    through `DestroyShipAction` and any other ship takes its battle HP and sail HP. `OnShipDamaged` is not called again.
  - It applies at a member's departure (its party's ships, so a rejoin reads them), when the instance empties or the
    battle finalizes without a commit (everything left), and in `MapEventPatches.Prefix_OnBattleWon` before the results
    and the loot read the ships.
  - A party left without ships gets its raft from vanilla at `MapEventEnded` (Jordan's raft patches run on the server).
  - Clients hold `RefreshMenuOptions` while a mission is current (`SnapshotShipMenuRefreshPatch`), because the applied
    HP reaches players still in the battle; `MapState.OnActivate` refreshes the menu on return.
- **Not written back:**
  - the captured-ship list vanilla `OnMissionEnd` builds (patched off by `CoopNavalShipCapturePatch`); only a disengage
    reads it, so a decisive result already matches vanilla, and disengage parity is M6c;
  - in-mission ship capture (`ShipControllerMachine`), which has its own design.
- **Loot.** Jordan's `LootDefeatedPartyShips` runs after the fates applied, so sunk ships are gone and survivors take
  the after-defeat damage on top of their battle HP, as in vanilla. A player's share is held by `LootShipHold`: it
  leaves the losing party at once (a replicated `Owner = null`, no destroy), so that party's finalize (`RemoveParty`
  destroys its ships) no longer kills ships the loot screen still offers. Taking one (`ApplyByLooting`) and discarding
  the rest (`LootShipsHandler`) work after the finalize. The discard releases the hold; a held ship nobody took is
  destroyed when its looter disconnects, or after a 2 hour fallback for a loot screen that never closes (the ship screen
  comes after the troop and item loot and any conversations).

## 4. Status

### Goals

| Goal | Status | Proven by |
|---|---|---|
| G1 Beat a pirate fleet to a natural end, with ship weapons | Natural end done and live-passed (R40 A, B1, B2). Weapons live-passed in R41a-2 W1 (ballistas for 156 s, no crash). | `vs_pirates`, `sinking`, `fleet`, `migration`, `weapons=n`; `NavalNaturalEndScenarioTests`, `NavalShipWeaponScenarioTests`, `NavalVsPiratesScenarioTests` |
| G2 Ramming and boarding between any two hulls | In progress (M10a): copies are steered dynamic bodies (D3 option B) and collide with own hulls; contact and ram damage route to the owner, and only the rammer's owner runs the ram's damage tick. Not live yet. | `ramming`, `boarding`; `NavalCopySteeringTests`, `NavalDamageRoundTripScenarioTests` |
| G3 Retreat, leave or disconnect without breaking the battle | Retreat and leave live-passed in R41a (A, B, C). Disconnect is a leave (D4); hard drops of a non-host and of the host live-passed in R41a-2 (D, E). Done: the R41a-2 W2 and S fixes and the departure order (parked N of N, then withdrew N) live-passed in R41a-3. | `retreat`, `migration who=nonhost`; `NavalDepartureScenarioTests`, `BattleReserveReconnectScopeTests` |
| G4 Results match vanilla | Done in code (M6), not live yet (R45): battle HP, sail HP, sunk ships and Boatswain XP are written back, and loot ships survive the losing party's finalize. Disengage loot (M6c) and in-mission capture are pending. | `NavalShipFateScenarioTests`, `LootShipHoldTests`, `BattleShipFateReporterTests`, `BattleShipFateLedgerTests` |
| G5 Battles from real campaign play | Presets done (WP4), never live. | `encounter`, `blockade`; preset unit tests |
| G6 AI allies, 3+ players per side | Allies pending (M11): the launcher passes empty ally lists and the spawner fields the enemy side only. 3 players are E2E-covered; live runs had 2 clients. | `allied_npc`; 3-client E2E scenarios |
| G7 Join or rejoin mid-battle shows the true state | Late-join catch-up done (records, condition, weapon, seats); `late_join` live-passed in R39. A rejoin is a late join with a fresh reserve (D4), live-passed in R41a-2 (D, E) and R41a-3 (W2, D, E); a late joiner's dropped host hull record (R41a-2 E) is fixed in E2E, its member hold not hit live yet. | `late_join`, `migration`; `NavalLateJoinScenarioTests`, `NavalDepartureScenarioTests` |

### Milestones

| Milestone | State |
|---|---|
| M1 Jordan merge | done, R35 |
| M2 R34 findings | done |
| M3 E2E harness | done |
| M4, M4b, M4c, M4d presets and fixes | done, R36 to R39 |
| M9 natural end and reserve | done, live in R40 |
| M12 ship weapons | done, live in R41a-2 (W1) |
| Refactors A and B (services, registry lifecycle, authority stamp, hull authority split) | done |
| M8a retreat and leave | done, live in R41a |
| M8b disconnect adoption and hand-back | removed by D4 |
| WP4 probes and `encounter`/`blockade` presets | done, not live |
| M8c withdraw departing crew, `weapons=n` | done, live in R41a (crew withdrawal) |
| Harness removal invariant | done |
| R41a live run | done: A, B, C pass, D fail, E partial |
| D4 disconnect as leave, rejoin as late join, R41a fixes | done, live in R41a-2 (D, E, K) |
| R41a-2 live run | done: W1, D, E, K pass; W2, S fail |
| R41a-2 fixes (pickup stations, player allocation, setup failure, departure order, late-join record, preset) | done, live in R41a-3 (member hold and W2 pickup not hit) |
| R41a-3 live run | done: W2, D, E, S, K, W1 pass |
| R41a retreat, leave and disconnect live validation | done (R41a, R41a-2, R41a-3) |
| R41b probes | done, D3 decided |
| M10a ram and contact (steered copies) | in progress, R42 next |
| M10b plank crossing and melee | pending, needs M10a |
| M6 results parity (fates, Boatswain XP, loot hold) | done in code, R45 next |
| M6c disengage parity | pending |
| M11 allied fleets | pending |

## 5. Live-run log

| Run | Build | Verdict and blocker |
|---|---|---|
| R23 | `8a9450277` | Inconclusive: manual rowing and ropes session got no user input. |
| R24 | `cbddc1a2f` | Pass with notes: copies track 0.1 to 0.4 m, rope throw to bridge on both clients, no plank crossing. |
| R25 | `d5244de2d` | Blocked: AI hulls never fielded on the host. |
| R26 | `49b497d12` | Fielding and peer replication pass; migration partial, hulls froze and crew was lost. |
| R27 | `bbde1340e` | Adoption and handover pass, then the successor's game thread died on a NaN in naval physics. |
| R28 | `45821c20e` | Blocked: successor native crash right after the hull swap. |
| R29 | `c6a9de8a3` | Pass: three-tick hull swap, with and without migration. |
| R30 | `80188ca3c` | Damage and condition pass; sinking gate not drivable; client HitReward NREs. |
| R31 | `4f45f316b` | Stopped twice (GPU out of memory, then on request); no gates. |
| R32 | `965c12969` | Battle end and sink pass; no contact at 9 m; no scoreboard after the win; client2 crashed natively leaving. |
| R33 | `2d01d6e75` | Native crash on the peer when the host left a concluded battle: seated crew faded on the hull copy. |
| R34 | `cec737670` | Pass with low findings: sink, win and lose, migration at epoch 2 with 3 swaps, about 60 fps at 110 to 127 agents. |
| R35 | `4dbb0d6ef` | M1 merge. Ship damage replicates; mission HP not written back, no ship loot, the leaver's hull stays a frozen copy. |
| R36 | `7aaebcaef` | Scenario auto-Ready froze a client (behaviours removed inside `Mission.OnTick`). |
| R37 | `1b83a54f6` | Client NRE on mission open, puppet NRE in a formation without a hull, host crash on a half-spawned puppet, peer ram did no damage. |
| R38 | `53fb62a4a` | Native client crash after a 9-hull deployment with 6 copies spawned while still deploying; the host ram didn't register. |
| R39 | `c923f9527` | Pass: 8 battles, no crash; `late_join` works; `allied_npc` fields no allied hulls; the duel loser's ships vanish (Jordan). |
| R40 | `a9475656d` | Natural ends pass; blocked by two native crashes on the survivor when a player left a concluded battle (crew left parked after the removal). |
| R41a | `6eceafdcc` | A, B, C pass (R40 crashes gone, removal invariant held); D fails, E partial. The preset's disconnect was a graceful leave, so adoption and hand-back never ran; the returner had an empty reserve and hung in deployment (guard's `EndMission` undone by `AfterStart`, then a per-tick NRE on a missing hull); the host left first and the map event was orphaned. The ballista NRE crash found during R41a was fixed in `20b3e4c80`. Led to D4. |
| R41a-2 | `9f643fc36` | W1 (`vs_pirates weapons=1`, ballistas ran 156 s), D (`who=nonhost` hard drop and rejoin), E (hard host drop, migration, rejoin) and K (kept host) pass. W2 fails: the rejoined client crashed natively 15 s in, a deck item pickup replicated as a station use and its puppet re-seated onto the removed item (fixed: pickups are no stations, re-seats need the point still on its hull). S fails: a returner whose wounded hero was left out of its rebuilt reserve threw in vanilla's player allocation every tick, re-running team setup and spawning 161 hulls (fixed: the player allocation in `CoopNavalTeamSetupFailurePatches` needs the player origin, and a failed naval setup ends the mission once). Also fixed after the run: the leaver's crew was faded before its hull removal (party withdrawal now follows the removal), a late joiner dropped the host's hull record that beat its membership (now held), and the `migration` preset dropped the successor too and left an early-pulled returner unready. |
| R41a-3 | `25e1c856d` | Game v1.4.8, NavalDLC v1.2.8. W2, D, E, S, K and W1 pass, no crash. Departure order holds: each departing hull parked N of N, then withdrew N crew; no migrator despawn. S: the heroless returner ends cleanly with `PlayerTeamNotAllocatable`. E stalled 9 min (no weapons, hulls not closing) and needed a forced win. |

## 6. Open decisions and known limits

- **D4, disconnect (decided 2026-10-19): a disconnected player's ship leaves the battle**, exactly like a Leave, and the
  player comes back as a late joiner with fresh hulls and a fresh reserve. Hand-back and player-hull adoption were
  deleted: hand-back could not work live (the returner's reserve is empty after an absence and vanilla assigns no ship
  to a team with no troops) and was never exercised live. Cost: a side whose only hull belonged to the disconnected
  player can lose the battle while the player is away. This differs from land, where the host adopts a disconnected
  player's troops and hands them back (BR-031, BR-033).
- **D3, contact model (decided 2026-10-20): option B, steered copies.** A copy keeps its dynamic body and is steered
  toward the owner's samples, so hulls of different owners touch. R41b measured steered copies at 0.16 to 0.26 m mean
  drift with contact damage routed to the owner and identical HP on both clients; option C (static collider) could not
  be built (`AddPhysics(isStatic)` left no static body). Kinematic and static copies and `copy_body` were deleted.
- **D2.** A failed AI fleet counts as a players' victory (the land precedent); an aborted battle would need a new
  result state.
- **Copies float on local water (decided 2026-10-21, known difference).** Water height is a function of the engine's
  process-wide `MBCommon.GetTotalMissionTime()` (the probe's `total`), which adds up every mission the client has run
  (conversations, earlier battles) and has no managed setter. So clients with different mission histories see different
  wave phases: about 1.5 m mean apart in R42's open-sea battle (`battle_terrain_opensea_southern`), about 0.001 m in
  its coastal one. A copy keeps its local buoyancy: its planar position and yaw follow the owner, while its heave and roll
  differ from the owner's by about the water mismatch (R42: 1-1.5 m heave and 5-6° roll mean, roll max 26°). Drift
  criteria are planar plus yaw only; heave, pitch and roll are reported for information. `WaterQuality` and
  `PhysicsTickRate` are per client too. `water_probe` logs both clocks; align two probes on `total`, not on `mt`. Rejected:
  - follow the owner's heave, roll and pitch with buoyancy off: the copy would visibly hover over or sink into local waves;
  - feed the owner's water plane into the copy's buoyancy: the copy would sit in visibly different water here.
- **Jordan dependencies:**
  - M6 relies on `CampaignShipsSync` replicating a server's `HitPoints`, `SailHitPoints` and `Owner` writes and
    `DestroyShipAction` (E2E-proven);
  - the campaign side of a mid-battle retreat (the party at sea, rafts) is his;
  - ship loot and distribution are his. M6 adds, for his review, one `LootShipHold.Hold` call in
    `MapEventResultsInterface.LootDefeatedPartyShips` (and a constructor parameter); `LootShipsHandler` and
    `PortScreenChangesHandler` needed no change, since the offered ships keep their handles;
  - suggested: his `ShipHitPointsPatches` postfix should skip the refresh during a mission or with a null menu (M6 guards
    it from `SnapshotShipMenuRefreshPatch`);
  - disengage loot in `MapEventResultsInterface` (M6c);
  - AI parties' ships must be registered for their fates to apply (a missing one logs `Failed to get Ship`);
  - the PvP loser's ships vanish (R39).
- **Ship fate limits (M6).** A hard drop loses up to 2 s of HP changes (a sink always goes out at once). The ships of an
  AI party that a non-host player fields with its own team are refused, since only the host may report AI ships. A
  fate that arrives after the results were calculated is dropped.
- **Ram damage has one writer.** A ram exists only on a hull with a bow ram piece (`MissionShip._ram`; the preset hull
  `northern_light_ship` has no bow slot, so use `ram=n`). `RamTickPatch` replaces `MissionShipRam.OnTick` on every
  client with one implementation: only the rammer's owner deals the hit, routed to the target's owner when the target is
  a copy, and every client keeps vanilla's 2 s cooldown, deck breaks, kills and effect. The fixed-tick ram forces stay, so
  each owner's hull is pushed back by the other's copy locally.
- **Ram kills are authority-scoped (`NavalRamKills`).** Every client that sees a ram impact, through the owner's tick
  or a copy's, kills only its own (or un-networked) agents within 2 m: the kill loop skips another owner's puppet, and
  that owner kills it on its own view of the impact and reports it like any death. Every
  client that sees the impact also breaks the target's deck parts within 5 m locally (oars included), which only steers
  the outcome on the hull's owner and is cosmetic elsewhere. A puppet that still dies here without its owner's death is
  logged as a Warning (`Puppet ... died here without a routed blow`); DEBUG counts the spared ones (`ramPuppetsSpared`).
  A client whose copy of the ram misses an impact the owner saw keeps its agents there alive (live-check in R42).
- **Allied fleets.** An AI lord on the players' side fields no hulls. Agents refused by a formation with no hull are not
  re-assigned.
- **Late join and reconnect.** A late joiner gets every record, condition, weapon state and seat; a returning player is
  one. `late_join` ran live in R39 and the rejoin in R41a-2 (D, E) and R41a-3 (W2, D, E); the condition catch-up is
  E2E-only. Not hit live yet: an item pickup on a rejoined client in W2 (pickups by the returner's agents ran in R41a-3
  E with no crash).
- **A hero knocked out earlier in a battle can't rejoin it.** Vanilla `MapEventParty.Update` leaves heroes in
  `_woundedInBattle` or `_diedInBattle` out of the roster, so the returner's rebuilt reserve has no hero and the setup
  guard ends its mission (`PlayerTeamNotAllocatable`, R41a-3 S). This matches vanilla, where a wounded hero can't enter
  a battle. Inferred from the decompile and the troop counts, not traced; the `Left hero` Debug line never fires for it.
- **Open: relayed catch-up bypasses the server's join hold.** The server holds its own sends to a joining peer
  (`CoopServer.Send`), but a peer's catch-up goes through `RelayPacketHandler` (`target.Send`), so a member's hull record
  can reach a joiner before the joiner learns that member is in the mission (R41a-2 E). The replicator now holds such a
  record until the owner is a member (log `Holding hull ... until it is a mission member`) and drops one from a departed
  owner (`Dropped hull record ... of departed ...`). Weapon and seat catch-up may hit the same ordering gap; the relay
  ordering itself is unchanged. The hold is not hit live yet: in R41a-3 the race didn't occur.
- **Follow-up A, late hulls and `MissionShip.OnDeploymentFinished` (partly done, capture P0).** Hulls fielded after
  deployment (AI fleet, copies, swap replacements) now get the weapon AI, the helm's `OnDeploymentFinished` (standing
  point components, enemy helm AI-only, the logics its vacancy check reads) and `CrewSizeOnMainDeck`
  (`LateHullDeploymentPatch`). Still skipped: model ammo (a swap must not refill it), the anchor, wake and flotsam, the
  rope, oar, climbing and ammo barrel machines' standing point setup, and `IsDeployed`. Until capture replicates, the main
  agent can't take an enemy hull's helm (`ForeignHelmUsePatch`) and a capture that completes anyway is refused and logged
  (`CoopShipCaptureRefusalPatch`).
- **Follow-up B, weapon target lists are snapshots.** A late hull is no object target for weapons initialised before it
  spawned; formations still are.
- **Silent member after the host left.** A naval battle whose host reported and left concludes once the remaining
  members match its report or leave; there is no timeout for a member that stays in the mission without reporting.
- **Follow-ups from the D4 review (not done):**
  - a hull's `OriginalOwnerControllerId` (`BattleShipSpawnData`, `NetworkShipInfo.OriginalOwner`) only feeds logs and
    `inspect` now that hand-back is gone;
  - `BattleHostHandler` tracks a controller in four overlapping sets (`PresentControllers`, `AbsentControllers`,
    `HostOwnedOfflineControllers`, `WithdrawnControllers`), which should become one status per controller;
  - `BattleHostHandler` is too big: membership and the health withdrawal should move out into their own services.
- **Follow-ups from the R41a-2 review (not done):**
  - `BattleAuthorityMigrator.DespawnPartyTroops` repeats `BattleTroopWithdrawal.Deregister` per agent; hoist it into the
    withdrawal and inject that;
  - the ship replicator's `departedOwners` is a second membership view next to `IMissionContext`, which could own it;
  - `BattleShipReplicator.cs` (about 1090 lines) and `BattleAuthorityMigrator.cs` (about 860) are too big and should be
    split;
  - the `migration` preset fixes who drops by the first host each client sees, so a client whose mission starts only
    after the host dropped would take the successor for it.
- **Follow-ups from the M10 review (not done):**
  - `NavalScenarioStager` (about 820 lines) should split into encounter, blockade and fleet-outfitter classes behind DI;
  - one staleness constant should serve both the replicator's `SampleLifetimeSeconds` and `CopySteering.StaleSeconds`;
  - `CoopNavalBattleBehavior` should get the engine directly instead of through `INavalBattleServices.Engine`;
  - the DEBUG engage reset tracer should check cheaply whether anything is watched before it resolves;
  - `RamTickPatch` replaces vanilla `MissionShipRam.OnTick` on every client (with `NavalRamKills.ApplyImpact`), so
    re-check the whole replacement against it on every DLC update;
  - the next cut of `NavalShipEngine` is the hull-swap trio (`ParkHullAgents`, `ReplaceHull`, `BoardHullAgents`).
- **Boarding.** Ropes and bridges replicate, but plank crossing and melee on a foreign deck were never seen live, and a
  rope cut from the target side isn't replicated.
- **Sea raids** still open the land raid scene.
- **CI** has no NavalDLC, so release builds ship without `Missions.Naval.dll` (maintainer decision).

## 7. Debug surface

### Commands (`coop.debug.naval.*`)

| Command | Side | What it does |
|---|---|---|
| `scenario <preset> [knobs] [ids]` | server | Stages a preset and starts the battle. Refuses while a listed player is still in a battle mission (leave it and finish the post-battle screens first), and heals each listed player's hero to full. |
| `scenario_status` | client | Preset steps, knobs and `battle.hulls[]` with owner, AI flag, weapon and ram. |
| `inspect` | client | Session, host epoch, hull streams, damage, weapons, decks, stations, AI fleet, end tally, agents, ships; DEBUG adds `copyProbe`: steering knobs, drift (against the owner's estimated current pose, the latest sample moved on by its age without the lead) and correction per copy, `oscillations10s`/`oscillationsTotal` (sign changes of the along-heading position error to the target, low-passed over the steering's `tau`, outside a 0.1 m deadband; the raw error flips at each sample arrival as the target jumps by the owner's frame-time jitter times its speed, R43), `targetJump` (how far the target moved at each sample arrival), steering cost (`steerMeanMs`, `steerP99Ms`, `steerMaxMs`) with `gcCollections10s` per generation, contact counters, the deck puppets' `maxDeckError` (the largest hull-frame puppet-to-target error, skipping a teleport's or a new deck placement's sample, counted in `deckSamplesSkipped`), `rams[]` (`hasRam` per hull) and the ram counters (`ramTicksGatedOnCopies`, `ramPuppetsSpared`); and `ships[].fatesSent`, the campaign ship fates this client sent for the hull (M6). |
| `fleet [party]` | server and client | Lists each ship of a party on this machine: its id, HP, sail HP and owner. `party` is a controller id or a `MobileParty`/`PartyBase` id; without it, the main party on a client and every player's party on the server. Run it before and after a battle on every machine. |
| `win`, `lose` | battle host | Ends the battle as a victory or defeat for the host's side; peers follow. |
| `damage <ship> <amount>`, `sink <ship>` | client | Hits a hull as a ship weapon would; another owner's hull takes it through its owner. |
| `reswap <ship>` | battle host | Replaces an AI hull without a migration. |
| `helm <rudder> <row> <sail> [seconds]` | client | Drives the own hull. |
| `rope_list`, `rope_throw`, `rope_status` | client | Lists, throws and shows ropes. |
| `copy_steer [key=value...]` | client | Shows or tunes the steered copies' knobs here (`gain window softzone clamp tau lead snap`), live. |
| `water_probe start [grid=5] [spacing=40] [seconds=120] \| status \| stop` | client | Samples the water grid for the shared-clock test; each tick logs every hull's `x`, `y`, `yaw` (degrees, counter-clockwise from +x), `z`, `pitch`, `roll` and the water under it. `start` is refused until deployment has finished. |
| `board_copy [ship] \| status` | client | Teleports the main agent onto a copy's deck (the nearest copy, or the one whose ship id starts with `ship`) at vanilla's deck spot: the hull's player standing point, else the middle inner deck frame (`MissionShip.GetMiddleInnerSpawnGlobalFrame`). Reports the hull-frame `deckError` and `heightAboveSpot`; `status` reports the hull the main agent stands on. The copy's owner reports the puppet's `maxDeckError` in `inspect`. |
| `immortal [on \| off]` | client | Sets the main agent's vanilla mortality state to `Immortal` (`on`) or `Mortal` (`off`), so blows, routed ones included, take no health; reports it without an argument. A ram's 2 m kill radius (`Agent.Die`) still kills it. |

`coop.debug.map_event.start_naval` starts a naval battle directly. Everything above except `inspect` and `rope_status` is
DEBUG only.

### Presets

`solo`, `vs_pirates`, `boarding`, `ramming`, `sinking`, `migration`, `late_join`, `fleet`, `duel`, `allied_npc`,
`retreat`, `encounter`, `blockade` (`GameInterface/Services/MapEvents/NavalScenarios/NavalScenarioPresets.cs`).

`encounter` takes scenario players off a raft first (a player released at sea is on one, and `PlayerRaftVisibilityPatches`
turns an engage order on a player's raft into `GoToPoint`; R41b P4), as landing would: the raft ends, the raft's `DisableAi`
is undone with no server-side behavior recalculation, and the fleet cached at the stranding comes back
(`IPlayerRaftFleetInterface.ReturnFleet`) before the top-up. The server re-issues the fleet's engage order whenever
it isn't `EngageParty` on the first player. If there is still no map event after 10 s and the fleet is within
`EncounterModel.NeededMaximumNavalDistanceForEncounteringMobileParty`, the server calls
`EncounterManager.StartPartyEncounter(fleet, player)`, which becomes the server-detected player conversation. Neither runs
once the fleet is held in a player conversation (`ConversationPartyHold.IsInPlayerConversation`) or either party is in
a map event, so the conversation's hold isn't overridden and no second encounter opens. That conversation opens on the
first player before the map event, and so before the scenario message, exists. So the server sends
`NetworkNavalScenarioEngaging` with the engage order, and each listed client answers an `encounter_meeting` conversation
that has stayed open 2 s, as `coop.debug.map_event.complete_encounter_meeting` does, until the scenario message arrives
(R42 needed that command by hand on the engaged client). While the
engage step waits, each distinct stack that resets the fleet's movement is logged once. `scenario_status` reports the
re-issue count and whether the fallback ran.

`blockade` prefers a free lord already at war with the town's and the players' factions. Failing that, it takes the nearest
free lord of a third faction and declares the missing wars with `DeclareWarAction.ApplyByDefault`, logged as
`declaredWar=`. It no longer needs wars set up by hand on a fresh campaign. `town=id` still picks the town.

Knobs (`NavalScenarioResolver.Usage`):

- `hulls=n` AI hulls;
- `hull=id` hull type;
- `spacing=m` boarding or ramming gap;
- `ships=n` ships per player;
- `delay=s` before the scripted step;
- `reconnect=s` for `migration`;
- `who=host|nonhost` who disconnects;
- `drop=hard|leave` for `migration`: `hard` (default) has the server drop the player's connection at once, the real
  disconnect path; `leave` goes to the main menu, which the battle sees as a leave. Who drops is fixed by the first host
  each client sees, so the successor never drops too. A player that reconnects while the battle runs gets the scenario
  again with its disconnect done and re-enters through Attack, or presses Ready if a mission start already pulled it in;
- `town=id` for `blockade`;
- `weapons=n` (0 to 4) arms the first n ships of every player and of the AI fleet with `fore_ballista`, swapping in a Battle
  Knarr where a hull has no fore slot.
- `ram=n` (0 to 4) fits `bow_ram_lvl1` on the first n ships without a ballista of every player and of the AI fleet
  (not the `encounter` or `blockade` enemy), swapping in an `empire_medium_ship` where a hull has no bow slot. `ramming`
  defaults to `ram=1`.

A scenario client whose Ready press throws (R42 crash 2's first symptom) logs the exception, fails the `ready` step with
`deployment half-finished` and runs no further steps in that battle.

### Validation procedure

1. Get the maintainer's go for the run. Build `Coop.csproj` Debug with VS18 MSBuild and the `Deploy.targets` deploy
   (`ModName=Coop`). Record the module and documents snapshot and the assembly MVIDs in the case dir.
2. Start the server and 2 clients through the MCP with the `naval` profile (`doc/automated-testing/mcp-setup.md`). Wait
   for `readyForCampaignTests` on both clients.
3. On the server, run the scenario under test, for example `coop.debug.naval.scenario vs_pirates hulls=3 weapons=1`.
   Check every gate with `inspect`, `scenario_status` and the `[NavalSync]` log lines on each client.
4. After each battle, exit in the order the gate asks, then move on.
   - Leave each battle (`coop.debug.map_event.leave_battle` on each client) and finish the post-battle screens before the
     next scenario; `scenario` refuses while a listed player is still in a mission (R43 staged into a live mission and
     the client never entered the new map event). The server can't see the post-battle screens, so wait for them.
   - `scenario` heals the listed heroes when it stages: a heal sent while a client is still in the old mission is
     overwritten at its exit (`CoopAgentOrigin.OnMissionEnded`, R43: 51/100). A wounded hero gets its party removed
     from the next battle by `BattleMissionStartHandler.RemoveWoundedNonInitiatorParties`, which turns a 2-player
     scenario into a 1-player one (R41a-2 E attempts 1 and 2). `coop.debug.hero.set_hitpoints Hero_Player 100` still
     heals one by hand.
   - To test a kept host's re-entry refusal, use `coop.debug.map_event.start_attack_mission` on that client. The kept host
     has no `PlayerEncounter` after leaving (menu `naval_encounter_disengaged`), so `enter_current_battle` only answers
     "No active encounter" (R41a-2 K).
5. `stop_run`, restore the module and documents from the snapshot and verify them, then write the report with each gate's
   evidence lines.

### ProcDump

- Don't use `-h`. It false-fired while the game was loading, stopped after its dump count, and in R40 didn't fire on a
  3-minute freeze.
- `-e` is refused while the TaleWorlds watchdog is attached. The watchdog takes its own crash dump.
- On a freeze, take `procdump64 -accepteula -ma <pid>` by hand, ideally twice a few seconds apart.
- `dotnet-dump` only shows managed frames. A native crash needs cdb: `cdb -z <dmp> -c "!analyze -v; ~0k; q"`.
