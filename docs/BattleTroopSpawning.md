# Bannerlord Native Architecture: MapEvents → Battles → Agents

**Scope:** how the *unmodified game* takes a campaign-map battle (`MapEvent`) all the way down to
`Agent` instances in a mission, and how state flows back to the campaign during and after the
battle. Everything here is decompiled vanilla behavior (`TaleWorlds.CampaignSystem`,
`TaleWorlds.MountAndBlade`, `SandBox`) — no mod code. Written as the substrate reference for
designing a synchronization system on top of it.

---

## 1. The three layers and who is "live" when

| Layer | Key types | Active when |
|---|---|---|
| **Campaign** | `Campaign`, `MapState`, `MobileParty`, `PartyBase`, `EncounterManager`, `MapEventManager` | Map is ticking |
| **Encounter** | `PlayerEncounter` (state machine), game menus (`encounter` menu, `MenuHelper`) | Player is engaged; map time usually paused/waiting |
| **Mission** | `Mission`, `MissionState`, `MissionBehavior` stack, `Agent`, `Team`, `Formation` | A battle scene is open |

Critical scheduling fact: `MapState` and `MissionState` are exclusive game states. **Campaign time
does not tick while a mission is open**, so `MapEvent.Update` (the map-side simulation) and a
real-time mission never run concurrently for the same event. AI-vs-AI events resolve purely through
the map-side simulation; the player's event resolves through the mission, with results committed
when the encounter resumes.

---

## 2. Campaign-side battle model

### 2.1 `MapEvent`

- Created by the `MapEventManager`/event-component factories; typed via `BattleTypes`
  (`FieldBattle`, `Raid`, `Siege`, `Hideout`, `SallyOut`, `SiegeOutside`, `BlockadeBattle`, …) with
  a `MapEventComponent` subclass carrying type-specific behavior (`FieldBattleEventComponent`,
  `RaidEventComponent`, `SiegeAmbushEventComponent`, …).
- Owns exactly two `MapEventSide`s (`_sides[2]`, indexed by `BattleSideEnum.Defender/Attacker`),
  a `BattleState` (`None` / `AttackerVictory` / `DefenderVictory`), `WonRounds`, a
  `TroopUpgradeTracker`, and an optional `IBattleObserver` (scoreboard hook).
- `IsPlayerMapEvent` distinguishes the event the player participates in
  (`MapEvent.PlayerMapEvent`).

### 2.2 `MapEventSide`

Per side: `LeaderParty`, `_mapFaction`, `_battleParties` (list of `MapEventParty`), running tallies
(`TroopCasualties`, `CasualtyStrength`, `RenownValue`, `InfluenceValue`, `StrengthRatio`), and the
**mission allocation state**: `_readyTroopsPriorityList` and
`_allocatedTroops : Dictionary<UniqueTroopDescriptor, MapEventParty>`.

### 2.3 `MapEventParty` and the flattened roster

Joining a battle wraps a `PartyBase` in a `MapEventParty`, which snapshots the party's member
roster into a **`FlattenedTroopRoster`** (`_roster`): one `FlattenedTroopRosterElement` per
individual man — `(CharacterObject Troop, UniqueTroopDescriptor Descriptor, RosterTroopState
State, Xp)`. It also keeps three `TroopRoster`s of outcomes: `DiedInBattle`, `WoundedInBattle`,
`RoutedInBattle`. `MapEventParty.Update()` re-snapshots `_roster` against the live
`Party.MemberRoster`.

**`UniqueTroopDescriptor` is the identity of one individual man for the entire battle.** Every
downstream structure — allocation, agent origin, casualty report, XP — keys on it. Any external
system that wants to mirror a battle must preserve these descriptors.

---

## 3. How battles start

### 3.1 Entry points

- `EncounterManager.Tick` → `HandleEncounterForMobileParty(party, dt)` fires when parties touch;
  it routes to `StartPartyEncounter(attacker, defender)` or `StartSettlementEncounter(...)`.
- For AI-only contact this creates the `MapEvent` directly; for the player it creates a
  `PlayerEncounter` (`PlayerEncounter.Start` → `Init`), which drives game menus.
- `PlayerEncounter.StartBattleInternal` selects the event type — the branch order is: forced flags
  (`ForceRaid` / `ForceSallyOut` / `ForceVolunteers` / `ForceSupplies`) → settlement defender
  (fortification ⇒ siege, village ⇒ raid, hideout ⇒ hideout) → sally-out-ambush / blockade
  variants → besieged attacker / defender ⇒ sally-out / siege-outside → default
  `FieldBattleEventComponent.CreateFieldBattleEvent`.
