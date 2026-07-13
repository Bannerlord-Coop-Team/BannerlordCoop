# Entering / Leaving a Location over the `IMeshNetwork`

How co-located players are connected while inside an interior (tavern, town centre,
castle courtyard, village) in BannerlordCoop, focused on the mission-scoped peer-to-peer
network, `IMeshNetwork`.

---

## 1. The core mental model

A location interior is **owned locally by the player who walks into it**. The campaign
server has no main party strolling into a tavern, so it never opens an interior mission
for itself. Two players who happen to be in the same settlement location are connected
to each other directly — not through the campaign server — over a separate, mission-scoped
P2P network.

That network is `IMeshNetwork` (concrete implementation `LiteNetP2PClient`). It is
deliberately distinct from `CoopClient`'s campaign `INetwork`:

> The mesh network is registered `As<IMeshNetwork>` (**not** `As<INetwork>`) in
> [`MissionModule`](../source/Missions/MissionModule.cs) so mission services bind to it
> explicitly and it does not collide with `CoopClient`'s `INetwork` in the shared client
> container.

Key properties:

- **Pure NAT punch** in the live co-host path — `ConnectToInstance` sends a NAT introduce
  request; no connection to the server is opened (`PeerServer` is null). The campaign
  server is used only as the rendezvous, pointed at via `SetRendezvous`.
- **The instance id is computed locally** as `settlementId|locationId`. Both co-located
  clients independently derive the same id, so no assignment round-trip is needed — the
  server creates the instance on the first NAT punch.
- **Exit is a full teardown** (`Stop()`), but reliable sends are flushed first so the
  graceful `NetworkLeaveMission` broadcast reaches peers before `DisconnectAll` cuts the
  link. The timeout path is the ungraceful fallback.

---

## 2. Flow diagram

```mermaid
sequenceDiagram
    autonumber
    actor Player as Local Player
    participant Game as SandBoxMissions
    participant Patch as PlayerLocationEntryPatches
    participant Ctrl as CoopTavernsController<br/>(ILocationMissionBehavior)
    participant Mesh as IMeshNetwork<br/>(LiteNetP2PClient)
    participant NAT as Rendezvous / NAT-Punch<br/>(campaign server)
    participant Peer as Remote Co-host Peer

    rect rgb(230, 245, 230)
    note over Player, Peer: ENTERING A LOCATION
    Player->>Game: Open location (OpenIndoorMission / TownCenter / Castle / Village)
    Game-->>Patch: Harmony postfix Handle(scene, location, mission)
    note right of Patch: returns early if IsServer
    Patch->>Ctrl: AttachLocationBehaviors(mission)<br/>resolve from shared container, AddMissionBehavior
    Patch-)Ctrl: publish PlayerEnteredLocation

    Ctrl->>Ctrl: Handle_PlayerEnteredLocation<br/>(guard _instanceRequested, resolve ids)
    Ctrl->>Mesh: SetRendezvous(campaign Address, Port)
    Ctrl->>Mesh: Start() — bind socket + poller
    Ctrl->>Mesh: ConnectToInstance("settlementId|locationId")
    Ctrl->>Ctrl: agentRegistry.Clear()

    Mesh->>NAT: SendNatIntroduceRequest(token = controllerId|instanceId|natType)
    NAT-->>Mesh: OnNatIntroductionSuccess(targetEndPoint)
    Mesh->>Peer: netManager.Connect(targetEndPoint, token)
    Peer-->>Mesh: OnConnectionRequest (accept if instanceId matches)
    Mesh-->>Ctrl: OnPeerConnected ⇒ publish PeerConnected

    Ctrl->>Mesh: Handle_PeerConnected ⇒ Send(peer, NetworkMissionJoinInfo)
    Player-->>Ctrl: OnRenderingStarted ⇒ TryRegisterLocalAgent
    Ctrl->>Mesh: SendAll(NetworkMissionJoinInfo)  %% catch peers connected before subscribe

    Peer->>Mesh: OnNetworkReceive(join info)
    Mesh-->>Ctrl: packetManager ⇒ Handle_JoinInfo
    Ctrl->>Ctrl: SpawnAgent + RegisterNetworkControlledAgent<br/>(buffer if mission not ready)
    end

    rect rgb(250, 235, 235)
    note over Player, Peer: LEAVING A LOCATION
    Player->>Ctrl: OnEndMission
    Ctrl->>Mesh: SendAll(NetworkLeaveMission(controllerId))
    Mesh->>Peer: deliver LeaveMission (reliable)
    Ctrl->>Mesh: Stop()
    note right of Mesh: Stop() ⇒ DisconnectPeers():<br/>FlushReliableSends (drain queue ≤100ms)<br/>then DisconnectAll + MissionContext.EndInstance<br/>(drop instance membership), poller.Stop, netManager.Stop
    Ctrl->>Ctrl: agentRegistry.Clear() + Dispose()

    alt graceful leave
        Peer->>Peer: Handle_LeaveMission ⇒ MakeDead / FadeOut + RemoveNetworkControlledAgent
    else ungraceful drop / timeout
        Mesh-->>Peer: (no LeaveMission) connection times out
        Peer-->>Peer: OnPeerDisconnected ⇒ publish PeerDisconnected<br/>(AgentMovementHandler cleans up agent)
    end
    end
```

