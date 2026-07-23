# Syncing `PlayerEncounter` in BannerlordCoop

Synchronizing player encounters, battles, and conversations within the existing
GameInterface / Coop.Core message + network framework. §1–4 are the target design and mental
model; **§5 documents the current implementation** (in the `MapEvents` service as of commit
`a201b1f0`); §6–8 cover the concrete layout, edge cases, and remaining work.

---

## 1. The core mental model

`PlayerEncounter` is **not** a world object. It is a *per‑player, client‑side UI/flow
state machine* that wraps everything that happens between "my party bumped into
something on the map" and "I'm back to free‑roam on the map." In vanilla it is a
singleton:

```csharp
public static PlayerEncounter Current => Campaign.Current.PlayerEncounter;  // ONE per campaign
```

It is built entirely around three globals that are **different on every client**:

- `MobileParty.MainParty`
- `Hero.MainHero`
- `CharacterObject.PlayerCharacter`

That single fact drives the whole design:

> **Do not synchronize the `PlayerEncounter` object. Let each client run its own
> `PlayerEncounter.Current` for its own `MainParty`, and synchronize only the
> authoritative *world mutations* that an encounter produces.**

The world mutations that actually need to be shared are:

| Concern | Who owns truth | Already handled by |
|---|---|---|
| Creating / joining a `MapEvent` (battle) | **Server** | `MapEvents` service (`BattleHandler`) |
| Battle results, casualties, loot, prisoners | **Server** | partially `MapEvents` |
| Time / pause state | **Server** | `Time` service (`ITimeControlInterface`) |
| Conversation *consequences* (claim, recruit, relation, war/peace, quests) | **Server**, per‑domain | per‑field services (e.g. settlement claim) |
| The encounter menu UI, the conversation UI, the local state machine | **Each client, locally** | vanilla, run locally on each client (see §5) |

Everything else — opening a menu, opening a conversation, ticking the encounter
state machine — is **local** to the owning client and must only run for the player
whose `MainParty` is involved.

---

## 2. Vanilla control flow (the ground truth)

### 2.1 How an encounter is *started*

```
EncounterManager.Tick()                              // every campaign frame
  └─ HandleEncounters(dt)
       └─ HandleEncounterForMobileParty(party, dt)   // proximity / AiBehaviorInteractable check
            └─ party.Ai.AiBehaviorInteractable.OnPartyInteraction(party)
                 └─ EncounterManager.StartPartyEncounter(attacker, defender)
                      ├─ (player involved, no active encounter) RestartPlayerEncounter(def, atk)
                      │     ├─ PlayerEncounter.Start()                 // Campaign.Current.PlayerEncounter = new PlayerEncounter()
                      │     └─ Current.SetupFields(attacker, defender) // sets _encounteredParty, PlayerSide
                      └─ (AI vs AI, different factions) StartBattleAction.Apply(...)   // pure MapEvent, no PlayerEncounter
```

The **decision of what kind of encounter it is** is made by the menu model:

```csharp
string menu = Campaign.Current.Models.EncounterGameMenuModel
                      .GetEncounterMenu(attacker, defender, out bool startBattle, out bool joinBattle);
GameMenu.ActivateGameMenu(menu);   // "encounter_meeting", "encounter", "town_outside", "village_outside",
                                   // "army_encounter", "join_encounter", siege menus, "camp", ...
```

`PlayerEncounter.Init(attacker, defender, settlement)` (the internal overload) is the
"do everything" entry point: it calls `SetupFields`, runs `GetEncounterMenu`,
conditionally calls `StartBattle()` / `JoinBattle()` / `EnterSettlement()`, and finally
`ActivateGameMenu(menu)`.

> Key: `_encounteredParty` is **only** set by `SetupFields` (or by joining a battle).
> The parameterless `PlayerEncounter.Init()` only seeds fields when
> `MainParty.MapEvent != null`. A neutral meeting has no `MapEvent`, so it must be seeded
> explicitly with `SetupFields(attacker, defender)`.

### 2.2 How a battle is *started*

A battle is a `MapEvent`. The `PlayerEncounter` is just the menu/flow around it.

```
encounter menu, "attack" option consequence
  └─ PlayerEncounter.StartBattle()
       └─ StartBattleInternal()
            └─ creates / sets _mapEvent  (FieldBattleEventComponent.CreateFieldBattleEvent,
                                          MapEventManager.StartSiegeMapEvent,
                                          RaidEventComponent.CreateRaidEvent, etc.)
```