- Construction chain: `MapEvent` ctor → sides constructed (`MapEventSide(mapEvent, side,
  leaderParty)`) → each joining `PartyBase` becomes a `MapEventParty`
  (`MapEvent.AddInvolvedPartyInternal`), snapshotting its roster. Nearby friendly parties can be
  pulled in (`CanPartyJoinBattle` enforces war-relation consistency on both sides).

### 3.2 The encounter state machine (`PlayerEncounter`)

`PlayerEncounterState`: `Begin → Wait → PrepareResults → ApplyResults → PlayerVictory /
PlayerTotalDefeat → CaptureHeroes → FreeHeroes → LootParty → LootInventory → LootShips → End`.
`PlayerEncounter.Update()` advances it; the encounter game menu's options (Attack, Send Troops,
Leave, Surrender) set flags (`PlayerSurrender`, `LeaveEncounter`, …) that steer it. This state
machine is the *only* path by which a player mission's outcome is written back into the campaign.

---

## 4. The map-side battle loop (`MapEvent.Update`)

Runs on campaign tick for every live event (so: only when no mission is open):

1. **Diplomacy check** — if either leader is gone or the factions are no longer at war,
   `DiplomaticallyFinished = true` and the event unwinds without a result.
2. `Component?.Update(ref finish)` — type-specific logic (raid progress, siege, …).
3. **Simulation rounds** — while both sides have troops and `_nextSimulationTime` has passed:
   `CheckRunAway()` then `SimulateBattleSessionForMapEvent()` (the auto-resolve combat model:
   picks simulation troops per side, applies damage via
   `MapEventSide.ApplySimulationDamageToSelectedTroop`, using the same descriptors/rosters).
4. When `BattleState != None`, `finish = true` → `OnBattleWon()`:
   `CalculateMapEventResults()` (plunder, renown/influence/morale *shares*), and for
   **non-player events** immediately `CalculateAndCommitMapEventResults()`. Player events defer the
   commit to the encounter loot states (§7).
5. `FinishBattle()` releases parties (their `MapEventSide` is cleared) unless a `PlayerEncounter`
   still owns the event.

The "send troops" flow is this same simulation surfaced to the UI:
`SimulateBattleSetup(priorTroops[])` + `SimulateBattleRound(ticksDef, ticksAtk)`.

---

## 5. Opening a real-time battle mission

From the encounter menu's Attack option: `MenuHelper.EncounterAttackConsequence` →
`CampaignMission.OpenBattleMission(MissionInitializerRecord)`. The record carries the scene name
(chosen by `SceneModel.GetBattleSceneForMapPatch` from the map patch under the player), terrain
type, atmosphere, a random terrain seed, and the encounter direction (attacker→defender positions).

`SandBoxMissions.OpenBattleMission` then calls `MissionState.OpenNew("Battle", rec, behaviors)`
with a behavior stack. The ones that matter for troop flow:

| Behavior | Role |
|---|---|
| `DefaultBattleMissionAgentSpawnLogic` | The spawner: phases, initial spawn, reinforcements |
| `SandBoxBattleMissionSpawnHandler` | Campaign adapter: decides spawn counts from the MapEvent |
| `BattleSpawnLogic("battle_set")` / deployment plan | Where formations are placed |
| `MissionCombatantsLogic` | Builds `Team`s from `MapEvent.InvolvedParties` |
| `BattleAgentLogic` | **The campaign feedback channel** (hits, kills, XP, upgrades) |
| `BattleObserverMissionLogic` | Pushes casualty events to the `IBattleObserver` (scoreboard) |
| `BattleReinforcementsSpawnController` | Triggers reinforcement waves |
| `BattleEndLogic` | Victory/retreat conditions that end the mission |
| `BannerBearerLogic`, `AgentVictoryLogic`, `MissionAgentPanicHandler`, … | Combat behavior detail |

---

## 6. The troop spawning pipeline (campaign → agents)

This is the core chain:

```
MapEventSide ──MakeReadyForMission──► allocation state
      ▲                                   │
      │                          PartyGroupTroopSupplier (IMissionTroopSupplier, one per side)
      │                                   │ SupplyTroops(n) → AllocateTroops → UniqueTroopDescriptor[]
      │                                   ▼
      │                          PartyGroupAgentOrigin (IAgentOriginBase, one per man)
      │                                   │
      │            DefaultBattleMissionAgentSpawnLogic / MissionBattleSideSpawnContext
      │                                   │ formation assignment + deployment
      │                                   ▼
      └── casualties ◄──────────  Mission.SpawnTroop → AgentBuildData → Mission.SpawnAgent → Agent
```

### 6.1 Readying a side — `MapEventSide.MakeReadyForMission(priorTroops)`

