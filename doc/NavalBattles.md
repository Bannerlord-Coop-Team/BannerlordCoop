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
- Every other owner's hull is a copy on this client: spawned from the record, made kinematic
  (`DisableDynamicBodySimulation`), and driven only by its owner's streams. A copy never collides and never simulates.

### Streams

- `BattleShipReplicator` sends each own hull's frame, helm input and rope state every 0.05 s (20 Hz,
  `SendIntervalSeconds`). Copies interpolate to the latest sample and drop samples older than 1 s on the receiver's clock.
- `BattleShipConditionStream` sends a hull's damage state (HP, sails, fire, sinking) and its fieldable reserve whenever
  either changes. A late joiner gets the last one on entry.
- `BattleShipWeaponReplicator` sends a ship weapon's aim and ammo state at 10 Hz plus each shot. A copy replays the shot
  with a projectile that deals no damage.
- `AgentStationUseReplicator` sends seat and release events (helm, oars, weapon). Puppets are seated on copies the way the
  owner seated them.
- A hit on a hull that another client simulates is routed to that owner, which applies it once through the vanilla damage
  method (`BattleShipDamageRouter`).
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
- `NavalBattleServices` composes the ship services per mission and ticks them in a fixed order: ships, deck poses,
  stations, weapons, migration reserve.
- The E2E harness implements the same interface with `FakeNavalShipEngine`, so the real replicators run headless.

### Snapshot ships, not campaign ships

- The mission never spawns the party's campaign `Ship`. `CoopShipSnapshotBuilder` builds a detached copy (same owner, hull,
  pieces and damage) and the mission damages that (`Missions.Naval/CoopShipSnapshotBuilder.cs`).
- `CoopShipSnapshotPatches` keeps snapshot HP writes out of Jordan's campaign ship sync, keeps `DestroyShipAction` from
  running on a snapshot at 0 HP, and grants no Boatswain XP for snapshot damage.
- So nothing that happens to a hull in the mission reaches the campaign ship today; see section 3.

## 2. Lifecycles

### Deployment hold

- Own hulls are announced at the first deployment commit, not at spawn.
- A copy is held until this client's team setup is over and `NavalShipsLogic.IsDeploymentMode` is false
  (`BattleShipReplicator`, log `Holding foreign hull ... until local deployment ends`). Vanilla's deployment finish pools
  removed hulls and runs over every hull; copies spawned during deployment are the leading suspect for the R38 crash.
- Held records keep the latest state, so a copy spawns at its current frame and condition.
- A hull can't be swapped during deployment either: the swap waits in its first phase until deployment mode is off.

### Host migration swap

- A copy's body can't be re-activated (`EnableDynamicBody` does not undo `DisableDynamicBodySimulation`). So a client
  that takes a hull over replaces it with a freshly spawned simulated hull of the same ship, keeping the ship id
  (`Missions/Battles/NavalHullAuthority.cs`).
- One phase per mission tick:
  1. `ParkHullAgents`: everyone aboard stops using machines, is lifted clear with physics force-closed and untracked from
     the hull.
  2. `ReplaceHull`: vanilla `RemoveShip`, then `SpawnShip` at the same frame under the AI controller, then the condition
     is carried over and the trajectory planner rebuilt. The registry rebinds the ship id.
  3. `BoardHullAgents`: the parked agents go back to their deck positions on the new hull and vanilla seats the helm and oars.
- If the replace fails or throws, the parked agents go back aboard the old hull. A client that hands a hull away turns it
  back into a copy (`ReleaseNpcHull`).
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
- **A missing own reserve ends the mission.** `CoopNavalReserveGuard` checks the reserve in `EarlyStart` and ends the
  mission from its first `OnPreDisplayMissionTick` (an `EndMission` in `EarlyStart` is undone by `Mission.AfterStart`).
  A player team with no troops gets no ship, and a reserve without the player hero (a wounded hero left out of the
  rebuilt reserve, R41a-2 S) has no player troop, so `CoopNavalPlayerTeamAllocationPatch` skips vanilla's initial player
  allocation, which would throw every tick, and has the guard end the mission. The player sees the guard's message and
  goes back to the map, as in vanilla where a wounded hero can't enter a battle.
- **A failed naval team setup ends the mission.** A throw in `DeploymentMissionController.SetupTeams` leaves
  `TeamSetupOver` unset, so vanilla re-runs it every tick and deploys every side again (R41a-2 S: 161 hulls, then a
  native crash). `CoopNavalTeamSetupFailurePatches` marks the guard's `SetupFailed` on the first throw, which ends the
  mission, and skips the deployment controller's tick from then on.
- **Not written back:**
  - a hull's final HP and sunk state (snapshots are mission-local);
  - the captured-ship list vanilla `OnMissionEnd` builds (patched off by `CoopNavalShipCapturePatch`, because mission
    ships are snapshots);
  - Boatswain XP.
- **Loot.** Ship loot after the battle is Jordan's `LootDefeatedPartyShips`, which doesn't know what happened in the
  mission. The write-back and loot parity are M6.