The **mission scene** (the actual fight) is a separate step, launched via
`CampaignMission.OpenBattleMission(record)`. In this mod that is already driven over the
network: see `BattleHandler.Handle_NetworkStartAttackMission`, which builds the
`MissionInitializerRecord` and calls `OpenBattleMission` on the client.

### 2.3 The encounter state machine

`PlayerEncounter.Update()` → `UpdateInternal()` runs a loop over `PlayerEncounterState`:

```
Begin → Wait → PrepareResults → ApplyResults
      → PlayerVictory / PlayerTotalDefeat
      → CaptureHeroes → FreeHeroes
      → LootParty → LootInventory → LootShips → End
```

`Update()` is pumped by the map/menu tick. The loop short‑circuits to `Finish()` whenever
`Current._leaveEncounter == true`. This is the post‑battle resolution pipeline (results,
casualties, loot, prisoners) — all of which are **authoritative world state** and belong to
the server.

### 2.4 How a conversation is *started*

```
"encounter_meeting" menu init: game_menu_encounter_meeting_on_init
  ├─ if (MeetingDone || Battle exists not involving main):
  │     if (LeaveEncounter) PlayerEncounter.Finish();              // neutral talk finished → leave
  │     else { if (Battle == null) StartBattle(); SwitchToMenu("encounter"); }  // hostile → battle menu
  └─ else PlayerEncounter.DoMeeting()
            └─ DoMeetingInternal()
                 ├─ _meetingDone = true
                 ├─ partner = ConversationHelper.GetConversationCharacterPartyLeader(_encounteredParty)
                 ├─ playerData  = ConversationCharacterData(PlayerCharacter, MainParty)
                 ├─ partnerData = ConversationCharacterData(partner, _encounteredParty)
                 └─ land:  CampaignMapConversation.OpenConversation(playerData, partnerData)
                    sea:   CampaignMission.OpenConversationMission(playerData, partnerData)
```

`OpenConversation` → `ConversationManager.OpenMapConversation`:

```csharp
(GameStateManager.Current.ActiveState as MapState).OnMapConversationStarts(playerData, partnerData);
SetupAndStartMapConversation(partnerData.Party.MobileParty,
    new MapConversationAgent(partnerData.Character),     // partner agent
    new MapConversationAgent(PlayerCharacter));          // _mainAgent
```

`SpeakerAgent` (`ConversationManager.SpeakerAgent`) is assigned during the sentence flow
(`StartNew → SetSpeakerAgent`). It is **null** whenever the partner `MapConversationAgent`
was built from a null character — i.e. when `_encounteredParty` was never seeded.

### 2.5 How everything *finishes*

- **Conversation (neutral):** the dialog's terminating node runs
  `MapEventHelper.OnConversationEnd()` (wired as a dialog `.Consequence(...)`), which sets
  `PlayerEncounter.LeaveEncounter = true` when the encountered party is not at war. The
  meeting menu re‑inits, sees `MeetingDone && LeaveEncounter`, and calls
  `PlayerEncounter.Finish()`.
- **Conversation (hostile, then fight):** the menu re‑inits with `MeetingDone && !LeaveEncounter`,
  calls `StartBattle()` and `SwitchToMenu("encounter")`.
- **Battle:** runs the state machine through results, then `End` → `Finish()`.
- **`PlayerEncounter.Finish()`:** `GameMenu.ExitToLast()`, applies/teleports as needed, then
  `Campaign.Current.PlayerEncounter = null; Campaign.Current.LocationEncounter = null;`.

> ⚠️ `MapEventHelper.CanMainPartyLeaveBattleCommonCondition()` dereferences
> `MainParty.MapEvent.PlayerSide` on its first line. It is an **`"encounter"` (battle) menu**
> option condition and will NRE if reached with `MapEvent == null`. Reaching it during a
> neutral meeting means the meeting fell into the battle branch because `LeaveEncounter`
> was never set (see §6.3).

---

## 3. What breaks in coop

1. **Singleton `PlayerEncounter.Current`.** Vanilla assumes one player. If two players
   each enter an encounter, a single shared `Current` cannot represent both. → Each client
   must keep its **own** `Current` for its **own** `MainParty`; the field must never be driven
   by another player's encounter.
