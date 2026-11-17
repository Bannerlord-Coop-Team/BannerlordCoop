# Sessions: running a mod's own mission over Coop

A session is a co-op mission with no campaign. A mod hosts one, other players join it, and every member runs the mod's
own mission. Coop supplies the connection, the host election, the agent, damage, death and siege replication, and a
message channel. The mod supplies the mission itself.

Players have no hero, party or map presence in a session. The server loads no save and exits when the session closes.
The state machines are in [ClientAndConnectionStates.md](ClientAndConnectionStates.md).

## The entry point: `SessionLauncher`

`SessionLauncher` (`GameInterface/Services/Sessions/SessionLauncher.cs`) is the one type a mod uses. The message broker
holds its subscribers weakly, so the mod keeps the launcher in a field for as long as it uses it.

```csharp
launcher = new SessionLauncher(request => OpenMyMission(request.SessionId, request.Payload));
launcher.HostChanged += changed => { /* run authoritative logic while changed.IsLocalHost */ };
launcher.MissionEndRequested += ended => { /* end the mission and return to the main menu */ };
```

| Member | What it does |
|--------|--------------|
| `Host(modId, payload, password, visibility)` | On Steam, spawns a session server process and joins it. Off Steam, this instance becomes the server. `payload` is opaque to Coop, at most `AttemptHostSession.MaxPayloadBytes`, and reaches every member |
| `Join(address, port, password)` | Joins a hosted session |
| `CreateMissionBehaviors(sessionId)` | The behaviors the mod adds to its mission. Empty without a running Coop session |
| `Close()` | Asks the server to close the session. False unless this client is the host |
| `IsLocalHost` | This client is the elected host |
| `Channel` | The mod channel, below |
| `openMission` callback | Raised on every member after it joined. The mod opens its mission from the payload |
| `HostChanged` | The host was elected or changed. Carries the controller id, the epoch and `IsLocalHost` |
| `MemberJoined` | This client heard of another member entering and replayed its state to it. Once per peer link: on each existing member once for the newcomer, and on the newcomer once per existing member |
| `MissionEndRequested` | The session ended, either closed by the host or because the server was lost. The mod ends its own mission |

## Lifecycle

1. The host calls `Host`. Coop starts the session server (see the table for the Steam and non-Steam split).
2. Each member receives `NetworkSessionJoined` and the launcher calls `openMission` with the session id, the mod id and
   the payload.
3. The mod opens its mission with the behaviors from `CreateMissionBehaviors`. When the mission has loaded, the first
   member to report ready becomes the host. A host that leaves promotes the next member in the successor line, and
   `HostChanged` is raised with a higher epoch.
4. The host calls `Close`, or the server is lost. Every member gets `MissionEndRequested`.

## The mod channel

`launcher.Channel` sends the mod's own messages to the other members of the live session mission, reliably and in order.

- `SendToSession(message)` reaches every other member. `SendToController(controllerId, message)` reaches one.
- Both return false when no session mission is live.
- A message is a `[ProtoContract]` type that implements `IMessage` (or `IEvent`). It can be defined in the mod's own
  assembly. Subscribers get it on the message broker like any mission message.
- Channel messages carry no host epoch. Put `SessionHostChanged.Epoch` and the session id in your own messages, and drop
  a message from an older epoch or another session.

## Rules for the mod's mission

- Pass the behaviors from `CreateMissionBehaviors` in the `MissionState.OpenNew` list, or add them in
  `OnBeforeMissionBehaviorInitialize`.
- The mission needs a `MissionCombatantsLogic`. Coop reserves the ally team for the other players through it.
- The mission owns its end condition and places its own siege engines.
- Only the host spawns shared AI. Every member's `SpawnAgent` is replicated as an agent that member owns.
- Troops are identified on the wire by their `StringId`, so every member must load the same module data.

## Out of scope

- Voice. It is not configured in a session.
- Campaign features: heroes, parties, map events, saves, battle results, supply and the deployment phase.
