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
  Heave, pitch and roll come from this client's buoyancy on its own water, which now runs on the battle host's water
  time, so they should match the owner's (see "Waves are synced" in section 6). The crew's weight is the owner's: vanilla
  pushes a hull down with the agents on its deck (`NavalPhysics.ApplyAgentForces`: committed agent mass x
  `StepAgentWeightMultiplier`, 2 on ships, x g at their mass-weighted centre), and a copy's puppets stand and sit elsewhere
  than the owner's crew (R45: copies of AI-crewed hulls heeled 2.5-4.2 degrees off). Each sample carries the owner's
  committed crew mass and centre in the hull body's frame (`BattleShipCrewLoad`), and `CopyCrewLoadPatch` applies that
  load on a copy instead of its puppets', with vanilla's in-hull check. A copy weighs its own puppets until the first
  sample with a load. An owner with nobody vanilla weighs aboard (an active agent stepped on the hull and inside its
  physics box), such as a crew that boarded an enemy hull, sends an empty load and the copy takes no crew weight. A
  sample without a load (a frame that committed none while weighed agents stood aboard) and a stale owner keep the
  last load. Own hulls run vanilla. One rule decides a copy's mode at
  every sample and step: steered while its body simulates, its owner's frames written otherwise (warning `has no
  simulating body` once per copy; `bodyMode` in `inspect`). `NavalSteeredCopies.Track`/`Forget` and the mission-end
  reset are the only writers of `NavalForeignHulls`, the static copy set the patches read; spawning, releasing, replacing
  and removing a hull all go through `Track`/`Forget`. `Track` also leaves the hull without a controller, and
  `ForeignHullControllerPatch` (a prefix on `MissionShip.SetController`) turns every later controller write on a copy into
  none without auto update: vanilla hands a hull a controller at init, at deployment end, when it burns or sinks and in
  the capture swap. A captor forgets its prize as a copy before the swap, so the prize takes the captor's controller.
- A copy collides with this client's own hulls, but its owner stays the authority of everything that happens to it:
  contact and ram damage on a copy is routed to the owner (`BattleShipDamageRouter`). Two copies never touch here
  (`CopyPairCollisionFilterPatch`, a prefix on `MissionShip.CanPhysicsCollideBetweenTwoEntities`), since no client
  simulates either; each owner resolves its hull against the other's copy.

### Streams

- `BattleShipReplicator` sends each own hull's frame, body velocity and yaw rate, crew load, helm input and rope state every 0.05 s
  (20 Hz, `SendIntervalSeconds`; a long frame's overshoot carries into the next interval, capped at one, so heavy frames
  keep the rate without a burst). Each accepted sample goes straight to the engine as the copy's steering target. A
  stream counts as expired after 1 s without samples on the receiver's clock, and the steering stops feeding the
  owner's velocity forward after the same time.
- `BattleShipConditionStream` sends a hull's damage state (HP, sails, fire, sinking) and its fieldable reserve whenever
  either changes. A late joiner gets the last one on entry.
- `BattleShipWeaponReplicator` checks a ship weapon's aim, state and ammo at 10 Hz and sends them when they change and again
  each second, plus each shot. A copy replays the shot with a projectile that deals no damage; the real hit reaches the struck
  hull's owner through `BattleShipDamageRouter`. A copy taken over in place starts loaded with the reload's bolt spent.
- `AgentStationUseReplicator` sends seat and release events (helm, oars, weapon). Puppets are seated on copies the way the
  owner seated them. A dropped item on a deck is no station: its pickup goes through `WeaponPickupHandler`, the use and
  release patches never publish it, `NavalShipEngine` neither describes nor resolves it as a station, and a seat is only
  re-seated while its point is still a station of its hull (R41a-2 W2). A deactivated machine is still seated, since its
  state is recomputed locally and can lag the owner's. A use published on an agent tick worker is announced on the game
  thread.
- A hit on a hull that another client simulates is sent only to that hull's current authority, which applies it through the
  vanilla damage method (`BattleShipDamageRouter`). A peer that no longer holds the hull drops it, so a hit applies at most
  once across a host change or a capture.
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
- Only AI hulls nobody captured have `FollowsBattleHost`. On every host change all clients move them to the new host at the
  same ordered event (`TransferHostHulls`), and their samples carry the host epoch so a stale host's traffic is dropped. A
  player's hull never changes owner unless it is captured: it stays with its player or leaves with it.

### A captured hull belongs to its captor

- A hull captured in the mission (section 2, "Capture") is simulated by its captor from then on, whoever fielded it:
  `NetworkShipInfo.CurrentAuthority` and `CaptorControllerId` name the captor, and the hull no longer follows the battle host.
  Its streams, damage, rams, weapon, fates and departure follow the captor like any own hull.
- The captor's own hull is left without a formation or a team (`IsOrphaned`, its old side in `OrphanedSide`), as vanilla's
  swap leaves it. It stays the captor's hull and leaves with it.
- Crew identity is formation-based, so the captor's formation now names the prize (`TryGetByFormation`): its crew and later
  reinforcements are the prize's.

### The DLC seam

- `Missions` holds every replicator and binds to no NavalDLC type. Every hull operation goes through
  `INavalShipEngine` (`Missions/Battles/INavalShipEngine.cs`).
- `Missions.Naval` implements it (`NavalShipEngine`), holds the launcher, the end logic and the patches, and registers them
  in `NavalMissionModule`. That module is loaded only while NavalDLC is active.
- The `CoopNavalDlc` MSBuild property (`source/Directory.Build.props`, default: War Sails is installed) decides whether
  `Missions.Naval`/`Coop.Naval` compile and ship. When it's false they build empty and `Coop`/`Coop.Tests` skip them.
- `ModInformation.IsNavalDlcActive` is set once at startup: NavalDLC enabled and both naval assemblies shipped. The
  module validator, the host warning and the server's naval battle gate all read it, so a build without the naval code
  rejects NavalDLC clients as before.
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

### Arrivals

- Hull-addressed naval messages that are sent once, or that depend on queued state, apply from the arrivals queue
  (`IBattleShipArrivals`) at the start of the next naval services tick, in arrival order: spawn records, removals, departures,
  entries, host changes, conditions, final ropes, routed damage, ship weapon shots and states, and capture requests, confirms
  and records. The game-thread queue drains while the engine's async agent and team tick runs, which reads the hull state
  these change (R48 L6).
- Samples go through the same queue, merged to one queued arrival per hull that applies the newest sample; a sample for a
  hull whose record still waits is held with the record. Station use is kept pending and applied by its replicator's tick.
- A local hit on an AI hull waits once behind a queued host change (the session's epoch is ahead of `IAppliedHostEpoch`)
  and then goes to the hull's authority, so it reaches the successor. Hull conditions are stamped with the applied epoch.

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
  back into a steered copy (`ReleaseHull`).
- `coop.debug.naval.reswap` runs a swap without a migration.

### Capture

Vanilla captures a vacant enemy hull when the main agent holds its helm for 3 s while the hull is connected to the
player's own hull by a bridge (`ShipControllerMachine`), then swaps the two hulls' formations (`NavalShipsLogic.OnShipCaptured`).
Co-op runs vanilla's timer on the captor's view and lets the hull's current authority decide. The request and decision are
`Missions/Battles/BattleShipCaptureReplicator.cs`; applying a capture is `ShipCaptureApplier.cs`; the DLC side is
`INavalShipCaptureEngine` (`Missions.Naval/NavalShipCaptureEngine.cs`).

1. The main agent may start at another owner's helm only when that copy's team is an enemy (`ForeignHelmUsePatch`).
   Vanilla's helm gates the rest: an enemy helm is AI-only until its formation empties (a fresh enemy copy's helm opens to
   the player at once, `NavalShipEngine.SpawnForeignHull`, since its formation may never get a unit here) and disabled for
   players until the hull is vacant and connected.
2. When the timer runs out, `CoopShipCapturePatch` skips vanilla's swap and publishes `ShipCaptureTimerElapsed`. The captor
   sends `NetworkShipCaptureRequest` to the hull's authority alone, or decides at once when it is that authority (the battle
   host capturing an AI hull it simulates).
3. The authority decides on the real hull (`BattleShipCaptureReplicator.Decide`, reasons in `ShipCaptureRefusal`): it must
   still simulate it; the captor must be in the mission with a registered hull of its own that still has its formation; the
   hull must be an enemy's (or the captor's own orphan), not leaving or mid-swap (`Busy`), not a storyline hull, not
   sinking, and vacant by vanilla's rule, read fresh (its 5 s cached formation query is invalidated first). It applies the
   capture itself and broadcasts `NetworkShipCaptured`, or sends `NetworkShipCaptureRejected` to the captor alone.
4. Every client applies `NetworkShipCaptured` in one step of its naval services tick (`OnMissionTick`), never from the
   game-thread queue: the queue drains while the engine's async agent and team tick runs, which reads the assignments and
   formations a capture rewrites (R48 L6). Capture requests, confirms and records share one arrivals queue (`IBattleShipArrivals`) with
   hull spawn records, removals, departures, entries, host changes and hull conditions, applied first in that tick in
   arrival order. An AI hull's condition from its successor that overtook the host change is kept until the change moves
   the hull to it. A result report or the mission end applies what waits first, unless the async agent tick runs (then it
   stays for the next tick). A sample for a hull whose record still waits is held with the record.
   The authority moves in place
   (`IBattleShipReplicator.TransferHull(..., inPlace: true)`: sample and condition order restart, the captor adopts its copy
   with `INavalShipEngine.AdoptHull`, the old authority turns its hull into a copy with `ReleaseHull`; no hull is removed or
   replaced), then vanilla's swap runs on the local hulls (`ApplyCapture`, copies keep no controller), the registry records
   the prize and the orphan, and the prize counts as gone from its old side. Nothing of this starts unless vanilla's swap
   can run on the local hulls (`INavalShipCaptureEngine.CheckCapture`: both hulls in the mission, the captor's hull holds
   its formation's assignment, both teams have a naval team AI). The authority refuses such a capture with
   `SwapUnavailable`. A receiver holds it unchanged and retries with its held confirms and records (logged once,
   `swapsBlocked`), and applies it whole once the swap can run. After `ShipCaptureApplier.SwapWaitSeconds` (30 s) a
   receiver gives it up (Error, `swapsGivenUp`, `lastDrop=swap_blocked:<reason>`) and no longer matches its peers; the
   captor itself never gives up: it takes the prize over without vanilla's swap (Error, `capturesWithoutSwap`): the
   authority, registry, records and server report follow the capture and the hull keeps its old formation there. That
   beats handing it back, since every other client already gave the prize to the captor and stopped simulating it, and a
   hand-back would need every peer to undo an applied capture. While a captor holds, nobody simulates the prize.
5. The captor sends `NetworkBattleShipCaptured` to the server on the relay channel its fates use, so the server takes the
   prize's fates from it (section 3), and sends its records of the prize and the orphan to every peer again.

- **Order.** Every hull counts the captures it took part in, as prize or as the captor's hull
  (`NetworkShipInfo.CaptureSequence`). A confirm carries both hulls' counts on its sender and applies only when they match
  the receiver's: ahead of them it waits, behind them it is a repeat (counted) or a conflict (logged, the first applied
  stands). So captures of one hull from different senders apply in the same order everywhere, whichever arrives first.
- **Swimmers keep the prize's old formation (R46).** Vanilla's vacancy ignores swimmers, and its swim AI moves the crew of a
  sinking hull into the formation of a floating one (`AgentNavalComponent.CheckAgentOffShip`). So capturing the last afloat
  hull of a side whose other hull sank leaves its old formation with units and no ship, and vanilla's AI refresh at the end
  of the swap (`NavalQuerySystem` average ship position) read that formation's ship unchecked: an NRE on every client, then
  a native crash. Singleplayer can reach the same state. `NavalShiplessFormationPatch` makes the three unchecked lookups
  (own and enemy average ship position, `ShipOrder`'s closest enemy ship) skip a formation without a ship, as their sibling
  queries do. A throw inside vanilla's swap after the checks is logged as a half-applied swap with its step (diagnostic only).
  Not patched (follow-up): the other `GetShip` call sites that drop its result, mostly behaviours caching their formation's
  hull (`BehaviorNavalEngageCorrespondingEnemy` ctor, `RefreshTargetShip:198`, `TickOccasionally:446`, `ResetBehavior`,
  `OnBehaviorActivatedAux`; `BehaviorNavalRemoveConnection` ctor and `:38-99`; `BehaviorNavalRamming.GetAiWeight:107`;
  `BehaviorNavalSkirmish.GetAiWeight:80`; `BehaviorNavalDefendInLine.TickOccasionally:336` and the in-line neighbour lookups;
  `NavalOrderController` order setters; the deployment controllers' filters). A formation left without a ship by a capture
  reaches the behaviour ones, the likely R48 L6 cause (code evidence, not a stack): `BehaviorNavalRemoveConnection.GetAiWeight`
  dereferences the null hull on the async team tick, freezing the game thread (`WaitTickCompletion`) and crashing the client. The capture
  engine resets the behaviour weights of the prize's former formation once it has no hull, so the formation AI picks none;
  `ShiplessFormationBehaviorWeightPatch` weighs `BehaviorNavalRemoveConnection`, `BehaviorNavalRamming` and
  `BehaviorNavalSkirmish` at 0 for any formation without a hull in a co-op battle; a hull spawned into a formation with
  units and no weighted behaviour gets, from the mission tick, fresh same-side ship lists and a tactic re-apply (its
  count and order checks miss that case); and
  `NavalAgentTickFailurePatch` logs a throw in the async agent and team tick, finishes the tick and swallows it.
  Ramming's weight and ramming and skirmish ship orders also read the closest enemy formation's hull unchecked, which
  L6's swimmers lack: `ShiplessFormationBehaviorWeightPatch` weighs ramming at 0 then, `ShiplessTargetShipOrdersPatch`
  skips both orders, `SkirmishOrderWithoutTargetPatch` skips a skirmish order at a missing hull (also the player's order
  controller), `EngageOrderWithoutTargetPatch` and `FollowOrderWithoutTargetPatch` do the same for engage (charging
  swimmers) and follow (follow-me while swimming), `ShiplessSelectedFormationOrdersPatch` runs the naval order setters on the
  selected formations with a hull only (a captured player's own formation has none; skipped when none is left; also
  `SetOrderWithPosition`, as a co-op hull removal now expires every team's 5 s naval query lists too), and
  `RammingCancelWithoutAiControllerPatch` skips the controller call of a ramming whose hull lost its AI controller.
- **One decider per place in the order.** Only a hull's current authority decides a capture of it. A player's hull, or a
  captured hull, changes authority only through captures, so a confirm for it must come from its current authority at that
  place. An AI hull's authority also moves with host changes, which aren't ordered against confirms, so its confirms are
  ordered by the capture counts alone. Two guards keep one decider per place:
  - a promoted host refuses its inherited AI hulls as `Busy` for 2 s (`PromotionSettleSeconds`), so a capture the old host
    confirmed just before it left reaches it first over the mesh;
  - a client refuses a capture of its own hull as `Busy` while its own request to capture another hull with that hull waits
    (that request's decider may confirm a capture at the same place of this hull's order).
  Residual: an old host's confirm that takes longer than the settle window conflicts with a successor's capture of the same
  hull; the conflict is logged (`captureConflicts`) and each client keeps the one it applied first. A late copy of a capture
  already applied is a repeat (`duplicateConfirms`), even once the hull moved on; it is a conflict only when this client
  applied a different capture at that place of either hull's order.
- The decider sends its confirm before it applies the capture, so peers take it through the confirm rather than through
  the records the captor sends again; if those records still come back first (a mesh that delivers at once), it applies once.- A confirm is exempt from the membership gate, since mesh routes aren't ordered against the server's departure and
  migration events, so it applies after its sender left or the hull moved to a successor. A confirm for a hull that is
  mid-swap, or whose record still waits here, waits until the hull is registered or gone, however long a joiner's
  deployment takes. One that arrives after its captor left here removes the prize, as it left with the captor everywhere else.
- A record from a hull's current authority that shows captures this client missed (it never got the confirm) brings a
  registered hull up to date: a prize is captured through the swap with the captor's hull the record names (waiting while
  either hull is mid-swap here), an orphan loses its formation. A record of a later capture also replaces a held record of the
  same ship. Not built: when the captor's hull is already orphaned here by that same capture, the prize can't be swapped and
  would need respawning from the record; it stays as it is and a warning is logged. A record dropped because its owner left
  marks the ship id removed, so confirms held for it are dropped too.
- **The victim leaves at once (C4).** If a PvP victim confirms and disconnects, a client can get the victim's departure
  before its confirm and take the prize out with the victim. The captor's records sent again after the capture bring it
  back: a captured record of a hull this client removed with another departed player reopens and spawns it as the captor's.
  The server records the capture even after the owner's party left the battle, and keeps the first captor per place in the
  hull's capture order (NetworkBattleShipCaptured.CaptureSequence), so the campaign result doesn't depend on which report
  arrives first.
- A request nobody answers in 5 s expires (the authority left before deciding). The captor's player sees one line when a
  capture is refused, times out or is won by someone else (the winner's broadcast confirm tells it).
- A late joiner gets the prize's record from the captor with `CaptorControllerId` (ProtoMember 20), the hull's capture
  count (22), the captor's hull it was taken with (23) and the side it was taken from (24), and the orphan's with
  `Orphaned` (21). It spawns the orphan into a free formation and then orphans it (`OrphanHull`), and counts each capture as
  a hull gone from its side, so it ends the battle by the retreat rule if it becomes the host.
- PvP: a player's hull is captured the same way, with its owner's client as the authority; vacancy then needs that player's
  whole formation, its main agent included, swimming or gone. The victim's formation keeps its main agent but no hull, so
  vanilla's helm checks ask whether a null hull is connected; `CapturedFormationConnectionPatch` answers "not connected".
- Vanilla points the old owner's crew record of the prize at the captor's orphan. A tracked agent counts as a hull's crew
  only on the hull's team (`NavalShipEngine.IsHullCrew`), so those swimmers never withdraw with the orphan.
  Removing the orphan leaves that record naming the removed hull: vanilla's removal handlers skip a hull with no team
  (`NavalAgentsLogic.OnShipRemoved`), and its reinforcement tick skips such a record (`NavalShipAgents.CheckSpawnReinforcements`
  needs `Ship?.Team`), so the swimmers stay in the battle and nothing spawns onto the removed hull. Not yet live-checked (R48).
- When the captor leaves, retreats or disconnects, the prize and the orphan leave with it through the usual departure path
  (they don't follow the host), and the NPC keeps its ship with the battle HP the captor reported. The captor's own mission
  end announces only the prize: vanilla's `NavalShipsLogic.RemoveShip` acts only through the ship assignment holding a hull,
  and the orphan holds none, so its peers remove the orphan when the captor leaves. `NavalShipEngine.RemoveHull` lends an
  assignment-less hull a free assignment for that removal, so vanilla's `RemoveShipAux` takes it out of `AllShips` and the
  scene and raises the removal events (R47 bug A).
- Vanilla's off-ship check (`AgentNavalComponent.CheckAgentOffShip`, every 5 s per AI agent) drops an agent from its hull's
  crew and formation when it stands on a hull whose navmesh island holds neither its formation's hull nor any allied
  formation's. After a capture the orphan has no formation, so the captor's troops still on the orphan's deck lose their
  formation as soon as the prize and the orphan part (ropes cut, the prize sails off, or the orphan's AI drifts away). This is
  vanilla behaviour, not a sync loss; it was R47's "bug B" (battle 3), where the prize sailed 50 m off before the migration.
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

No rope may hook or stay tied to a removed hull either (R46 freeze). Vanilla's removal breaks the ropes and clears the
boarding order, but not the rope stations' AI preference (`ShipAttachmentMachine._preferredTargetShip`), and its pilot AI
throws at the preferred hull's points without a removal check (`GetBestEnemyAttachment`). A hook that landed on a removed
hull built a joint with no target hull (`ShipAttachmentJoint._shipTargetScript` null, its root entity no longer carries a
`MissionShip`), whose first fixed-tick force step threw on the native physics thread, and the TaleWorlds Watchdog froze the
process. Every removal path now keeps it: `NavalRopes.BreakConnections` before `RemoveShip` (departure, capture, migration
replace), `ForgetTarget` on vanilla's `ShipRemovedEvent` (vanilla's own retreat too), `RopeConnectPatch` refuses a rope to a
removed hull, `TickRope` breaks one whose end left or whose joint lacks a hull (`NavalRopes.EndIsGone`), and
`RopeJointHullGuardPatch` skips such a joint's force step until then (the first skip is logged, `jointStepsSkipped`). A sinking
hull is not removed (vanilla keeps it in `AllShips`).

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
  with vanilla's rules, over the hulls `NavalSideTallyReader` gathers through the engine (the E2E scenarios count through
  the same reader):
  - every live human counts, swimmers included;
  - crew counts only on afloat hulls, by the hull its formation crews, since a copy's `NavalShipAgents` is empty;
  - reserve is what each owner reported for its hull;
  - a sunk or departed hull's reserve doesn't count.
- **Player hero grace.** Vanilla lets the local main agent's side lose to hulls out of action only once that agent was down
  for 20 s, and gives the other side no wait. Coop keeps that wait for each side's player heroes (`NavalPlayerHeroGrace`),
  so in PvP neither side loses while one of its players still fights; an AI side still has none. Only current mission
  members' heroes count, matched by the character's registry id, and every client scans for them once a second, so a
  promoted host keeps the history. Unlike vanilla, which ends a depleted side at once, the side depletion check waits for
  the same grace (maintainer decision after R49), so a side whose last living human was its player hero still holds about
  20 s after the hero went down.
- **Other clients** end with the host's replicated result. A side counts as retreated only once hulls left it, with their
  players or to a capture, and none is left. So capturing the last enemy hull ends the battle as an enemy retreat after
  vanilla's 5 s, as in vanilla (`MissionResult.CreateSuccessful(..., enemyRetreated: true)`); the committer reports only the
  `BattleState`, which is the same as for a depletion win. A captured hull counts on its captor's side with the captor's
  crew and reserve, and the orphan (no team) isn't counted.
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
    Once a member reported capturing a ship (`NetworkBattleShipCaptured`), only that member's fates of it count and its
    first one replaces the held fate; an owner taking its own ship back clears the record. The newest by host epoch then
    revision wins, a sunk ship stays sunk, and fates are dropped once the results were calculated.
  - `IBattleShipFateApplier` writes them with patches live, never under `AllowedThread`, so every write replicates:
    the owner's Boatswain gets the summed mitigated damage once (vanilla's 0.1 XP per point), then a sunk ship goes
    through `DestroyShipAction` and any other ship takes its battle HP and sail HP. `OnShipDamaged` is not called again.
  - It applies at a member's departure (its party's ships and the ships it captured, so a rejoin reads them; a ship of its
    party that another player captured stays held, since a capture changes no campaign owner and the prize leaves with its
    captor, R46), when the
    instance empties or the battle finalizes without a commit (everything left), and in `MapEventPatches.Prefix_OnBattleWon`
    before the results and the loot read the ships.
  - A party left without ships gets its raft from vanilla at `MapEventEnded` (Jordan's raft patches run on the server).
  - The client menu refreshes on ship HP, anchor and sea-state writes (`ShipHitPointsPatches`, `AnchorPointPatches`,
    `MobilePartyBehaviorHandler`) skip while a mission is current, because the applied HP reaches players still in the
    battle; `MapState.OnActivate` refreshes the menu on return.
- **Not written back:** the captured-ship list vanilla `OnMissionEnd` builds (patched off by `CoopNavalShipCapturePatch`;
  it lists every losing hull still in the mission, captured in it or not). Only a disengage reads it, so a decisive result
  already matches vanilla, and disengage parity is M6c. In-mission capture changes no campaign owner in vanilla either; it
  only decides who reports the prize's fate.
- **Loot.** Jordan's `LootDefeatedPartyShips` runs after the fates applied, so sunk ships are gone and survivors take
  the after-defeat damage on top of their battle HP, as in vanilla. A player's share is held by `LootShipHold`: it
  leaves the losing party at once (a replicated `Owner = null`, no destroy), so that party's finalize (`RemoveParty`
  destroys its ships) no longer kills ships the loot screen still offers. Taking one (`ApplyByLooting`) and discarding
  the rest (`LootShipsHandler`) work after the finalize. The discard releases the hold; a held ship nobody took is
  destroyed when its looter disconnects or its party is removed, or after a 2 hour fallback for a loot screen that never
  closes (the ship screen comes after the troop and item loot and any conversations).
- **Server log (Information, `[NavalSync]`, one line per ship).** The applier logs `Ship <id> of <party> grants its Boatswain
  XP for <n> mitigated damage`, `Ship <id> of <party> sank in battle; destroying it (hp, sail)` and `Ship <id> of <party>
  keeps its battle condition: hp <old> -> <new>, sail <old> -> <new>`. The loot path logs `Loot ship <id> of <loser> went to no
  winner; the battle results destroy it` (a postfix on the distribution, `NavalLootShipDistributionLogPatches`), and
  the hold logs `Holding loot ship`, `Released loot ship <id> held for <looter>: taken by <party> | discarded ...` and
  `Destroying unclaimed loot ship <id> held for <looter>: <reason>`.

## 4. Status

### Goals

| Goal | Status | Proven by |
|---|---|---|
| G1 Beat a pirate fleet to a natural end, with ship weapons | Natural end done and live-passed (R40 A, B1, B2). Weapons live-passed in R41a-2 W1 (ballistas for 156 s, no crash). | `vs_pirates`, `sinking`, `fleet`, `migration`, `weapons=n`; `NavalNaturalEndScenarioTests`, `NavalShipWeaponScenarioTests`, `NavalVsPiratesScenarioTests` |
| G2 Ramming and boarding between any two hulls | In progress (M10a): copies are steered dynamic bodies (D3 option B) and collide with own hulls; contact and ram damage route to the owner, and only the rammer's owner runs the ram's damage tick. Not live yet. | `ramming`, `boarding`; `NavalCopySteeringTests`, `NavalDamageRoundTripScenarioTests` |
| G3 Retreat, leave or disconnect without breaking the battle | Retreat and leave live-passed in R41a (A, B, C). Disconnect is a leave (D4); hard drops of a non-host and of the host live-passed in R41a-2 (D, E). Done: the R41a-2 W2 and S fixes and the departure order (parked N of N, then withdrew N) live-passed in R41a-3. | `retreat`, `migration who=nonhost`; `NavalDepartureScenarioTests`, `BattleReserveReconnectScopeTests` |
| G4 Results match vanilla | Done in code (M6), not live yet (R45): battle HP, sail HP, sunk ships and Boatswain XP are written back, and loot ships survive the losing party's finalize. In-mission capture replicates (authority-confirmed, in place, PvP included), not live yet (R46). Disengage loot (M6c) is pending. | `NavalShipFateScenarioTests`, `NavalCaptureScenarioTests`, `LootShipHoldTests`, `BattleShipFateReporterTests`, `BattleShipFateLedgerTests`, `BattleShipCaptureReplicatorTests` |
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
| M10w water clock sync (shared battle wave time) | done, live-verified in R45 |
| M10b plank crossing and melee | pending, needs M10a |
| M6 results parity (fates, Boatswain XP, loot hold) | done in code, R45 next |
| M6 in-mission capture | done in code, R46 next |
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
- **Waves are synced (water clock, 2026-10-21, live-verified in R45).** This supersedes "copies float on local water".
  R45 measured the water 0.022 m mean apart, with no backward step through two host changes and a late join.
  R42 found that wave height follows the engine's accumulated mission timer, which adds up every mission a client has
  run, so clients with different mission histories saw different wave phases (1.54 m mean apart in
  `battle_terrain_opensea_southern`; aligned on the probe's `total` the two clients' water matched to 0.003 m). The
  native water update (`TaleWorlds.Native.dll` v1.4.8, function at RVA `0x1DA870`, called for every view's scene by the
  engine frame at `0x9B820`) reads that clock as `(float)ticks[scene+0x6064] * 1e-4`: the int64 table at RVA `0xDA29B0`
  holds one tick count per `MBCommon.TimeType` (1e5 ticks per second), and mission scenes use `Mission`, so the read is
  10 x `GetTotalMissionTime()`. `NativeWaterClockPatch` (GameInterface) replaces that `cvtsi2ss`+`mulss` with
  `movss xmm1,[slot]`, a float this mod owns. It is installed once, on first use by a client opening a naval battle
  (Windows, client role; it refuses a server or headless build), and only when its signature matches exactly once;
  otherwise it logs the match count and DLL identity and waves stay vanilla. The redirect is switched on only while a
  coop naval battle runs (only from the game thread, the thread that runs the load) and switched back first thing at
  mission end, so other scenes run vanilla code. `0x1DA870`'s only caller chain is `0x9B820`, the frame tick `0x53950`
  and the `rglFrame_move_task`, with one frame task in flight at a time. After the water update the same frame tick calls
  through the managed interface at `0xDA2A28` (`0x5422A`, with dt), most likely the managed tick, though that callback
  wasn't identified. So a swap from `AfterStart` or mission end shouldn't race the site; this is a static reading, not a
  live check. The campaign map's view may stay registered under the mission
  (`MapScreen` toggles its scene view itself, and `0x9B820` has no enabled-view check), so map water can read battle
  time while the battle runs; that is hidden and cosmetic, and map water right after a battle is a live check.
  - Each scene's ocean keeps its own previous time (the water object at `scene+0x628`, created per scene by `0x1DA040`
    and zeroed by `0x30F700`; only `0x30F230` writes its time), so a new mission scene starts clean. A large or
    negative step in the water time is harmless: `0x30F230` turns it into a dt only for the foam flipbook
    (`+0x66c += frames * dt`, keeping just the fractional part each frame, so one frame shows a wrong foam frame at
    most), and the wave phase it passes on is absolute (`t * 0.7 + 1000`). Vanilla already takes the whole
    accumulated clock as dt on a new scene's first update.
  - Every client starts the battle's water at the same time, 0 (`BattleWaterClock.BattleStartWaterTime`), from
    `AfterStart`, which runs under the loading screen, instead of at its own accumulated clock. Clients that load
    together already agree to within their load-time skew, and the switch from vanilla happens before the battle is
    visible. The latest host time that arrived during loading (a late joiner gets one at once) is used as the start
    instead; the mission timer stands during loading (`TickLoading` doesn't tick the mission, `FinishMissionLoading`
    ticks it twice by 1 ms), so the latest sample is the freshest.
  - The battle host writes its own clock and sends it every second (`NetworkBattleWaterTime`, host-epoch stamped) and at
    once to a peer entering the battle. From `AfterStart` on, a client's written time never runs backwards: a host time
    more than 2 s of mission time (20 water units) ahead jumps forward, at once for the first host time and the first
    after a host change, otherwise only when all of the last 3 samples are that far ahead. Everything else is slewed
    at 75% to 125% speed toward a target smoothed (0.3 per sample) toward the largest offset of the last 3 samples
    (transit only makes an offset look smaller).
  - A promoted host keeps its current offset, so it continues from its own water time. Election ignores water time, so
    a promoted member may never have heard the old host (a joiner still on its 0 start). On a host change
    (`BattleHostMigrated`) every client therefore sends its own time to the new host once, and the host jumps forward to
    any client time more than 2 s ahead; the clients' backward gap to the lagging host only lasts until then. The report
    travels the mission mesh and the assignment the server channel, so a report that arrives first is held and applied
    on the new host's first tick at that epoch. `NavalWaterClockScenarioTests` covers this across instances with an
    in-memory water clock.
  - The ship wake renderer reads the same clock at `0x1DAB69` (1e-5 scale, seconds) and is not patched, so wake effects
    stay on each client's local clock.
  - Not compensated: the host time's transit and the frame it waits for the game thread. The R42 fit puts 0.02 s of
    clock lag at about 0.015 m mean water difference.
  - Copies still float on local buoyancy, but the local water now matches the owner's, so a copy's heave, pitch and
    roll match its owner's too. R45 confirmed the water (0.02 m) and heave (0.02-0.05 m mean), but copies of AI-crewed
    hulls rolled 1.9-4.3 degrees off, from the puppets' weight (see section 1). With the crew load fix, R46 measured
    copies' crew mass and centre equal to the owner's and free-hull roll at mean <= 0.06 degrees, sd <= 0.51 degrees,
    gain 0.99-1.01. Roll is pass/fail per free copy over windows of at least 60 s without sinking, snap, ram contact or
    a rope, on the signed offset copy minus owner: |mean| <= 0.5 degrees, sd <= 1.0 degrees, and a gain of 0.9 to 1.1
    (slope of the copy's roll on the owner's, both de-meaned). Roped hulls are excluded, see the next item.
  - **Roped hulls still roll off.** R46 measured roll sd 1.7-4.4 degrees and a gain up to 1.56 on copies tied by ropes,
    even with an empty deck, so it is the rope and contact force on copies, not the crew load. Roll stays report-only for
    a roped copy until that follow-up lands.
  - `coop.debug.naval.water_clock` reports the patch status, the written and vanilla water time, their offset and the
    battle's offset estimate.
  - **Each game-version port must re-check the signature** and update it if it no longer matches: the scene field
    offset `0x6064` and the register allocation can change on any engine rebuild. It does not match the v1.2.8 GOG DLL.
  - `WaterQuality` and `PhysicsTickRate` are still per client. `water_probe` logs both clocks; align two probes on
    `total`, not on `mt`.
- **Jordan dependencies:**
  - M6 relies on `CampaignShipsSync` replicating a server's `HitPoints`, `SailHitPoints` and `Owner` writes and
    `DestroyShipAction` (E2E-proven);
  - the campaign side of a mid-battle retreat (the party at sea, rafts) is his;
  - ship loot and distribution are his. M6 adds, for his review, one `LootShipHold.Hold` call in
    `MapEventResultsInterface.LootDefeatedPartyShips` (and a constructor parameter); `LootShipsHandler` and
    `PortScreenChangesHandler` needed no change, since the offered ships keep their handles;
  - disengage loot in `MapEventResultsInterface` (M6c);
  - AI parties' ships must be registered for their fates to apply (a missing one logs `Failed to get Ship`);
  - the PvP loser's ships vanish (R39).
- **Ship fate limits (M6).** A hard drop loses up to 2 s of HP changes (a sink always goes out at once). The ships of an
  AI party that a non-host player fields with its own team are refused, since only the host may report AI ships. A
  fate that arrives after the results were calculated is dropped. A reporter counts a fate's mitigated damage as sent, so
  the Boatswain XP of a fate the server refuses (an old host's at a stale epoch, a sender already out of the battle) is
  lost; its HP and sunk state come back with the next fate, since those are absolute.
- **A client that joins while loot ships are held lacks them.** A held ship has no owner, and the save reaches ships only
  through a party's ships, so a client joining or rejoining while someone's loot screen is open never loads it. If the
  looter then takes it, the `Owner` write can't resolve there and the looter's fleet is one ship short on that client
  until it rejoins. A fix needs a design call: send held ships to a joiner after its save loads, or keep them reachable
  from something the save walks.
- **Ram damage has one writer.** A ram exists only on a hull with a bow ram piece (`MissionShip._ram`; the preset hull
  `northern_light_ship` has no bow slot, so use `ram=n`). Vanilla's `MissionShipRam.OnTick` runs on every client. The
  collision gate drops a copy rammer's `DealCollisionDamage`, so only the rammer's owner deals the hit, routed to the
  target's owner when the target is a copy. Every client keeps vanilla's 2 s cooldown, deck breaks and effect. The
  fixed-tick ram forces stay, so each owner's hull is pushed back by the other's copy locally.
- **Ram kills are authority-scoped (`NavalRamKills`).** `RamTickPatch` transpiles the tick's one `Agent.Die` call (and
  stops co-op from starting if a DLC update moves it), so every client that sees a ram impact, through the owner's tick
  or a copy's, kills only its own (or un-networked) agents within 2 m: the kill loop skips another owner's puppet, and
  that owner kills it on its own view of the impact and reports it like any death. Every
  client that sees the impact also breaks the target's deck parts within 5 m locally (oars included), which only steers
  the outcome on the hull's owner and is cosmetic elsewhere. A puppet that still dies here without its owner's death is
  logged as a Warning (`Puppet ... died here without a routed blow`); DEBUG counts the spared ones (`ramPuppetsSpared`).
  A client whose copy of the ram misses an impact the owner saw keeps its agents there alive. The owner-side kill of a
  puppet the rammer's client spared is not verified live yet.
- **Ram morale follows the same rule.** Vanilla's `OnShipRammed` raises the rammer team's AI-controlled agents, and
  morale isn't replicated, so a client whose gate skips a ram's `DealCollisionDamage` calls it for its own agents and the
  owner's routed apply skips it (`RoutedRamMoralePatch`). A contact collision keeps vanilla's single call on the hull's
  owner, so other clients' agents get no contact morale.
- **Routed hits are at most once, and these windows lose one** (counted by reason in `inspect`'s `shipDamage.drops`):
  - a hit sent to an authority that handed the hull on before it arrived (host change, capture, or a hit overtaking the
    host change on another channel) is dropped as `not_authority`;
  - a sender's hit on an AI hull while a host change's move is queued waits once behind that move (`deferred`), but a
    host change still waiting for its game-thread hop goes to the old host;
  - an old host that applies a hit just before its own move keeps it only if its next condition goes out first;
  - hits still in the arrivals queue at `Dispose` are cleared. Fates are flushed from the applied state, so results never
    see them.
- **Allied fleets.** An AI lord on the players' side fields no hulls. Agents refused by a formation with no hull are not
  re-assigned. The launcher still counts AI allies in vanilla's 8-ship side cap split, so their unfielded share is lost: a
  player with 6 ships next to an AI lord with 6 fields about 4.
- **Saved Order of Battle layout.** Co-op skips `NavalOrderOfBattleVM.LoadConfigurationShips`, so the player's saved ship
  and formation layout never applies, while vanilla's `SaveConfiguration` still stores the snapshot ships into the
  client's `NavalOrderOfBattleCampaignBehavior` after each battle.
- **Player deployment slots.** When a side has more than one player, every client lays out all the side's player fleets
  with vanilla's `ComputeTeamDeploymentOffsets`, each fleet as deep as the deepest possible one (1.1 times the longest
  ship deployment length in the game data) with vanilla's 32 m gap, and takes its own rank's slot
  (`NavalPlayerDeploymentSlot.SlotFor`). The side base comes from vanilla's `ComputeDeploymentBaseOffsets` fed with the
  small-battle offset (-min(path, 400) / 2 + 1), not with `NavalShipsLogic.ComputeSpawnPathDeploymentOffset`, which deepens
  with the ships this client assigned (only its own fleet) and so differed between players with more than two hulls. So
  every client derives the same layout, blocked segments are skipped, and when nothing fits behind the side base (R49: the
  spawn point sat near the path start) the fleets are packed forward from the path start as vanilla does. A Warning is
  logged only when even that doesn't fit; the `Deployment slot` Information line logs the base and the slot on each client.
  Known limits (M11, 3+ players per side):
  - the forward packing of three or more fleets can reach the enemy's line (four 74 m fleets put the front one 5 m from
    the opposing base), and the fit check doesn't look at the opposing base;
  - the player count is the side's registered player parties when each client opens the mission, so a late joiner counts
    one more party than the clients that opened before it, and in the forward packing every slot moves with that count.
- **R49 freeze (native, no managed guard).** Client1 crashed 29 s into `vs_pirates hulls=1 weapons=1` with an
  AccessViolation in native `Mission.GetNearbyAgentsAux`, called from vanilla `NavalPhysicsAgentCollisionSolver.OnParallelFixedTick`
  on the fixed-tick thread; the watchdog then held the process frozen. The dumps show valid inputs (a finite centre and a
  1.12 m radius) and a torn read of the engine's agent grid: a cell head read while an agent moved to the next cell. The
  managed lock only serializes readers, so no managed check can prevent it. The trigger was the slot overlap above, which
  threw crews across cells next to floating objects. Skipping the solver would also turn off vanilla's agent push on
  floating objects, so it's left as is; the next freeze needs a watchdog-timed dump (§7 ProcDump).
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
  owner (`Dropped hull record ... of departed ...`). A station use from a sender that isn't a member yet waits under its
  10 s pending lifetime; weapon catch-up may hit the same ordering gap. The relay ordering itself is unchanged. The hold is not hit live yet: in R41a-3 the race didn't occur.
- **Follow-up A, late hulls and `MissionShip.OnDeploymentFinished` (done, not live-checked).** Hulls fielded after
  deployment (AI fleet, copies, swap replacements) get vanilla's whole `MissionShip.OnDeploymentFinished` once, from
  `NavalShipsLogic.ShipSpawnedEvent` (`NavalLateHullDeployment`). A swap sets the carried ammo after the spawn, and a copy
  takes the replicated ammo. Live check: a climb interrupted on an AI-fleet hull, an AI rope throw, an oar seat on a copy.
  The capture side is done: a fresh enemy copy's helm opens to the player (section 2, "Capture"), and capture replicates.
- **Capture limits (not live yet, R46).**
  - The captor's prompt reads vacancy on its copy, where puppets swim on local water and the reserve reads 0, so it may
    show a hull vacant that its authority then refuses (`NotVacant`); the authority's verdict is the one that counts.
  - The orphan gets an AI controller with no formation from vanilla's `UpdateController` on its captor, as in vanilla.
  - A copy whose body doesn't simulate is adopted as it is and only logs a warning; the swap fallback was left out, since
    it would drop the captor's reserve and take its formation off player command.
  - A capture an old host confirmed more than `PromotionSettleSeconds` before it reaches the successor can conflict with
    the successor's own capture of that hull; clients keep the one they applied first and log `captureConflicts`.
  - A client that took a PvP victim's hull out with the victim and then gets the captor's records spawns the prize again,
    but the captor's crew puppets it already had stay in the orphan's old formation, not the prize's (no swap ran there).
  - A late joiner counts only a hull's last capture as a hull gone from its side (the record carries only that one), so its
    retreat tally misses an earlier capture of a hull that was taken again. It also never counts hulls that left with
    their players before it joined; if it becomes host, such a side ends as depleted once its last swimmers are gone,
    instead of vanilla's 5 s retreat.
  - A captor's request can expire (5 s, the player hears that nobody answered) while its confirm still waits somewhere on
    the way; the capture applies when it arrives, without another message.
  - A PvP victim's formation keeps vanilla's record pointing at the captor's orphan, so a later reinforcement of the victim
    would target a copy; its client logs a warning when its hull is captured.
- **Weapon target lists are snapshots (follow-up B, fixed R46).** A ballista lists its ship targets once, at its own
  deployment (`ThreatSeeker.InitializeTargetableObjects`); `NavalLateHullDeployment` refreshes every fielded hull's list
  on the mission tick after a later hull spawns, so co-op hulls arriving after deployment are targets as in vanilla.
- **A captured prize's ballista may stay idle (vanilla).** `GetAllThreats` drops a target on the weapon's own side for good,
  so before the capture the prize's ballista already dropped its old side's hulls; after it (`RangedSiegeWeapon.OnShipCaptured`
  only sets `DefaultSide`) it can't target its new enemies' hulls and skips formations in melee, as in singleplayer. Unlike
  singleplayer, the late-hull refresh above re-reads every fielded ballista's list whenever any hull spawns later (a copy, an
  AI fleet, a swap replacement), so after such a spawn the prize does target its new enemies' hulls. Its gunner also stops
  within 7 m of an enemy (`Ballista.EnemyRangeToStopUsing`). R46 L1's idle prize matched the vanilla part.
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
  - the DEBUG engage reset tracer should check cheaply whether anything is watched before it resolves; delete it
    (`NavalEngageResetTracer` and `NavalEngageResetTracePatch`) once the `encounter` preset passes live (G5);
  - the next cut of `NavalShipEngine` is the hull-swap trio (`ParkHullAgents`, `ReplaceHull`, `BoardHullAgents`).
- **Boarding.** Ropes and bridges replicate, but plank crossing and melee on a foreign deck were never seen live, and a
  rope cut from the target side isn't replicated.
- **Sea raids** still open the land raid scene.
- **CI** has no NavalDLC, so release builds ship without `Missions.Naval.dll` (maintainer decision) and reject NavalDLC
  clients. What ships still follows the CI image; pass `-p:CoopNavalDlc=` in the release workflows to make it explicit.

## 7. Debug surface

### Commands (`coop.debug.naval.*`)

| Command | Side | What it does |
|---|---|---|
| `scenario <preset> [knobs] [ids]` | server | Stages a preset and starts the battle. Refuses while a listed player is still in a battle mission (leave it and finish the post-battle screens first), and heals each listed player's hero to full. |
| `scenario_status` | client | Preset steps, knobs (`entry`, `ram`, `reserve` and `drop` included) and `battle.hulls[]` with owner, AI flag, weapon and ram. |
| `inspect [summary\|brief]` | client | `summary` (or `brief`) prints a `NAVAL_INSPECT_SUMMARY` header (instance, own id, host, epoch, deployment, hull count, the deck puppets' last/max deck error, DEBUG `copyOnCopy`) and one line per hull: the 8-digit id that the hull commands take, hull type, owner (`(npc)` for an AI hull), authority, HP, sail HP, `afloat`/`sinking`/`sunk`, `copy=own\|steered\|written`, and in DEBUG the copy's drift mean/max and `fatesSent`. Without it, the full JSON: session, host epoch, hull streams (per hull `followsHost`, `captor`, `captureSequence`, `orphan`, `side`, `formation`), `shipCapture` (`captureRequests`, `captureRejections` by reason, `captureExpired`, `pendingRequests` with `waitedSeconds`, and `applied`: `capturesApplied`, `recordsApplied`, `confirmsDropped`, `duplicateConfirms`, `captureConflicts`, `heldConfirms`), `ships[].captureTimer`, damage, weapons, decks, stations, AI fleet, end tally, agents, ships; DEBUG adds `copyProbe`: steering knobs, drift (against the owner's estimated current pose, the latest sample moved on by its age without the lead) and correction per copy, `oscillations10s`/`oscillationsTotal` (sign changes of the along-heading position error to the target, low-passed over the steering's `tau`, outside a 0.1 m deadband; the raw error flips at each sample arrival as the target jumps by the owner's frame-time jitter times its speed, R43), `targetJump` (how far the target moved at each sample arrival), steering cost (`steerMeanMs`, `steerP99Ms`, `steerMaxMs`) with `gcCollections10s` per generation, contact counters, the deck puppets' `maxDeckError` (the largest hull-frame puppet-to-target error, skipping a teleport's or a new deck placement's sample, counted in `deckSamplesSkipped`), `rams[]` (`hasRam` per hull) and the ram counters (`ramDropped`, `ramPuppetsSpared`); and `ships[].fatesSent`, the campaign ship fates this client sent for the hull (M6). |
| `fleet [party]` | server and client | Lists each ship of a party on this machine: its id, HP, sail HP and owner. `party` is a controller id, a `MobileParty`/`PartyBase` id, or a party or leader name or id (`Player`, `Player3325`; player parties first, then lord parties), on clients too; without it, the main party on a client and every player's party on the server. Run it before and after a battle on every machine. |
| `finish_loot [take=<ship>[,<ship>...]\|none\|all]` | client | Finishes the open post-battle loot screens with their own buttons: the party screen takes every troop and prisoner then Done, the inventory takes every item then Done, the ship screen selects and takes each chosen ship (a `Ship_...` id, with or without `Ship_`, or a unique prefix of one) then Confirm, leaving the rest (`take` defaults to `none`). Each confirmation is answered yes. So `PortScreenChangesHandler` takes the ships and `OnPlayerLootShipEnd` sends `NetworkDiscardLootShips`, which releases the M6 loot hold. The next screen usually opens on a later frame, so run it again until it reports no loot screen. Still manual: the captured-lord conversation (`FreeOrCapturePrisonerHeroes`) before the party screen. |
| `win`, `lose` | battle host | Ends the battle as a victory or defeat for the host's side; peers follow. |
| `damage <ship> <amount>`, `sink <ship>` | client | Hits a hull as a ship weapon would; another owner's hull takes it through its owner. |
| `reswap <ship>` | battle host | Replaces an AI hull without a migration. |
| `helm <rudder> <row> <sail> [seconds]` | client | Drives the own hull. |
| `try_helm <ship> [force]` | client | Has the main agent try a hull's helm as the use key would: vanilla's `CanUseObject` (the helm's `IsDisabledForPlayers`), a free seat and `IsAbleToUseMachine`, then `HandleStartUsingAction`, where `ForeignHelmUsePatch` refuses a copy's helm unless the copy is an enemy (to capture it; an orphan has no team, so it stays refused). One `NAVAL_TRY_HELM` line with `enemy`, `copy`, `canUseObject`, `vacant`, `ableToUse` and `result=REFUSED_BY_VANILLA\|BLOCKED\|ALLOWED`. An enemy helm is only offered once vanilla would allow a capture (the hull vacant and connected), so `force` skips vanilla's gates to test the co-op block directly (a friendly copy or an orphan reads `BLOCKED`). An allowed seat is released again. |
| `rope_list`, `rope_throw`, `rope_status` | client | Lists, throws and shows ropes. |
| `copy_steer [key=value...]` | client | Shows or tunes the steered copies' knobs here (`gain window softzone clamp tau lead snap`), live; a tuning holds across battles until this client leaves the session. |
| `water_clock` | client | DEBUG: one `NAVAL_WATER_CLOCK` line with the water clock patch status, the written and vanilla water time and their offset; in a naval battle also the host role, host samples and the offset estimate. |
| `water_probe start [grid=5] [spacing=40] [seconds=120] \| status \| stop` | client | Samples the water grid for the shared-clock test; each tick logs every hull's `x`, `y`, `yaw` (degrees, counter-clockwise from +x), `z`, `pitch`, `roll`, the water under it and the crew load its physics takes: `crewMass` (kg), `crewLocal` (body-frame centre, null for none or an empty deck) and `crewSrc` (`owner` on a copy using its owner's load, otherwise `vanilla`). `start` is refused until deployment has finished. |
| `board_copy [ship] \| status` | client | Teleports the main agent onto a copy's deck (the nearest copy, or the one whose ship id starts with `ship`) at vanilla's deck spot: the hull's player standing point, else the middle inner deck frame (`MissionShip.GetMiddleInnerSpawnGlobalFrame`). Reports the hull-frame `deckError` and `heightAboveSpot`; `status` reports the hull the main agent stands on. The copy's owner reports the puppet's `maxDeckError` in `inspect`. |
| `immortal [on \| off]` | client | Sets the main agent's vanilla mortality state to `Immortal` (`on`) or `Mortal` (`off`), so blows, routed ones included, take no health; reports it without an argument. A ram's 2 m kill radius (`Agent.Die`) still kills it. `on` with no living main agent arms it, and the naval behavior applies it on the first tick the main agent lives (R47: the hero was killed mortal seconds after spawning). |
| `clear_crew <ship>` | hull's authority | Kills the hull's formation's units (the main agent excepted) and empties its reserve through vanilla, so the deaths replicate and the hull is vacant for a capture; vanilla's vacancy query refreshes within 5 s. |
| `capture <ship> [force [twice]] \| status <ship>` | client | Moves the main agent to an enemy hull's helm (or this client's own orphan's) and takes it the way the use key does, so vanilla's 3 s timer runs and its end goes to the hull's authority; refuses with the helm gate that fails. `status` reports the authority, captor, orphan flag, `CaptureTimer`, how long this client's request waited and every helm gate (`enemy`, `vacantHere`, `connected`, `usableByMain`, `disabledForPlayers`). `force` publishes what vanilla's elapsed timer publishes (`ShipCaptureTimerElapsed`) with the main agent's hull, past this copy's helm gates, so the authority's own refusal (`NotVacant`, `Busy`, `SwapUnavailable`) answers it; a request already waiting for the hull is reported and not sent again. `force twice` forces two in the same frame, so the second meets the first one still waiting (one `NAVAL_CAPTURE_TWICE` line with both outcomes). |
| `swim <ship>` | client | Leaves any station and puts the main agent on the water 4 m off the hull's starboard side, midway along it, so swimming, follow-me orders from the water and a swimmer's capture vacancy can be staged. |
| `order <followme\|charge\|hold>` | client | Issues the order through the player's order controller, as the order menu does. Follow-me goes to the formations vanilla's select-all picks: every selectable formation except the one whose hull the main agent captains, because vanilla's `SetNavalFollowOrder` skips the hull being followed. So a ship follow order only shows on a second hull of the player's; with one hull, follow-me leaves its ship order unchanged. Charge and hold go to the main agent's formation alone. One `NAVAL_ORDER` line with the main formation's movement order, its hull's ship order, the main agent's formation ship, whether it swims and `selected=[formation:ship=N:shipOrder=X,...]` for every selected formation; a throw inside vanilla's order is reported as `threw=`. Vanilla's naval hold (`NavalOrderController.SetOrder(StandYourGround)`) only stops the ships (`SetNavalStop`), so a shipless, swimming formation keeps its earlier movement order (R49: `movementOrder=Charge` after `charge` then `hold`); that's the formation's real order, not a misprint. |
| `fire <weapon-ship> <target-ship> [status]` | client simulating the weapon | Snaps the weapon hull's ship ballista onto the target hull's `ShipTargetMissionObject` and calls vanilla's `Shoot`, so the bolt goes out through the same capture and replication path as an AI shot (`shipWeapons.shotsSent` here, `shotsReplayed` on peers). Needs a gunner and a loaded weapon (`Idle`). `status` shoots nothing and reports state, ammo, gunner, `disabledForAi` (an enemy within the 7 m stop range), distance, `inArc` (`CanShootAtPoint`) and `aiWouldShoot` (`CanShootAtThreat`: line of fire, friendly hulls, the nearest agent on a flat shot). |

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
re-issue count and whether the fallback ran. A failed engage, or one a newer scenario replaced, gives the fleet its campaign
AI back (`DoNotMakeNewDecisions` off).

`blockade` prefers a free lord already at war with the town's and the players' factions. Failing that, it takes the nearest
free lord of a third faction and declares the missing wars with `DeclareWarAction.ApplyByDefault`, logged as
`declaredWar=`. It no longer needs wars set up by hand on a fresh campaign. `town=id` still picks the town.

Knobs (`NavalScenarioResolver.Usage`):

- `hulls=n` AI hulls;
- `hull=id` hull type;
- `spacing=m` boarding or ramming gap;
- `ships=n` ships per player;
- `delay=s` before the scripted step. The sink, disconnect and retreat steps count it from the end of that client's
  deployment, which the scenario's own Ready press brings a few seconds after the mission opens, so give a weapon or a
  fight time to happen with a larger delay. `late_join` counts it from staging;
- `reconnect=s` for `migration`;
- `who=host|nonhost` who disconnects;
- `drop=hard|leave` for `migration`: `hard` (default) has the server drop the player's connection at once, the real
  disconnect path; `leave` goes to the main menu, which the battle sees as a leave. Who drops is fixed by the first host
  each client sees, so the successor never drops too. The disconnect step is done once the leave or drop request ran, and
  fails (with the rejoin) when its console command is missing. A player that reconnects while the battle runs gets the
  scenario again with its disconnect done and re-enters through Attack, or presses Ready if a mission start already
  pulled it in;
- `town=id` for `blockade`;
- `weapons=n` (0 to 4) arms the first n ships of every player and of the AI fleet with `fore_ballista`, swapping in a Battle
  Knarr where a hull has no fore slot.
- `ram=n` (0 to 4) fits `bow_ram_lvl1` on the first n ships without a ballista of every player and of the AI fleet
  (not the `encounter` or `blockade` enemy), swapping in an `empire_medium_ship` where a hull has no bow slot. `ramming`
  defaults to `ram=1`.
- `reserve=n` (0 to 200) gives every player, and the AI fleet a start_naval preset fights (not the `encounter` or `blockade`
  enemy), n troops in hull reserve to reinforce, for example after a capture's orphan is removed (`boarding hulls=2
  reserve=20`, `duel ships=2 reserve=20`). It runs after `weapons=n` and `ram=n`, which swap hulls. Vanilla fields at most
  the ships' total crew (`GetMaximumDeployableTroopCountForTeam`) and puts the troops past the deck crew below deck as each
  hull's reserve, so a troop past the total crew never fights in vanilla either. The default hull and the Battle Knarr
  carry their whole crew on deck, so the stager swaps plain roomless ships for an `empire_heavy_ship` (40 places below
  deck) until n fit, then tops the healthy troops (wounded don't count) up to the deck crew plus n. A ballista or ram hull
  is never swapped, so `hulls=1 weapons=1 reserve=20` has no room and logs a Warning; add a hull.

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

- Don't use `-h`. It fires only when `IsHungAppWindow` reports a hung window: it false-fired on a busy loading screen, and
  it never fires on a crash, because the TaleWorlds watchdog (attached as the debugger) holds the unhandled exception and
  freezes every thread while its crash dialog waits (R40 3 minutes, R49 9 minutes, `Responding=False` at 0 CPU).
- `-e` is refused while the watchdog is attached. The watchdog takes its own crash dump once the dialog is answered.
- Watch `C:\ProgramData\Mount and Blade II Bannerlord\logs\watchdog_log_<pid>.txt` (copied into each case's `logs/`): the line
  `Crash occurred. Asking for dump.` means a crash, not a hang. Take `procdump64 -accepteula -ma <pid>` at once, while
  the process is frozen at the fault, then leave the dialog to the user, whose answer lets the watchdog write its dump
  with the exception record.
- Without that line, a client `Coop_client.log` silent for 15 s during a battle (it logs every second) is the freeze
  signal: take two `-ma` dumps a few seconds apart and check the watchdog log again.
- `dotnet-dump` only shows managed frames. A native crash needs cdb: `cdb -z <dmp> -c "!analyze -v; ~0k; q"`.