2. **Per‑client identity.** `MainParty` / `MainHero` / `PlayerCharacter` differ per client.
   `DoMeeting`, the conversation UI, and the menus must run **only on the owning client**.
   Broadcasting "open the meeting menu" to everyone opens a bogus conversation on the wrong
   client (and produces the null‑`SpeakerAgent` / null‑`MapEvent` crashes).
3. **MapEvent identity.** A battle must be the *same* `MapEvent` object (same object‑manager
   id) on all participating clients. Creation must be server‑authoritative; clients resolve
   the id. This is the `MapEvents` service's responsibility, not `PlayerEncounters`.
4. **Time / pause.** Vanilla pauses the world on a menu/encounter. The world is shared, so an
   encounter must not pause everyone. The `Time` service already neutralizes local pausing
   (`DisableGameMenuPausePatches`, `MapStatePatch`) and makes pause server‑authoritative
   (`ITimeControlInterface`). Encounters only need to *register intent* (an unpause policy),
   not set time directly.
5. **Conversation consequences.** A dialogue can claim a fief, recruit troops, change
   relations, declare war, advance a quest. Those are world mutations and must be synced
   **per domain** (the existing pattern), independent of the conversation UI.

---

## 4. Design principles (how this fits the framework)

The framework has one repeatable shape, used by `MapEvents`, `Time`, `Settlements`, etc.:

- **Messages**
  - *Local request*: `readonly struct X : IEvent` — published on the broker by a Harmony patch.
  - *Network command*: `[ProtoContract] readonly struct NetworkX : ICommand` — carries
    object‑manager **string ids**, sent via `INetwork`.
- **Patches** (Harmony) intercept the vanilla method and decide locally:
  ```csharp
  if (AllowedThread.IsThisThreadAllowed()) return true;   // call came from our own handler → run original
  if (ModInformation.IsServer) return true;               // server runs authoritative logic
  MessageBroker.Instance.Publish(__instance, new XRequest(...));
  return false;                                           // block the local vanilla path
  ```
- **Handlers** (`IHandler`, auto‑registered by `ServiceModule`) bridge broker ↔ network:
  - local event → resolve to ids via `IObjectManager` → `network.SendAll(...)` / `network.Send(peer, ...)`
  - network command → resolve ids → apply inside `using (new AllowedThread()) { ... }`
- **Ownership** via `IsPartyControlled()` (`IControlledEntityRegistry` + `IControllerIdProvider`)
  and `IsPlayerParty()` (`IPlayerRegistry`). The **server is authoritative**; a **client owns its
  own `MainParty`**.

The mod is **client‑authoritative for intent, server‑authoritative for world mutation.**
The owning client says "I want to encounter X"; the server validates + performs the world
mutation (MapEvent creation, time) and tells the relevant client(s) what to do locally.

---

## 5. Current implementation (as of commit `a201b1f0`)

This section describes what is **actually in the code today**, which diverges from the
design principles in §1–4. The separate `PlayerEncounters` service that an earlier draft of
this doc proposed (a `RequestPlayerEncounter → NetworkRequestPartyEncounter →
NetworkPartyEncounterCreated` request/response flow, plus conversation/menu diagnostics) **was
removed**. All encounter synchronization now lives in the existing **`MapEvents`** service,
and the model is **broadcast‑replication**, not owner‑targeted request/response.

### 5.1 Where it lives & the actual model

- No `PlayerEncounters` service, no `PlayerEncounter.Init` patch, no encounter
  request/response messages. `PlayerEncounter.Init` / `DoMeeting` / menus / `Update` run
  **vanilla** on whichever side reaches them.
- Encounter + `MapEvent` creation runs **locally** wherever `EncounterManager.StartPartyEncounter`
  is reached (client *or* server). The **server** additionally publishes `BattleStarted`, which is
  broadcast as `NetworkStartBattle` so **other clients replicate the same encounter** by re‑running
  `StartPartyEncounter` under an `AllowedThread`.
- `MapEventManagerTickDisable` was **deleted** — `MapEventManager.Tick` now runs on the
  client again (this addressed the stale‑MapEvent / `MapEventSide` desync). `EncounterManager.Tick`
  is no longer patched either (the `TickPatch` is commented out). **`MapEvent.Update` is still
  disabled on clients** (`MapEventPatches.PrefixUpdate => ModInformation.IsServer`), so battle
  *progression* stays server‑authoritative.