## 4. Status

### Goals

| Goal | Status | Proven by |
|---|---|---|
| G1 Beat a pirate fleet to a natural end, with ship weapons | Natural end done and live-passed (R40 A, B1, B2). Weapons done in code, never live: R40 hulls carried none. | `vs_pirates`, `sinking`, `fleet`, `migration`, `weapons=n`; `NavalNaturalEndScenarioTests`, `NavalShipWeaponScenarioTests`, `NavalVsPiratesScenarioTests` |
| G2 Ramming and boarding between any two hulls | Not started. Probes in code (copy_body, contact counters); peer ram does no damage (R37), copies never collide. | `ramming`, `boarding`; no E2E |
| G3 Retreat, leave or disconnect without breaking the battle | Retreat and leave live-passed in R41a (A, B, C). Disconnect is a leave (D4), done in E2E; the hard-drop `migration` run is pending. | `retreat`, `migration who=nonhost`; `NavalDepartureScenarioTests`, `BattleReserveReconnectScopeTests` |
| G4 Results match vanilla | Pending (M6). HP, sunk ships and captured ships are not written back. | none yet |
| G5 Battles from real campaign play | Presets done (WP4), never live. | `encounter`, `blockade`; preset unit tests |
| G6 AI allies, 3+ players per side | Allies pending (M11): the launcher passes empty ally lists and the spawner fields the enemy side only. 3 players are E2E-covered; live runs had 2 clients. | `allied_npc`; 3-client E2E scenarios |
| G7 Join or rejoin mid-battle shows the true state | Late-join catch-up done (records, condition, weapon, seats); `late_join` live-passed in R39. A rejoin is a late join with a fresh reserve (D4); R41a hung it in deployment, fixed in E2E, not live-run yet. | `late_join`, `migration`; `NavalLateJoinScenarioTests`, `NavalDepartureScenarioTests` |

### Milestones

| Milestone | State |
|---|---|
| M1 Jordan merge | done, R35 |
| M2 R34 findings | done |
| M3 E2E harness | done |
| M4, M4b, M4c, M4d presets and fixes | done, R36 to R39 |
| M9 natural end and reserve | done, live in R40 |
| M12 ship weapons | done, not live |
| Refactors A and B (services, registry lifecycle, authority stamp, hull authority split) | done |
| M8a retreat and leave | done, live in R41a |
| M8b disconnect adoption and hand-back | removed by D4 |
| WP4 probes and `encounter`/`blockade` presets | done, not live |
| M8c withdraw departing crew, `weapons=n` | done, live in R41a (crew withdrawal) |
| Harness removal invariant | done |
| R41a live run | done: A, B, C pass, D fail, E partial |
| D4 disconnect as leave, rejoin as late join, R41a fixes | done in E2E, not live |
| M7 contact probe (live), M10a ram and contact, M10b plank crossing and melee | pending, needs D3 |
| M6 results parity | pending |
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

## 6. Open decisions and known limits

- **D4, disconnect (decided 2026-10-19): a disconnected player's ship leaves the battle**, exactly like a Leave, and the
  player comes back as a late joiner with fresh hulls and a fresh reserve. Hand-back and player-hull adoption were
  deleted: hand-back could not work live (the returner's reserve is empty after an absence and vanilla assigns no ship
  to a team with no troops) and was never exercised live. Cost: a side whose only hull belonged to the disconnected
  player can lose the battle while the player is away. This differs from land, where the host adopts a disconnected
  player's troops and hands them back (BR-031, BR-033).
- **D3, contact model.** Ramming between owners needs contact with a copy. Option A keeps the copy kinematic and only
  routes ram damage. Option B (`copy_body steered`) keeps the copy's dynamic body and steers it toward the owner's
  samples. Option C (`copy_body static`) gives it a moveable static collider. The pick waits on the R41 `copy_body`
  probe data; helm capture is part of D3.
- **D2.** A failed AI fleet counts as a players' victory (the land precedent); an aborted battle would need a new
  result state.
- **Water is not shared.** The water clock is native and can't be set from managed code, and `WaterQuality` and
  `PhysicsTickRate` are per client. So the verdict is no-go on deterministic water. Option B has to treat each client's
  sea as a disturbance. `water_probe` checks whether the clock happens to be shared.
- **Jordan dependencies:**
  - M6 needs `CampaignShipsSync` to replicate a server `Ship.HitPoints` write and `DestroyShipAction`;
  - the campaign side of a mid-battle retreat (the party at sea, rafts) is his;
  - ship loot and distribution are his;
  - the PvP loser's ships vanish (R39).
- **Ramming is absent across owners.** Copies don't collide, so only two hulls simulated on the same client (the
  battle host's own hull and the AI hulls it simulates) can ram each other.
- **Allied fleets.** An AI lord on the players' side fields no hulls. Agents refused by a formation with no hull are not
  re-assigned.