Called by each `PartyGroupTroopSupplier` constructor. `MakeReady`:

- Distributes `sizeOfSide` (= side's `TroopCount`, or `priorTroops.Count` when a prior roster is
  carried over, e.g. lord's-hall fights) across `_battleParties` **proportionally to each party's
  `HealthyManCountAtStart`**.
- Per party: `SetParticipatingTroopCount(n)`, then `MapEventParty.Update()` (re-snapshot
  `_roster`), then collects ready elements into `_readyTroopsPriorityList` as
  `(element, party, priority)` tuples — priority comes from the military power model, sorted
  descending so the strongest troops spawn first.
- Resets `_allocatedTroops`.

### 6.2 Supplying troops — `PartyGroupTroopSupplier : IMissionTroopSupplier`

- Ctor: `PartyGroup = mapEvent.GetMapEventSide(side)`, `_initialTroopCount = TroopCount`, then
  `MakeReadyForMission(priorTroops)`.
- `SupplyTroops(n)` → `MapEventSide.AllocateTroops(ref list, n, customConditions)` pops the top of
  the priority list, records each descriptor in `_allocatedTroops`, and returns
  `PartyGroupAgentOrigin(supplier, descriptor, rank)` per man.
- Tracks `_numAllocated/_numKilled/_numWounded/_numRouted`; `AnyTroopRemainsToBeSupplied` and
  `NumTroopsNotSupplied` gate reinforcements and side depletion.

### 6.3 The agent origin — `PartyGroupAgentOrigin : IAgentOriginBase`

The per-man bridge object both directions:

- **Downward:** `Troop` (`CharacterObject`), `BattleCombatant` (the `PartyBase`), `Banner`,
  `Rank`, and `Seed = CharacterHelper.GetPartyMemberFaceSeed(party, troop, rank)` — i.e. visuals
  and equipment randomization are a pure function of (party, troop, rank).
- **Upward:** `SetKilled()/SetWounded()/SetRouted()` and `OnScoreHit(...)` route back through the
  supplier into `MapEventSide` (§7). `UniqueSeed` exposes the descriptor.

### 6.4 Spawn counts, phases and reinforcements

- `SandBoxBattleMissionSpawnHandler.AfterStart`: `total = MapEvent.GetNumberOfInvolvedMen(side)`
  for both sides; `InitWithSinglePhase(totalDef, totalAtk, initialDef, initialAtk, ...,
  CreateSandBoxBattleWaveSpawnSettings())`. Initial spawn vs. total is where the battle-size limit
  manifests; the remainder becomes reinforcement reserve.
- `DefaultBattleMissionAgentSpawnLogic` manages a `MissionSpawnPhase` per side and a
  `MissionBattleSideSpawnContext` holding that side's supplier. Per tick it checks deployment
  completion, batch quotas, and reinforcement conditions (`MissionSpawnSettings`: wave fractions,
  global timers, custom timers) and calls `context.SpawnTroops(n, isReinforcement)`.

### 6.5 Materializing agents — `MissionBattleSideSpawnContext.SpawnTroops`

1. Draw from `_reservedTroops`, then `_troopSupplier.SupplyTroops(remainder)`.
2. Group origins per `Team` (`Mission.GetAgentTeam(origin, isPlayerSide)` — teams were created by
   `MissionCombatantsLogic` from `MapEvent.InvolvedParties`).
3. Ask `MissionGameModels.Current.BattleSpawnModel.GetInitialSpawnAssignments(side, origins)` for a
   formation index (0–7, `FormationClass`) per origin; reinforcements reuse remembered
   assignments.
4. Per formation: `Formation.BeginSpawn(count, isMounted)` + position from the deployment plan;
   banner bearers are diverted through `BannerBearerLogic.SpawnBannerBearer`.
5. Per origin: `Mission.SpawnTroop(origin, isPlayerSide, hasFormation, spawnWithHorse,
   isReinforcement, formationTroopCount, formationTroopIndex, ...)` →
   builds `AgentBuildData(troop).Team(...).Banner(origin.Banner).TroopOrigin(origin)
   .Formation(...).ClothingColor...` → `Mission.SpawnAgent(buildData)` → a live `Agent` whose
   `Agent.Origin` points back at the `PartyGroupAgentOrigin`.

The player's own agent is spawned last in its formation batch; heroes use their actual equipment,
regular troops get equipment variants seeded deterministically via the origin's seed.

---

## 7. Feedback: mission → campaign (during and after the fight)

### 7.1 During the mission — `BattleAgentLogic`

- `OnAgentHit`: attacker's `origin.OnScoreHit(victimCharacter, captain, damage, isFatal,
  isFriendly, weapon)` → XP accrual recorded against the attacker's descriptor in the campaign-side
  structures (committed later by `CommitXpGains`).
- `OnAgentRemoved`:
  - wounded ⇒ `origin.SetWounded()`, killed ⇒ `origin.SetKilled()`, else
    `origin.SetRouted(notFled)`;
  - hero-kills-hero raises `CampaignEventDispatcher.OnCharacterDefeated(killer, victim)`;
  - kills run `TroopUpgradeTracker.CheckUpgrade(side, party, character)`.
- The origin's `Set*` calls flow: `PartyGroupTroopSupplier.OnTroop*` → counters →
  `MapEventSide.OnTroopKilled/Wounded/Routed(descriptor)`:
  - resolve owner via `_allocatedTroops[descriptor]`;
  - `MapEventParty.OnTroopKilled(descriptor)` — **mutates campaign state immediately**: removes
    the man from `PartyBase.MemberRoster` (non-heroes), marks `_roster` state, adds to
    `DiedInBattle`;
  - side tallies update (`CasualtyStrength += troopPower`, `TroopCasualties++`).
- `BattleObserverMissionLogic` mirrors these into the `IBattleObserver`
  (`TroopNumberChanged/HeroWounded/...`) for the scoreboard.

So: **member rosters change live during the mission**, not at the end. The end-of-battle commit
handles outcome distribution, not casualties.

### 7.2 Mission end → encounter resumes

`BattleEndLogic` (all enemies dead/routed, retreat, surrender) ends the mission; `MissionState`
pops back to the encounter menu; `MapEvent.BattleState` / `SetOverrideWinner` capture the verdict.
`PlayerEncounter.Update()` then walks `PrepareResults → ApplyResults → ...`:

- `CalculateMapEventResults()` — plundered/lost gold, player figurehead share, winner
  renown/influence/morale shares (`CalculateWinnerPartiesRenownInfluenceAndMoraleShares`).
- `CalculateAndCommitMapEventResults()` — on victory: `LootDefeatedPartyCasualties/Items/
  Prisoners/Ships`, `CaptureDefeatedPartyMembers`, then `CommitCalculatedMapEventResults` (apply
  gold/renown/influence/morale) — for the player these surface as the `CaptureHeroes/FreeHeroes/
  LootParty/LootInventory/LootShips` encounter states with UI.
- `MapEvent.CommitXpGains()` → per side → per party: the XP accrued per descriptor lands on the
  real rosters (driving the troop-upgrade UI later).
- `FinalizeEvent`/`FinishBattle` → parties' `MapEventSide` cleared, event removed from
  `MapEventManager`; defeated mobile parties may be destroyed; siege events optionally survive
  (`FinishBattleAndKeepSiegeEvent`).

---

## 8. Determinism and identity — what a sync system can rely on

1. **`UniqueTroopDescriptor` is the stable per-man key** through allocation, spawning, casualties
   and XP. Two machines that agree on the `FlattenedTroopRoster` contents (elements + descriptors)
   and on `HealthyManCountAtStart`/participating counts will produce the same allocation input.
2. **Allocation order** is the priority-sorted ready list; priorities come from the military power
   model — deterministic given identical rosters and context.
3. **Agent visuals/equipment** are seeded by `(party, troop, rank)` via the origin — deterministic
   given the same allocation order.
4. Nondeterminism enters through: the mission's `RandomTerrainSeed`, `MBRandom` consumption inside
   the mission (AI, hit rolls), and anything timing-dependent. A mirrored mission therefore stays
   consistent only at the *campaign* granularity (who exists, who died) unless agent-level state is
   synchronized separately.

## 9. Seams useful for an external system

| Seam | What it gives you |
|---|---|
| `IMissionTroopSupplier` (replaceable in the spawn logic ctor) | Full control over *which* men spawn and in what order |
| `IAgentOriginBase` | Per-man identity bridge; intercept `SetKilled/SetWounded/SetRouted/OnScoreHit` |
| `MapEventSide.MakeReadyForMission(priorTroops)` | Inject an exact prior roster (the lord's-hall/`priorTroops` path is a vanilla precedent) |
| `BattleSpawnModel` (`MissionGameModels`) | Override formation assignment of spawned origins |
| `MissionBehavior` events (`OnAgentBuild`, `OnAgentRemoved`, `OnMissionTick`) | Observe/augment every spawn and removal |
| `IBattleObserver` on `MapEvent` | Ready-made casualty event stream |
| `MapEvent.Update` / `SimulateBattleRound` | The auto-resolve path — fully campaign-side, descriptor-based, easiest to drive remotely |
| `PlayerEncounter` state machine | The single funnel through which mission outcomes reach the campaign |