- There is **no ownership gate on conversations/menus** — conversations open vanilla and local
  wherever the encounter resolves. (This is why the known conversation bugs in §6.3 / the
  leaderless‑party and `raid_occupied` crashes still reproduce; see "Known gaps" below.)

Relevant files (all under `Services/MapEvents/`): `Patches/EncounterManagerPatches.cs`,
`Patches/PlayerEncounterPatches.cs`, `Patches/MapEventPatches.cs`,
`Handlers/BattleHandler.cs`, `Messages/Start/*`, `Messages/Leave/*`.

### 5.2 Encounter start — actual control flow

```
EncounterManager.StartPartyEncounter(atk, def)
  EncounterManagerPatches.Prefix:
    if (!MapEventConfig.Enabled) return false
    if (AllowedThread.IsThisThreadAllowed()) return true     // replication call → run original
    if (ModInformation.IsClient)            return true       // client runs the encounter LOCALLY
    // server:
    publish BattleStarted(atk, def); return true              // run original (creates MapEvent on server)

[Server] Handle_BattleStarted
    resolve atk/def ids
    if (hasPlayer && AllPlayersInMapEvents) timeControl.ServerSetTimeControl(Pause)
    SendAll(NetworkStartBattle{attackerId, defenderId})

[All clients] Handle_NetworkStartBattle
    resolve ids → EncounterManagerPatches.OverrideOnPartyInteraction(atk, def)
        GameLoopRunner.RunOnMainThread + using(new AllowedThread):
            StartPartyEncounter(atk, def)        // replicates the encounter/MapEvent locally
            (or StartSettlementEncounter if defender is a settlement)
```

`HandleEncounterForMobileParty` is gated to controlled parties only
(`EncounterManagerPatches.HandleEncounterForMobilePartyPatch`). Settlement entry still goes
through `StartSettlementEncounter` → `StartSettlementEncounterAttempted` (client publishes,
server runs), unchanged.

> Divergence from §5.1‑proposed: there is **no `NetworkPartyEncounterCreated`, no menu
> decision on the server, and no owner‑targeting** — every client re‑runs the encounter from
> the broadcast, and the menu/conversation is whatever vanilla `Init`/`DoMeeting` produces locally.

### 5.3 Battle start — actual control flow

`MapEvent` creation is **not** delegated to a dedicated step; it falls out of the vanilla
`StartPartyEncounter` → `StartBattleAction` path that runs locally (server) and is replicated
on clients via `OverrideOnPartyInteraction` (§5.2). Battle‑specific patches:

- `PlayerEncounterPatches.StartBattleInternalPrefix` — `AllowedThread` runs; otherwise only the
  **server** runs `StartBattleInternal` (`return ModInformation.IsServer`).
- `MapEventPatches.PrefixOnBattleWon` — skipped on clients; on the server, commits results via
  `CalculateAndCommitMapEventResults()` when a player party is involved.
- `MapEventPatches.PrefixBattleState` (setter) and `PrefixAddInvolvedPartyInternal` publish
  `MapEventBattleStateChangeAttempted` / `MapEventInvolvedPartiesAdded` for sync.

Mission scene launch (unchanged, in `BattleHandler`):

```
AttackMissionAttempted ─► [server] NetworkAttackMissionAttempted (each side MakeReadyForMission)
                       ─► NetworkStartAttackMission ─► CampaignMission.OpenBattleMission (clients)
```

### 5.4 Conversation start — actual control flow

**Not synchronized.** When an encounter resolves to `encounter_meeting`, vanilla
`game_menu_encounter_meeting_on_init → PlayerEncounter.DoMeeting → CampaignMapConversation.OpenConversation`
runs **locally, unmodified**, wherever the encounter happened. There is no mod patch on the
conversation path (the earlier `ConversationDiagnosticPatches` / `EncounterMenuDiagnosticPatches`
and the `DoMeeting`/`Init` patches were removed).

Consequences of conversations (relation/skill/gold/recruit/claim/quest changes) are synced
per‑domain by their own services, not here.

> Known gaps (still reproducible, see §6.3 and the appendix):
> - Leaderless party → conversation partner resolves to a troop → no opening dialog line
>   matches → `SpeakerAgent` null → NRE in `MissionConversationVM.Refresh()`.
> - Encounters that resolve to a menu registered by a **disabled** campaign behavior crash with
>   a null `GameMenu` (e.g. `raid_occupied` from `DisableVillageHostileActionCampaignBehavior`,
>   which blanket‑skips `RegisterEvents` and so never runs `AddGameMenus`). The same blast radius
>   applies to other disabled conversation/menu behaviors (Notables, CommonTownsfolk,
>   CommonVillagers, Guards, HideoutConversations, Villager).

