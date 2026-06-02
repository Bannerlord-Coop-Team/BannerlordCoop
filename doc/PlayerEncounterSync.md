# Syncing `PlayerEncounter` in BannerlordCoop

A design for synchronizing player encounters, battles, and conversations within the
existing GameInterface / Coop.Core message + network framework.

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
| The encounter menu UI, the conversation UI, the local state machine | **Each client, locally** | this service (`PlayerEncounters`) |

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

## 6. Concrete service layout

```
source/GameInterface/Services/PlayerEncounters/
├── PlayerEncounterConfig.cs                 // const bool Enabled / Debug (mirror MapEventConfig)
├── Messages/
│   ├── RequestPlayerEncounter.cs            // IEvent  (owner → broker)            [exists]
│   ├── NetworkRequestPartyEncounter.cs      // ICommand (owner → server)           [exists]
│   ├── NetworkPartyEncounterCreated.cs      // ICommand (server → owner)           [exists; make MapEventId nullable]
│   ├── EncounterFinished.cs                 // IEvent  (owner → broker) [new]      // no-MapEvent finish
│   └── NetworkEncounterFinished.cs          // ICommand (owner → server / ack)     [new]
├── Handlers/
│   └── PlayerEncounterHandler.cs            // bridges all of the above            [exists; split responsibilities]
└── Patches/
    ├── PlayerEncounterInitPatch.cs          // PlayerEncounter.Init(atk,def,stl) prefix  [the commented-out one]
    └── PlayerEncounterFinishPatch.cs        // PlayerEncounter.Finish prefix (no-MapEvent case)
```

Reconcile with existing patches in `MapEvents` (keep there, do not duplicate):
- `EncounterManagerPatches` (StartPartyEncounter / StartSettlementEncounter / Tick / HandleEncounterForMobileParty)
- `PlayerEncounterPatches` (StartBattleInternal / PlayerSurrenderInternal / Finish / CheckNearbyParties…)
- `BattleHandler` (battle + mission start, leave, surrender, involved parties)

> Rule of thumb: **`MapEvents` owns the battle/MapEvent layer; `PlayerEncounters` owns the
> player‑facing encounter/menu/conversation flow.** They communicate by ids, not by
> reaching into each other's state.

### 6.1 Patch: encounter entry (`PlayerEncounter.Init`)

Restore the commented‑out `PlayerEncounterInitPatch` with these guards:

```csharp
[HarmonyPatch(typeof(PlayerEncounter))]
internal class PlayerEncounterInitPatch
{
    [HarmonyPatch(nameof(PlayerEncounter.Init), new[]{ typeof(PartyBase), typeof(PartyBase), typeof(Settlement) })]
    [HarmonyPrefix]
    public static bool PrefixInit(PlayerEncounter __instance,
        PartyBase attackerParty, PartyBase defenderParty, Settlement settlement)
    {
        if (AllowedThread.IsThisThreadAllowed()) return true;   // our own handler is driving it
        if (ModInformation.IsServer) return true;               // server runs authoritative Init path

        // Only the controlling client may originate an encounter for its MainParty
        if (MobileParty.MainParty?.IsPartyControlled() != true) return false;

        MessageBroker.Instance.Publish(__instance,
            new RequestPlayerEncounter(attackerParty, defenderParty, settlement));
        return false;
    }
}
```

(The debounce — `CanSendRequest` — is fine to keep; `Init` can be re‑entered while the menu
churns.)

### 6.2 Handler responsibilities (clean split)

- `Handle_RequestPlayerEncounter` (runs on the **owning client**): resolve ids, `SendAll`
  `NetworkRequestPartyEncounter` (only the server acts on it).
- `Handle_NetworkRequestPartyEncounter` (**server only** — guard with `ModInformation.IsServer`):
  validate, compute `menu`, create a `MapEvent` *only if the menu needs one*, then
  `Send(requesterPeer, NetworkPartyEncounterCreated{...})`.
- `Handle_NetworkPartyEncounterCreated` (**owner only**): rebuild local `PlayerEncounter`
  (`Finish` old → `Start` → `SetupFields`), `LeaveEncounter = false`, `ActivateGameMenu(menu)`.
  Run the world‑mutating bits inside `using (new AllowedThread())` so the entry patch lets
  them through.

