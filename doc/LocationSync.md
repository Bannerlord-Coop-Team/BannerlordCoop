# Location Sync in BannerlordCoop

Synchronizing **settlement interiors** (taverns, town centers, arenas, dungeons, lords' halls)
in a way that keeps the campaign map server-authoritative while letting the players who share
an interior run that scene **peer-to-peer**.

§1 is the mental model; §2 is the vanilla control flow; §3 is what breaks in coop; §4 the
design principles; §5 the target architecture (the seam between the two existing stacks); §6
the concrete service layout and message flow; §7 edge cases; §8 the roadmap.

> **Design decisions baked into this draft** (confirmed 2026-06-13):
> 1. **Host-owned NPCs.** Inside an instance, a designated host peer owns/simulates all NPC
>    agents and ambient AI; player agents are owned by their own clients. The campaign server
>    only sets the *roster* (who is in the scene), not the per-frame simulation.
> 2. **Server co-hosts the introducer.** The NAT-punch rendezvous role currently in the
>    standalone `IntroServer` is folded into the dedicated Coop server. It is the natural
>    matchmaker because it already controls the overworld and knows who is co-located.
> 3. **Server-issued instance GUID.** The server mints a unique instance id when the first
>    player enters a given settlement+location and broadcasts it; everyone entering that
>    location joins that id.

---

## 1. The core mental model

The campaign map is **one shared world**. A settlement interior is a **transient, local scene**
that only matters to the handful of players standing in it. These two facts pull in opposite
directions, so we run them on two different transports:

> **The server owns the overworld. The players in an interior own the interior.**
> The server decides *what* an interior contains (roster of `LocationCharacter`s, special
> items, who is allowed in); the co-located players decide *what happens inside it* (agent
> movement, combat, board games, conversation flow) over a direct P2P mesh.

This is the same "client-authoritative for local action, server-authoritative for world
mutation" split that [`PlayerEncounterSync.md`](PlayerEncounterSync.md) §4 establishes for
battles — applied to settlement interiors. The interior is allowed to diverge from frame to
frame across clients (each renders its own scene), but any outcome that mutates campaign state
(gold spent, troops recruited, relation/quest changes, items bought) must round-trip to the
server exactly like every other world mutation.

What must be shared, and who owns the truth:

| Concern | Owner | Transport | Status |
|---|---|---|---|
| Which `LocationCharacter`s populate a location | **Server** | Coop `INetwork` | **Done** — `Locations` service |
| Location special items | **Server** | Coop `INetwork` | **Done** — `Locations` service |
| Settlement enter/leave of a party | **Server** | Coop `INetwork` | **Done** — `ServerSettlementExitEnterHandler` |
| Instance identity (which P2P group) | **Server** | Coop `INetwork` | **Missing** — this doc |
| NAT introduction / rendezvous | **Server** (co-hosted) | LiteNetLib NAT punch | **Exists, standalone** — `IntroServer` |
| Player agent transform / combat | **Each player** | P2P `LiteNetP2PClient` | **Exists, test-only** — `Missions` |
| NPC agents + ambient AI inside the scene | **Host peer** | P2P `LiteNetP2PClient` | **Missing** — this doc |
| Board games / arena duels | **Host / participants** | P2P `LiteNetP2PClient` | **Exists, test-only** — `Missions` |
| Conversation *consequences* (recruit, trade, quest, relation) | **Server**, per-domain | Coop `INetwork` | per-domain services |

---

## 2. The two existing stacks (ground truth)

### 2.1 Overworld — server-authoritative (`Coop.Core`)

`CoopServer` / `CoopClient` over `CoopNetworkBase` (LiteNetLib). Each connection runs a
state machine:

```
CreateCharacter → ResolveCharacter → Load → Campaign ⇄ Mission
```

- [`MissionState`](../source/Coop.Core/Server/Connections/States/MissionState.cs) already
  represents "this peer is in a mission" and transitions back to `CampaignState` on
  `NetworkPlayerCampaignEntered`. Its `EnterMission()` is currently a no-op — **this is the
  hook the interior handoff plugs into.**
- [`ServerSettlementExitEnterHandler`](../source/Coop.Core/Server/Services/MobileParties/Handlers/ServerSettlementExitEnterHandler.cs)
  already broadcasts party enter/leave of a settlement to all peers. **The instance-assignment
  logic belongs alongside this**, because settlement entry is the moment the server learns a
  player is heading for an interior.

### 2.2 Interior — peer-to-peer (`Missions`)

[`LiteNetP2PClient`](../source/Missions/Services/Network/LiteNetP2PClient.cs) is a full NAT
hole-punch client: it connects to a rendezvous server, calls `NatPunchModule.SendNatIntroduceRequest`
with a [`ConnectionToken`](../source/Common/Network/Data/ConnectionToken.cs), and on
`OnNatIntroductionSuccess` dials the peer directly. Peers are grouped by an **instance string**.

- The rendezvous is [`MissionTestServer`](../source/IntroServer/Server/MissionTestServer.cs)
  in the standalone `IntroServer` project. `OnNatIntroductionRequest` introduces every existing
  peer in the same instance to the newcomer (a full mesh per instance).
- [`CoopMissionNetworkBehavior`](../source/Missions/Services/Network/MissionNetworkBehavior.cs)
  kicks off the punch in `OnRenderingStarted` using **`Mission.SceneName` as the instance key**.
- [`CoopTavernsController`](../source/Missions/Services/Taverns/CoopTavernsController.cs) spawns
  the local player's agent, exchanges `NetworkMissionJoinInfo` on `PeerConnected`, and spawns a
  networked agent per remote peer.
- NAT punch is proven by [`NatPunchSanityTests`](../source/MissionTests/NatPunchSanityTests.cs).

> **This stack currently only runs through [`TavernsGameManager`](../source/Missions/Services/Taverns/TavernsGameManager.cs)**,
> a standalone harness resolved only in `MissionTestMod`. It hard-codes `town_ES3`, force-starts
> a `TownEncounter`, opens the `tavern` indoor mission, and uses the scene name as the instance.
> It is **not wired into the live Coop campaign.** Building that wiring is the bulk of this work.

### 2.3 The bridge that already exists — `GameInterface/Services/Locations`

[`LocationHandler`](../source/GameInterface/Services/Locations/Handlers/LocationHandler.cs)
syncs **which** `LocationCharacter`s and special items populate a settlement's locations, with
snapshot reconciliation ([`Handle_NetworkLocationRosterSnapshot`](../source/GameInterface/Services/Locations/Handlers/LocationHandler.cs))
that diffs synced entries against the authoritative roster so a running mission isn't disrupted.
This is decision #1 in action: **the server already sets the roster.** What's missing is the
handoff that turns "I have the roster" into "I'm now in a P2P session with the other players who
have the same roster."

---

## 3. What breaks in coop (the gap)

1. **No seam.** Entering a settlement interior in the live campaign opens a vanilla, single-player
   mission. Nothing connects that to the P2P stack. The handoff
   (`campaign → instance → campaign`) does not exist outside the test harness.
2. **`Mission.SceneName` is not a unique instance key.** Two groups in two different towns' taverns
   share the same scene name and would be introduced into each other's mesh by the rendezvous.
   → server-issued instance GUID (decision #3).
3. **No authority model inside the instance.** The test harness only spawns player agents per peer.
   Tavern NPCs, ambient AI, wanderers, and arena fighters have no owner, so they would either be
   absent, duplicated (each client spawns its own), or diverge. → host-owned NPCs (decision #1).
4. **Two transports, one `GameInterface`.** `GameInterface` handlers resolve `INetwork` from
   whichever Autofac module wired them; `LiteNetP2PClient` is registered `As<INetwork>` in
   `MissionModule`, while `Coop.Core` registers `CoopNetworkBase`. A handler that must talk to the
   server (campaign mutation) and one that must talk to peers (interior action) need to resolve
   **different** `INetwork`s. The instance layer must not accidentally send interior chatter over
   the campaign socket or vice-versa.
5. **Rendezvous is a second process.** The introducer lives in `IntroServer`. For the live game the
   server should host it (decision #2) so matchmaking, instance assignment, and the punch token all
   originate from the same authority in one flow.
6. **NAT punch can fail** (symmetric NAT on both ends). The introducer is purely an introducer; there
   is no relay fallback today (see §7).

---

## 4. Design principles

Reuse the framework shape from `PlayerEncounterSync.md` §4, with one addition — a transport
selector:

- **Messages**
  - *Local request*: `readonly struct X : IEvent` published on the broker by a Harmony patch.
  - *Network command*: `[ProtoContract] readonly struct NetworkX : ICommand` carrying
    object-manager string ids.
- **Two network scopes.** Campaign-authoritative messages go over the **Coop `INetwork`**
  (`SendAll` reaches all peers via the server). Interior messages go over the **P2P `INetwork`**
  (`LiteNetP2PClient`, reaching only the peers in this instance's mesh). A handler declares which
  it needs by the `INetwork` it resolves; we keep the two registrations distinct rather than
  unifying them, so the scope is explicit at the wiring layer.
- **Server is the matchmaker.** The server assigns the instance id, decides who is allowed to punch
  into it, hosts the introducer, and elects the instance host. Clients never invent instance ids.
- **Host owns NPCs, clients own their player.** Inside the mesh, exactly one peer (the host)
  simulates NPC agents and broadcasts their state; each client simulates and broadcasts its own
  player agent. This mirrors the existing board-game host concept and keeps NPC AI from forking.
- **Interior outcomes that touch the campaign round-trip to the server.** Buying a drink, recruiting
  a companion, winning gold at a board game, finishing a quest dialogue — all go over the Coop
  `INetwork` to the per-domain service, never P2P-only.

---

## 5. Target architecture — the seam

### 5.1 Lifecycle (campaign → instance → campaign)

```
[Client A] enters settlement, walks into the tavern location
   └─ PartyEnterSettlementAttempted  ──Coop──►  [Server]

[Server] ServerSettlementExitEnterHandler + new InstanceCoordinator
   ├─ resolve (settlementId, locationId)
   ├─ instanceId = existing id for that (settlement,location) OR mint a new GUID
   ├─ record A as a member; elect host (first member = host)
   └─ Send(A, NetworkAssignInstance { instanceId, host?, rendezvousEndpoint, token })   ──Coop──►

[Client A] receives NetworkAssignInstance
   ├─ ConnectionLogic transitions A's mission state (server side: CampaignState → MissionState)
   ├─ open the interior mission locally (vanilla scene for that location)
   └─ LiteNetP2PClient.ConnectToP2PServer() ; NatPunch(instanceId)   ──P2P punch──►

[Server-hosted introducer] OnNatIntroductionRequest(instanceId)
   └─ introduce A to every other member already punched into instanceId  (mesh)

[Clients A,B,…] OnNatIntroductionSuccess → direct connect → PeerConnected
   ├─ exchange NetworkMissionJoinInfo (spawn each other's player agents)
   └─ host streams NPC roster agents + their per-frame state

[Client A] leaves the location / mission ends
   ├─ P2P mesh tears down (OnPeerDisconnected)
   ├─ NetworkPlayerCampaignEntered  ──Coop──►  [Server]
   └─ [Server] MissionState.EnterCampaign → CampaignState ; release instance membership
        └─ when last member leaves, retire instanceId
```

### 5.2 Instance identity & membership (server)

A new **`InstanceCoordinator`** on the server (sibling to `ServerSettlementExitEnterHandler`)
owns a map:

```
(settlementId, locationId)  →  Instance { Guid id, NetPeer host, HashSet<NetPeer> members }
```

- First member into a `(settlement, location)` mints the GUID and becomes host.
- `NetworkAssignInstance` tells the joining client its `instanceId`, whether it is the host, the
  rendezvous endpoint, and the punch `token` (reuse [`ConnectionToken`](../source/Common/Network/Data/ConnectionToken.cs),
  swapping the scene name for `instanceId`).
- On member leave / disconnect, drop membership. If the host leaves with members remaining,
  **re-elect** a new host and broadcast `NetworkInstanceHostChanged` so NPC ownership migrates
  (see §7 host-migration).
- When membership hits zero, retire the id.

This replaces `Mission.SceneName` as the punch key in `CoopMissionNetworkBehavior.OnRenderingStarted`.

### 5.3 Co-hosting the introducer (server)

Lift `MissionTestServer`'s `INatPunchListener` logic into a server-side `InstanceIntroducer`
component that shares (or sits beside) the existing server `NetManager` with `NatPunchEnabled =
true`. `PeerRegistry` / `P2PPeer` move from `IntroServer` into a shared assembly (or `Common`) so
both the live server and the standalone `IntroServer` can keep using them. The standalone
`IntroServer` stays for `MissionTests` / dev, but the live path no longer needs a second process.

### 5.4 Host-owned NPCs inside the instance (P2P)

- The roster the server already syncs (`Locations` service) tells **every** member which
  `LocationCharacter`s belong in the scene.
- Only the **host** actually drives those NPC agents (spawn, AI tick, movement) and broadcasts
  their transforms/animation over P2P, reusing the existing agent-movement/damage handlers
  (`AgentMovementHandler`, `AgentDamageHandler`) keyed by agent id.
- Non-host members spawn the same NPCs as **network-controlled** agents (the
  `RegisterNetworkControlledAgent` path already used for remote players in
  `CoopTavernsController.Handle_JoinInfo`) and mirror host updates.
- Player agents stay owned by their own clients regardless of host.

---

## 6. Concrete layout & flow

### 6.1 New / changed pieces

```
source/Coop.Core/Server/
├── Connections/States/MissionState.cs        // CHANGE: EnterMission() does the instance handoff
├── Services/Instances/                        // NEW service
│   ├── InstanceCoordinator.cs                 //   (settlement,location) → Instance; membership; host election
│   ├── InstanceIntroducer.cs                  //   server-side NAT introducer (lifted from MissionTestServer)
│   ├── Handlers/ServerInstanceHandler.cs      //   enter/leave → assign/retire; host re-election
│   └── Messages/
│       ├── NetworkAssignInstance.cs           //   ICommand server→client { instanceId, isHost, endpoint, token }
│       ├── NetworkInstanceHostChanged.cs      //   ICommand server→members { instanceId, newHostPeerId }
│       └── NetworkLeaveInstance.cs            //   ICommand client→server (or derived from campaign-entered)

source/Common/Network/                         // shared so both server & IntroServer use them
├── PeerRegistry.cs                            // MOVED from IntroServer
└── P2PPeer.cs                                 // MOVED from IntroServer

source/Missions/Services/
├── Network/MissionNetworkBehavior.cs          // CHANGE: NatPunch(instanceId) not Mission.SceneName
├── Instances/InstanceMissionController.cs     // generalize CoopTavernsController beyond taverns
└── Network/Handlers/InstanceHostHandler.cs    // NEW: owns NPC agents iff this peer is host; handles host migration
```

`GameInterface/Services/Locations/*` is **unchanged** — it already supplies the roster the host
spawns from.

### 6.2 Message flow (campaign scope vs P2P scope)

```
Coop scope (server-authoritative, via CoopNetworkBase):
  PartyEnterSettlementAttempted → NetworkAssignInstance
  NetworkLocationRosterSnapshot / NetworkAddLocationCharacter (existing)
  NetworkPlayerCampaignEntered (existing, exit)
  NetworkInstanceHostChanged

P2P scope (peer mesh, via LiteNetP2PClient):
  NetworkMissionJoinInfo (existing — player agents)
  Agent movement / damage / missiles (existing handlers)
  NPC agent state (host → members, reuses agent handlers)
  BoardGame / arena messages (existing)
```

### 6.3 Hooking `MissionState.EnterMission`

`EnterMission()` is currently empty. It becomes the server-side trigger: when a peer's connection
enters the mission state for a settlement interior, the `InstanceCoordinator` assigns/looks up the
instance and the handler sends `NetworkAssignInstance`. The return trip already works —
`NetworkPlayerCampaignEntered` drives `MissionState → CampaignState`; we add membership release in
that handler.

---

## 7. Edge cases / open questions

- **NAT punch failure (symmetric NAT).** No relay today. Options: (a) accept failure and keep the
  player in a solo local instance (degrade gracefully), (b) add a server-side relay fallback that
  forwards P2P packets when the punch fails. Recommend shipping (a) first, designing (b) as a later
  milestone. Either way the **client must detect** punch failure (timeout in `WaitForConnection` /
  no `OnNatIntroductionSuccess`) and fall back rather than hang.
- **Host migration.** Host leaves while members remain → server re-elects and broadcasts
  `NetworkInstanceHostChanged`. The new host must **adopt** the existing NPC agents (take over
  their simulation) rather than respawn them, to avoid a visible pop. Until adoption lands, the
  simplest correct fallback is: new host re-derives NPCs from the server roster, others clear and
  respawn — visible but consistent.
- **Two members enter "simultaneously."** Both could be "first." The server serializes membership
  on its own thread, so exactly one mints the id; the second gets the existing id. No client-side
  race because clients never mint.
- **Roster changes while the instance is live.** Already handled by
  `Handle_NetworkLocationRosterSnapshot`'s diff/reconcile — but only the **host** should apply
  roster-driven *spawns* (others mirror via P2P), otherwise non-host clients double-spawn. The
  reconcile currently runs on every client; gate the spawn side to the host inside an instance.
- **Campaign mutations from inside the instance.** Trade, recruit, gold, quests must go over the
  Coop `INetwork` to their per-domain services. Confirm none of the interior controllers shortcut
  these P2P-only.
- **Player agent identity vs campaign hero.** `NetworkMissionJoinInfo` carries a `CharacterObject`
  and a fresh per-mission `Guid`; we need the player's **campaign hero id** to correlate the
  interior agent with the overworld party (for conversations, "talk to player X"). Add the hero id
  to the join info or to `NetworkAssignInstance`.
- **Disconnect mid-instance.** P2P `OnPeerDisconnected` removes the agent locally; the server must
  also drop instance membership when the campaign connection drops, so a stale member doesn't keep
  an instance alive.
- **Non-tavern interiors.** Town center, arena, dungeon, lord's hall, prison — same seam, different
  scene + controller. `CoopTavernsController` should be generalized to an `InstanceMissionController`
  parameterized by location, rather than copied per location.

---

## 8. Roadmap

Milestones, smallest shippable first:

1. **Instance identity over Coop.** `InstanceCoordinator` + `NetworkAssignInstance`; client uses the
   server-issued GUID as the punch key instead of `Mission.SceneName`. No behavior change yet beyond
   correct keying — verify two groups in two towns don't cross-connect.
2. **Live tavern handoff for players only.** Wire `MissionState.EnterMission` → assign → client opens
   the real tavern mission and punches; players see each other's agents. NPCs spawn locally from the
   roster, not yet behavior-synced. (This is decision #1's "no NPC sync at first" as an interim step
   toward host-owned.)
3. **Co-host the introducer in the server.** Lift `MissionTestServer` logic into `InstanceIntroducer`;
   move `PeerRegistry`/`P2PPeer` to shared. Drop the second-process requirement for the live game.
4. **Host-owned NPCs.** Host election, host drives NPC agents over P2P, members mirror; gate
   roster-driven spawns to the host.
5. **Host migration + disconnect cleanup.** Re-election, NPC adoption, membership release on drop.
6. **NAT failure handling.** Detect + degrade to solo instance; relay fallback as a stretch.
7. **Generalize beyond taverns.** `InstanceMissionController` for town center / arena / etc.

> Cross-reference: the campaign-side battle/encounter analogue is documented in
> [`PlayerEncounterSync.md`](PlayerEncounterSync.md); settlement *entry* messaging lives in the
> `MobileParties`/`Settlements` services and is reused here unchanged.

---

## 9. Implementation status (as of this draft)

What is **in the code and tested** (compiles green across `Common`, `Coop.Core`, `Missions`;
`Coop.Tests` 143 passing incl. 8 new `InstanceCoordinatorTests`):

- **Server instance coordination** — `Coop.Core/Server/Services/Instances/`:
  `InstanceCoordinator` ((settlement,location) → server-issued `Guid`, join/leave, host election +
  re-election), `Instance`, and `ServerInstanceHandler` bridging the broker ↔ network. Registered in
  `ServerModule`. Peer identity is **reference-based** (`NetPeer` compares by endpoint, which would
  conflate distinct peers behind one NAT — see the test that caught this).
- **Instance messages** — `NetworkEnterLocation` (client→server), `NetworkAssignInstance`
  (server→client), `NetworkInstanceHostChanged` (server→new host). Auto-registered for protobuf via
  the `[ProtoContract]` reflection scan.
- **Server NAT introducer** — `CoopServer.OnNatIntroductionRequest` (previously `throw
  new NotImplementedException()`) now delegates to `InstanceIntroducer`, a self-contained per-instance
  endpoint registry that introduces co-located peers. Driven purely by introduction requests (no
  second connection / peer pre-registration). **Decision #2.**
- **Cross-container bridge** — both `Coop.Core` and `Missions` share `MessageBroker.Instance`, so the
  seam is a set of **local broker events in `Common`** (`EnterLocationRequested`, `InstanceAssigned`,
  `InstanceHostChanged`, `InstanceCleared`) plus `InstanceContext` (process-wide singleton holding
  `CurrentInstanceId` / `IsHost`, subscribed at module load). `ClientInstanceHandler` (Coop.Core)
  translates network ↔ local both ways.
- **Punch key swap** — `CoopMissionNetworkBehavior.OnRenderingStarted` now punches with
  `InstanceContext.CurrentInstanceId` when in an instance, falling back to `Mission.SceneName` for the
  standalone test harness. **Decision #3** (no more cross-town scene-name collisions).
- **Host foundation** — election/re-election + `NetworkInstanceHostChanged` + `InstanceContext.IsHost`
  + membership release on `PlayerDisconnected` / `NetworkPlayerCampaignEntered`. **Decision #1's plumbing.**

**Experimental, compiles, NOT yet verified in-game** (built to run + report back; heavy
`[LocationSync]` logging, each risky step tagged `(Step: …)` / `REPORT THIS`):

- **Trigger (Piece 1)** — `GameInterface/Services/Locations/`: `PlayerLocationEntryPatches` postfixes
  both `SandBoxMissions.OpenIndoorMission` overloads → `PlayerEnteredLocation`;
  `LocationInstanceRequestHandler` resolves ids → `EnterLocationRequested` → (existing)
  `ClientInstanceHandler` → `NetworkEnterLocation` to the server.
- **Live P2P bridge (Piece 2)** — `Missions/Services/Network/LiveInstanceLauncher.cs`, activated by one
  line in `Coop/CoopMod.cs` (the live module now references `Missions` + `IntroServer`). On
  `InstanceAssigned`: registers ProtoBuf surrogates, builds a `MissionModule` container, applies the
  Missions Harmony patches, points the rendezvous at the campaign server (new
  `NetworkConfiguration.SetRendezvous`, which DNS-resolves hostnames like `localhost` to a numeric IP),
  **starts the P2P socket without connecting to the campaign server**, attaches
  `CoopMissionNetworkBehavior` + `CoopTavernsController` to `Mission.Current`, and punches.

  > Important: the P2P client must NOT open a LiteNetLib *connection* to the campaign server — NAT
  > punch is unconnected and the server's `NatPunchModule` answers it. An earlier version called
  > `ConnectToP2PServer()`, which registered a second peer on the campaign server and tore down the
  > player's real campaign session. `LiteNetP2PClient.OnPeerConnected` now treats a null `PeerServer`
  > (no rendezvous connection) as "every connected peer is a real punched-through peer".

Known risks the in-game log is built to expose:

1. **Attach ordering.** The instance is assigned *after* the interior mission has started, so the P2P
   behaviors are added post-`AfterStart`/`OnRenderingStarted` (local agent may not register; punch is
   issued manually). Likely fix: request the instance on settlement entry, or attach at mission creation.
2. **Rendezvous reachability.** Punch is aimed at the campaign server endpoint (answered by
   `CoopServer.OnNatIntroductionRequest`); whether the P2P socket reaches it there is unverified.
3. **Second `MissionModule` container** (re-pulls `GameInterfaceModule`) and **applying Missions Harmony
   patches** in live play may conflict with the campaign container/patches.
4. **Host-owned NPC simulation** + adoption on migration still unimplemented (host flag exists).
5. **NAT-punch failure fallback / relay** (§7) still unimplemented.
6. **Host-as-server-player** skipped (trigger gated to clients).

> Net: the **server-authoritative spine and the keying/identity/handoff contracts are done and
> tested**; the remaining work is the in-mission P2P bridge and NPC ownership, which need the running
> game to iterate against.

---

### Appendix: key entry points

| Symbol | Role |
|---|---|
| `LiteNetP2PClient.NatPunch(instance)` | client kicks off NAT introduce for an instance key |
| `LiteNetP2PClient.OnNatIntroductionSuccess` | direct-connect to a peer after punch |
| `MissionTestServer.OnNatIntroductionRequest` | rendezvous introduces members of an instance (→ `InstanceIntroducer`) |
| `ConnectionToken` | `peerId % instanceName % natType`; reuse with `instanceId` |
| `CoopMissionNetworkBehavior.OnRenderingStarted` | where the punch key is chosen (today: `Mission.SceneName`) |
| `CoopTavernsController.Handle_JoinInfo` | spawn a network-controlled agent for a peer (reused for NPCs) |
| `ServerSettlementExitEnterHandler` | settlement enter/leave broadcast (instance assignment sits beside it) |
| `MissionState.EnterMission` / `.EnterCampaign` | server-side instance handoff in/out (today `EnterMission` is empty) |
| `LocationHandler.Handle_NetworkLocationRosterSnapshot` | authoritative roster reconcile the host spawns from |