### 5.5 Encounter finish — actual control flow

Finish is handled at the **`MapEvent`** level via a finalize round‑trip (no `PlayerEncounter.Finish`
patch and no `PlayerLeaveBattle` message — both were removed):

```
[Client] MapEvent.FinalizeEventAux()
   MapEventPatches.PrefixFinalizeEventAux:
     if (CallOriginalPolicy.IsOriginalAllowed() || IsServer) return true   // run real finalize
     else publish MapEventFinalizeAttempted(mapEvent); return false        // block local finalize

[Client] Handle_MapEventFinalizeAttempted → SendAll(NetworkMapEventFinalizeAttempted{mapEventId})  // → server

[Server] Handle_NetworkMapEventFinalizeAttempted
     mapEvent.FinalizeEventAux()                       // authoritative finalize
     Send(payload.Who, NetworkMapEventFinalized)       // back to the requesting client

[Requesting client] Handle_NetworkMapEventFinalized → GameMenu.ExitToLast()
```

Surrender keeps its own path: `PlayerEncounterPatches.PlayerSurrenderInternalPrefix` publishes
`PlayerSurrendered` → `Handle_PlayerSurrendered` (server `SendAll(NetworkPlayerSurrendered)` +
local `taken_prisoner` menu) → `Handle_NetworkPlayerSurrendered` (`DoSurrender` + `FinalizeEvent`).
`CheckNearbyPartiesToJoinPlayerMapEvent` is disabled outright (returns false).

> Divergence / known gap: this path only acks the **requesting** client (`ExitToLast`) and
> finalizes authoritatively on the server. Propagation of a finalize to **non‑requesting**
> clients relies on `MapEvent` state sync + `MapEventManager.Tick` purging finalized events,
> rather than an explicit broadcast — this is part of the "mostly working but with bugs" state.
> There is no separate "no‑MapEvent meeting finish" path; a neutral meeting ends through the
> vanilla `LeaveEncounter` → `PlayerEncounter.Finish()` flow locally.

### 5.6 Time / pause — actual

Unchanged from the design decision and implemented in `BattleHandler.Handle_BattleStarted`:
the **server** calls `ITimeControlInterface.ServerSetTimeControl(Pause)` only when a player is
involved **and** `AllPlayersInMapEvents()` is true. Plain meetings/menus do **not** pause the
shared world; local pausing is already neutralized by `DisableGameMenuPausePatches` /
`MapStatePatch`. No per‑encounter unpause policy is registered.

---

## 6. Concrete service layout (current)

Everything is in the **`MapEvents`** service — there is no `PlayerEncounters` folder. The
pieces that implement the flows in §5:

```
source/GameInterface/Services/MapEvents/
├── MapEventConfig.cs                         // const bool Enabled / Debug
├── Handlers/
│   ├── BattleHandler.cs                      // start, finalize, surrender, involved-parties, mission launch
│   └── MapEventHandler.cs
├── Logging/MapEventLogger.cs
├── Messages/
│   ├── Start/
│   │   ├── BattleStarted.cs                  // IEvent  (server-side, from StartPartyEncounter prefix)
│   │   ├── NetworkStartBattle.cs             // ICommand (broadcast → clients replicate the encounter)
│   │   ├── AttackMissionAttempted.cs / NetworkAttackMissionAttempted.cs / NetworkStartAttackMission.cs  // mission launch
│   │   └── StartBattleAttempted.cs / NetworkRequestStartBattle.cs
│   ├── Leave/
│   │   ├── MapEventFinalizeAttempted.cs      // IEvent  (client-side, from FinalizeEventAux prefix)
│   │   ├── NetworkMapEventFinalizeAttempted.cs   // ICommand (client → server: finalize authoritatively)
│   │   ├── NetworkMapEventFinalized.cs       // IEvent  (server → requesting client: ExitToLast)
│   │   └── PlayerSurrendered.cs / NetworkPlayerSurrendered.cs
│   ├── MapEventBattleStateChangeAttempted.cs / NetworkChangeBattleState.cs
│   └── MapEventInvolvedPartiesAdded.cs / NetworkAddInvolvedParties.cs
└── Patches/
    ├── EncounterManagerPatches.cs            // StartPartyEncounter / StartSettlementEncounter /
    │                                         //   HandleEncounterForMobileParty / OverrideOnPartyInteraction
    ├── PlayerEncounterPatches.cs             // StartBattleInternal / PlayerSurrenderInternal / CheckNearbyParties…
    ├── MapEventPatches.cs                    // FinalizeEventAux / Update(client-disabled) / OnBattleWon /
    │                                         //   BattleState / AddInvolvedPartyInternal / CanPartyJoinBattle
    ├── StartBattleActionPatches.cs / EncounterAttackConsequencePatch.cs / TakePrisonerActionPatches.cs
    ├── EncounterManagerAllowTemporaryRosters.cs / MapEventRobustnessPatches.cs / MapEventUpdatePatch.cs
    └── Disable/  (DisableBattle… / DisableCampaignWarManager… / DisableEncounterCaptureTheEnemyOnConsequence / …)
```