Add explicit `if (ModInformation.IsServer)` / owner checks to each handler so a message that
fans out to everyone only *acts* where it should.

### 6.3 Fixing the neutral‑meeting NRE

For the meeting path the server must **not** create a `MapEvent`. With `mapEventId == null`,
the owner seeds `SetupFields(attacker, defender)` (so `_encounteredParty` is valid and
`SpeakerAgent` resolves) and activates `"encounter_meeting"`. The vanilla conversation‑end
consequence then sets `LeaveEncounter = true`, so the menu re‑init `Finish()`es and never
falls into the `"encounter"` battle branch that calls
`CanMainPartyLeaveBattleCommonCondition()` with a null `MapEvent`.

If `OnConversationEnd` is not reliably firing on the owner, add a guard patch on
`game_menu_encounter_meeting_on_init` (or on the `"encounter"` leave/abandon conditions) that
bails when `MobileParty.MainParty.MapEvent == null`.

### 6.4 Player‑vs‑player meetings (future)

When two **players** meet, the "conversation" is between two humans. Options:
- Suppress the vanilla conversation entirely and show a lightweight interaction menu
  (trade / form army / nothing), or
- Route a real conversation only on one side and mirror chosen consequences.

Out of scope for the first pass, but the ownership rule keeps it tractable: the *requester*
is the owner of the flow; the other player's party is just `_encounteredParty`.

---

## 7. Edge cases / open questions

- **Re‑entrancy.** `Init`/menu churn re‑publishes requests. Keep the debounce, and make
  `Handle_NetworkPartyEncounterCreated` idempotent (finish any existing `Current` first — it
  already does).
- **Encountered party joins a battle in progress** (`join_encounter`, `army_encounter`,
  siege). The server already has the `MapEvent`; send its id and let the owner `JoinBattle`
  via the existing `MapEvents` join path rather than re‑deriving sides.
- **Owner disconnects mid‑encounter.** Server must release any pause/unpause policy and tear
  down a half‑created `MapEvent`. Hook into the existing connection/disconnect handlers.
- **Settlement entry.** `town_outside` / `village_outside` / `castle_outside` go through the
  `Settlements` / `MobileParties` settlement‑encounter messages
  (`StartSettlementEncounterAttempted`) — keep that path; don't duplicate it here.
- **MapEvent id availability.** `NetworkPartyEncounterCreated` assumes the `MapEvent` is
  already registered with the object manager on the owner. Ensure the `MapEvents` creation +
  registration broadcast lands **before** (or atomically with) the owner message, or have the
  owner resolve‑with‑retry.
- **Naval encounters** use `CampaignMission.OpenConversationMission` instead of
  `CampaignMapConversation.OpenConversation`. The owner‑local path handles both; just make
  sure `_encounteredParty.MobileParty.IsCurrentlyAtSea` is consistent on the owner.

---

## 8. Phased implementation plan

1. **Gate MapEvent creation** in `Handle_NetworkRequestPartyEncounter` on "menu needs a
   battle". Make `NetworkPartyEncounterCreated.MapEventId` nullable. → fixes the neutral
   meeting (`SpeakerAgent` null) and the `CanMainPartyLeaveBattleCommonCondition` NRE.
2. **Add owner / server guards** to every handler (`IsServer`, `IsPartyControlled`) so
   menus/conversations only run on the owner.
3. **Restore `PlayerEncounterInitPatch`** with the standard guard pattern; remove ad‑hoc
   entry points.
4. **Add the no‑MapEvent finish path** (`EncounterFinished` / `NetworkEncounterFinished`) and
   make the existing `Finish` patch route meetings to it (not battle teardown).
5. **Delegate battles to `MapEvents`** — replace the local `DetermineAndCreateMapEvent` with
   a call into the MapEvents creation flow; carry only the resulting `mapEventId`.
6. **Time policy**: none for meetings/menus — the world keeps ticking. Battles continue to
   use the existing `BattleHandler` pause policy.
7. **P‑v‑P meetings** and **disconnect cleanup** as follow‑ups.

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