- **Late join and reconnect.** A late joiner gets every record, condition, weapon state and seat; a returning player is
  one. Only `late_join` ran live (R39); the condition catch-up and the rejoin are E2E-only.
- **Follow-up A, late hulls skip `MissionShip.OnDeploymentFinished`.** Hulls fielded after deployment (mainly the AI
  fleet) miss its standing points, `SetUsableByAIOnly` on enemy helms, `_navalShipsLogic` (needed for capture,
  `ShipControllerMachine`), model ammo, wake and flotsam, and `IsDeployed`. Fix: call `ship.OnDeploymentFinished()` in
  `NavalNpcFleetSpawner.SpawnAssignedHulls` when `!hull.IsDeployed` (not for `ReplaceHull`, which would refill ammo).
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
- **Boarding.** Ropes and bridges replicate, but plank crossing and melee on a foreign deck were never seen live, and a
  rope cut from the target side isn't replicated.
- **Sea raids** still open the land raid scene.
- **CI** has no NavalDLC, so release builds ship without `Missions.Naval.dll` (maintainer decision).

## 7. Debug surface

### Commands (`coop.debug.naval.*`)

| Command | Side | What it does |
|---|---|---|
| `scenario <preset> [knobs] [ids]` | server | Stages a preset and starts the battle. |
| `scenario_status` | client | Preset steps, knobs and `battle.hulls[]` with owner, AI flag and weapon. |
| `inspect` | client | Session, host epoch, hull streams, damage, weapons, decks, stations, AI fleet, end tally, agents, ships; DEBUG adds copy body modes, drift and contact counters. |
| `win`, `lose` | battle host | Ends the battle as a victory or defeat for the host's side; peers follow. |
| `damage <ship> <amount>`, `sink <ship>` | client | Hits a hull as a ship weapon would; another owner's hull takes it through its owner. |
| `reswap <ship>` | battle host | Replaces an AI hull without a migration. |
| `helm <rudder> <row> <sail> [seconds]` | client | Drives the own hull. |
| `rope_list`, `rope_throw`, `rope_status` | client | Lists, throws and shows ropes. |
| `copy_body <ship\|all> kinematic\|steered\|static [knobs]` | client | Switches how copies exist here (D3 options A, B, C). |
| `water_probe start [grid=5] [spacing=40] [seconds=120] \| status \| stop` | client | Samples the water grid for the shared-clock test. |

`coop.debug.map_event.start_naval` starts a naval battle directly. Everything above except `inspect` and `rope_status` is
DEBUG only.

### Presets

`solo`, `vs_pirates`, `boarding`, `ramming`, `sinking`, `migration`, `late_join`, `fleet`, `duel`, `allied_npc`,
`retreat`, `encounter`, `blockade` (`GameInterface/Services/MapEvents/NavalScenarios/NavalScenarioPresets.cs`).

Knobs (`NavalScenarioResolver.Usage`):

- `hulls=n` AI hulls;
- `hull=id` hull type;
- `spacing=m` boarding or ramming gap;
- `ships=n` ships per player;
- `delay=s` before the scripted step;
- `reconnect=s` for `migration`;
- `who=host|nonhost` who disconnects;
- `drop=hard|leave` for `migration`: `hard` (default) has the server drop the player's connection at once, the real
  disconnect path; `leave` goes to the main menu, which the battle sees as a leave. A player that reconnects while the
  battle runs gets the scenario again with its disconnect done and re-enters through Attack;
- `town=id` for `blockade`;
- `weapons=n` (0 to 4) arms the first n ships of every player and of the AI fleet with `fore_ballista`, swapping in a Battle
  Knarr where a hull has no fore slot.

### Validation procedure

1. Get the maintainer's go for the run. Build `Coop.csproj` Debug with VS18 MSBuild and the `Deploy.targets` deploy
   (`ModName=Coop`). Record the module and documents snapshot and the assembly MVIDs in the case dir.
2. Start the server and 2 clients through the MCP with the `naval` profile (`doc/automated-testing/mcp-setup.md`). Wait
   for `readyForCampaignTests` on both clients.
3. On the server, run the scenario under test, for example `coop.debug.naval.scenario vs_pirates hulls=3 weapons=1`.
   Check every gate with `inspect`, `scenario_status` and the `[NavalSync]` log lines on each client.
4. After each battle, exit in the order the gate asks, then move on.
5. `stop_run`, restore the module and documents from the snapshot and verify them, then write the report with each gate's
   evidence lines.

### ProcDump

- Don't use `-h`. It false-fired while the game was loading, stopped after its dump count, and in R40 didn't fire on a
  3-minute freeze.
- `-e` is refused while the TaleWorlds watchdog is attached. The watchdog takes its own crash dump.
- On a freeze, take `procdump64 -accepteula -ma <pid>` by hand, ideally twice a few seconds apart.
- `dotnet-dump` only shows managed frames. A native crash needs cdb: `cdb -z <dmp> -c "!analyze -v; ~0k; q"`.