Removed in the rework (do **not** re-add unless re-designing): the `PlayerEncounters` service
(`RequestPlayerEncounter` / `NetworkRequestPartyEncounter` / `NetworkPartyEncounterCreated`,
`PlayerEncounterHandler`, the `PlayerEncounter.Init`/`DoMeeting` patches, and the
`ConversationDiagnosticPatches` / `EncounterMenuDiagnosticPatches`); `MapEventManagerTickDisable`;
`PlayerEncounterPatch.cs`; the `PlayerLeaveBattle` / `NetworkLeavePlayerBattle` messages.

> Rule of thumb in the current code: **`MapEvents` owns the entire battle/MapEvent layer**;
> the **player‑facing menu/conversation flow is left to vanilla and runs locally** on each
> client. The two are not separated into distinct services.

### 6.1 Encounter entry — actual

There is **no patch on `PlayerEncounter.Init`**. Vanilla `Init` runs wherever it is reached.
The entry point the mod patches is `EncounterManager.StartPartyEncounter`
(`EncounterManagerPatches.Prefix`): client runs it locally; server runs it locally **and**
publishes `BattleStarted` for broadcast/replication (see §5.2).

### 6.2 Battle replication — actual

`Handle_BattleStarted` (server) → `SendAll(NetworkStartBattle)` →
`Handle_NetworkStartBattle` (clients) → `EncounterManagerPatches.OverrideOnPartyInteraction`
re‑runs `StartPartyEncounter`/`StartSettlementEncounter` under `AllowedThread`. Mission scene
launch is `AttackMissionAttempted → NetworkAttackMissionAttempted → NetworkStartAttackMission
→ CampaignMission.OpenBattleMission`. Results commit server‑side in
`MapEventPatches.PrefixOnBattleWon`.

### 6.3 The neutral‑meeting NRE — still open

This is **not fixed** in the current implementation (no `PlayerEncounter.Init` interception,
no menu gating). When an encounter resolves to a conversation whose partner has no hero
(leaderless party → troop partner), no opening dialog line matches, `SpeakerAgent` stays null,
and `MissionConversationVM.Refresh()` NREs. Likewise, encounters resolving to a menu owned by a
**disabled** campaign behavior crash with a null `GameMenu`. See §5.4 "Known gaps" and §8 for
the remediation options (sync `LeaderHero` / gate the meeting path; stop `Disable*CampaignBehavior`
patches from also dropping menu/dialog registration).

### 6.4 Player‑vs‑player meetings (future)

Unchanged as a future concern. When two **players** meet, the "conversation" is between two
humans. Options: suppress the vanilla conversation and show a lightweight interaction menu
(trade / form army / nothing), or route a real conversation on one side and mirror chosen
consequences. Currently both clients would run the vanilla conversation locally against each
other's parties, which is not handled.

---

## 7. Edge cases / open questions (current state)