---

## 3. Concrete layout

| Concern | Type | File |
|---|---|---|
| Mesh network interface | `IMeshNetwork` | [`source/Missions/Services/Network/IMeshNetwork.cs`](../source/Missions/Services/Network/IMeshNetwork.cs) |
| Mesh network implementation | `LiteNetP2PClient` | [`source/Missions/Services/Network/LiteNetP2PClient.cs`](../source/Missions/Services/Network/LiteNetP2PClient.cs) |
| Entry trigger (Harmony postfix) | `PlayerLocationEntryPatches` | [`source/GameInterface/Services/Locations/Patches/PlayerLocationEntryPatches.cs`](../source/GameInterface/Services/Locations/Patches/PlayerLocationEntryPatches.cs) |
| Connection / join / leave owner | `CoopTavernsController` | [`source/Missions/Services/Taverns/CoopTavernsController.cs`](../source/Missions/Services/Taverns/CoopTavernsController.cs) |
| DI registration | `MissionModule` | [`source/Missions/MissionModule.cs`](../source/Missions/MissionModule.cs) |

---

## 4. Notes and edge cases

- **Attach before publish.** `PlayerLocationEntryPatches.AttachLocationBehaviors` adds the
  `ILocationMissionBehavior`s (`CoopTavernsController`, `CoopMissionNetworkBehavior`) to the
  freshly-opened mission *before* `PlayerEnteredLocation` is published, so the controller is
  alive and subscribed when it owns the instance request and join exchange. `OpenIndoorMission`
  fires several times per entry, so attachment is deduped via a `ConditionalWeakTable`.
- **`%` is reserved.** The instance id uses a `|` separator, not `%` — `ConnectionToken`
  serializes as `PeerId%InstanceId%NatType` and splits on `%`.
- **Join info can beat mission setup.** On a rejoin the kept-alive socket can deliver a peer's
  join info before the local interior mission has finished initializing teams/player agent.
  Early join info is buffered (`_pendingJoinInfos`) and drained once `TryRegisterLocalAgent`
  succeeds — spawning a remote agent into a not-yet-initialized mission corrupts team setup.
- **Network thread vs. main thread.** `Handle_JoinInfo` runs on the network thread, but
  `AgentBuildData`'s ctor and `SpawnAgent` touch TaleWorlds engine statics that must run on the
  main thread, so the entire build+spawn happens inside a `GameLoopRunner.RunOnMainThread` closure.
- **Leave reliability.** `FlushReliableSends` nudges the logic thread and waits (bounded to
  100 ms) for each peer's reliable queue to drain, so the queued `NetworkLeaveMission` is
  delivered before `DisconnectAll`. The `OnPeerDisconnected` / timeout path remains the fallback
  for ungraceful exits.