- **Leaderless / under‑synced party conversation.** Partner resolves to a troop (because
  `party.LeaderHero` is null), no opening dialog line matches → `SpeakerAgent` null →
  `MissionConversationVM.Refresh()` NRE. Open: confirm whether the party should have a synced
  `LeaderHero` (sync gap) vs is genuinely leaderless (don't route it into `DoMeeting`).
- **Menus owned by disabled behaviors.** `Disable*CampaignBehavior` patches that do
  `RegisterEvents() => false` also drop the menus/dialog those behaviors register. Confirmed
  for `raid_occupied` (`DisableVillageHostileActionCampaignBehavior`); same risk for Notables /
  CommonTownsfolk / CommonVillagers / Guards / HideoutConversations / Villager. Activating an
  unregistered menu yields a null `GameMenu` in `MenuContext.HandleStates()`.
- **Finalize propagation.** The §5.5 finalize round‑trip only acks the **requesting** client
  (`ExitToLast`) and finalizes on the server. Other clients rely on `MapEvent` state sync +
  `MapEventManager.Tick` purging finalized events — not an explicit broadcast. Watch for
  lingering/desynced `MapEvent`s on non‑participating clients.
- **Double‑drive of `MapEvent`s.** Because clients both replicate the encounter
  (`OverrideOnPartyInteraction` → `StartPartyEncounter` locally) and receive synced
  `MapEvent`/`MapEventSide` objects, mismatches can surface as `MapEventSide.RemovePartyInternal`
  index errors during `set_MapEventSide`. Re‑enabling `MapEventManager.Tick` mitigated the worst
  of it; the contention is still a latent source of bugs.
- **Settlement entry.** `town_outside` / `village_outside` / `castle_outside` go through the
  `Settlements` / `MobileParties` settlement‑encounter messages
  (`StartSettlementEncounterAttempted`) — separate path, unchanged.
- **Naval encounters** use `CampaignMission.OpenConversationMission` instead of
  `CampaignMapConversation.OpenConversation`; both run locally and are subject to the same
  leaderless‑partner gap.
- **Owner disconnects mid‑encounter / battle.** No explicit teardown of a half‑created
  `MapEvent` or release of the server pause; relies on existing connection/disconnect handlers.

---

## 8. Remaining work / roadmap

Relative to the **current** `MapEvents`‑based implementation (the design in §1–4 is the target
end‑state; §5 is where the code is now):

1. **Fix the conversation crashes (highest priority).** Two distinct causes:
   - *Disabled‑behavior registration loss* — make each `Disable*CampaignBehavior` keep its
     game‑menu / dialog registration (let `OnSessionLaunched`/`AddGameMenus` run) while skipping
     only the problematic event/AI handlers; or stop disabling `RegisterEvents` wholesale.
   - *Leaderless partner* — ensure `MobileParty.LeaderHero` syncs to clients, or gate the
     meeting/`DoMeeting` path so a party with no hero partner never opens a map conversation.
2. **Harden finalize propagation.** Broadcast finalize to all participating clients (not just
   the requester), and make sure server‑initiated finalizes reach clients.
3. **Reduce `MapEvent` double‑drive.** Decide whether clients replicate encounters locally
   *or* purely consume synced `MapEvent`s, and remove the contention (see §3, §7).
4. **Time policy** — unchanged: meetings/menus never pause; battles pause via
   `BattleHandler` when all players are in map events.
5. **P‑v‑P meetings** and **disconnect cleanup** as follow‑ups.

> Longer‑term, if the broadcast‑replication model keeps producing `MapEvent` contention, revisit
> the §5(proposed)/§1–4 owner‑targeted, server‑authoritative model: client requests, server
> creates the `MapEvent` once and broadcasts it, owning client reacts — instead of every client
> re‑deriving the encounter.

---

### Appendix: key vanilla entry points

| Method | Role |
|---|---|
| `EncounterManager.StartPartyEncounter` | proximity → start/restart `PlayerEncounter` or `StartBattleAction` |
| `PlayerEncounter.Start()` | `Campaign.Current.PlayerEncounter = new PlayerEncounter()` |
| `PlayerEncounter.SetupFields(atk,def)` | sets `_encounteredParty`, `PlayerSide` |
| `PlayerEncounter.Init(atk,def,stl)` | full entry: setup + menu + optional battle + activate menu |
| `EncounterGameMenuModel.GetEncounterMenu` | authoritative menu decision |
| `PlayerEncounter.StartBattle()` | create/attach the `MapEvent` |
| `CampaignMission.OpenBattleMission` | launch the fight scene |
| `PlayerEncounter.DoMeeting()` | open the map conversation |
| `CampaignMapConversation.OpenConversation` | → `ConversationManager.OpenMapConversation` (agents) |
| `MapEventHelper.OnConversationEnd()` | sets `LeaveEncounter` for non‑hostile talk |
| `PlayerEncounter.Update()` | post‑battle results state machine |
| `PlayerEncounter.Finish()` | tear down + clear `Campaign.Current.PlayerEncounter` |
