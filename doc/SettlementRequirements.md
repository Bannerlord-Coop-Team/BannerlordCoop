# Settlement Mission NPC Requirements

Requirements for host-owned settlement-mission NPCs (town center, tavern, lord's hall, castle
courtyard, village interiors). Mirrors the numbering style of [BattleRequirements.md](BattleRequirements.md);
the mesh/instance layer itself is documented in [LocationMeshNetwork.md](LocationMeshNetwork.md).

Status: **v1.0 DRAFT** — requirements authored alongside the implementation; each SR gets a status
(PLANNED / IMPLEMENTED / VERIFIED-LIVE) as phases land.

## 1. Hosting and authority

- **SR-010 (Initial host).** The first player whose mission-ready request the server processes for a
  location instance (`"{settlementId}|{locationId}"`, ObjectManager ids) becomes the **location
  host**. Later entrants append to a successor line in arrival order. PLANNED
- **SR-011 (Host authority).** Only the host runs native settlement population spawning (roster
  characters, ambient crowd, animals). The host owns every non-player agent in the mission and
  replicates it to peers as a puppet. PLANNED
- **SR-012 (Non-host suppression).** Non-hosts spawn **zero** native population agents. Any native
  spawn observed on a non-host while suppression is active is a defect (a diagnostic log guards
  this). PLANNED
- **SR-013 (Election timing).** Suppression is unconditional until a host assignment arrives (host
  unknown during mission load). When the assignment names the local client, it runs the population
  pass explicitly on the game thread. PLANNED
- **SR-014 (Migration).** When the host leaves or disconnects, the server promotes the first
  successor still in the instance and re-broadcasts the assignment with a bumped epoch. The promoted
  client **adopts NPCs in place**: authority transfer, interpolator forget, settlement-AI re-creation
  (see SR-030). Non-promoted clients keep their puppets untouched — movement ids survive the
  transfer. PLANNED
- **SR-015 (Departure fork).** A departing player's own agent despawns on every remaining client
  (existing generic path); host-owned NPC puppets never despawn on host departure — they await
  adoption. PLANNED
- **SR-016 (Empty instance).** When the last player leaves, the server clears the host assignment.
  The epoch watermark survives so a re-entered settlement cannot reuse an old epoch. PLANNED
- **SR-017 (Rejoin).** A player re-entering the settlement gets a fresh mission, requests the host,
  and (if the instance is live) receives a catch-up replay of the host's current agents; a former
  host lands at the tail of the successor line. PLANNED

## 2. Population replication

- **SR-020 (Scope).** All non-player agents replicate: roster characters (notables, companions,
  prisoners), ambient crowd, and animals (horses via `sp_horse` scene points, sheep/cows/hogs/
  geese/chickens, day-gated exactly as native). The player agent and other players' puppets are
  excluded from capture. PLANNED
- **SR-021 (Capture).** Host-side capture is a postfix on `Mission.SpawnAgent(AgentBuildData, bool)`
  plus a second postfix on `Mission.SpawnMonster(EquipmentElement, EquipmentElement, in Vec3, in
  Vec2, int)` (animals bypass `SpawnAgent(AgentBuildData)` — see V3). PLANNED
- **SR-022 (Roster binding).** Every human NPC spawn record carries its `LocationCharacter` roster
  identity. Non-hosts bind the puppet to a **local** roster entry — an existing server-synced entry
  for heroes, a reconstructed entry (from embedded `LocationCharacterData`) for ambient — and build
  the puppet with that entry's `AgentOrigin`. This makes puppets first-class citizens of native
  bookkeeping (`IsAlreadySpawned`, `Location.GetLocationCharacter(agent.Origin)`, passage guards)
  on every client, and makes adoption re-binding exact (see V4). PLANNED
- **SR-023 (Visual fidelity).** Spawn records ship the rolled `Equipment`, body properties, clothing
  colors and gender so puppets match the host's visuals regardless of local RNG state. PLANNED
- **SR-024 (Movement).** NPC puppets ride the existing mission-generic movement pipeline with
  compact ushort ids under a per-mission minted movement scope. Free horses ride the existing
  mount-movement path; herd animals ride the standard packet (see V9). PLANNED
- **SR-025 (Catch-up).** A mid-mission joiner receives the host's full current population as a
  reliable-ordered chunked batch, excluding despawned/dead agents. PLANNED
- **SR-026 (Mid-mission churn).** Host-side roster adds (lords walking in, passage traffic) spawn
  natively on the host, are captured, and replicate; host-side fade-outs/removals replicate as
  despawns. The 30-second passage-usage tick runs **only on the host** (it mutates the roster with
  local RNG — see R2). PLANNED

## 3. Adoption (migration mechanics)

- **SR-030 (AI re-creation).** The promoted host revives an adopted human puppet by resolving its
  local `LocationCharacter` (via the origin binding of SR-022), then
  `GetComponent<CampaignAgentComponent>().CreateAgentNavigator(locationCharacter)` +
  `locationCharacter.AddBehaviors(agent)` + `Controller = AI` (verified native sequence, V5).
  Animals get `Controller = AI` only. No battle `AgentAiWaker.Wake` (would set Alarmed). PLANNED
- **SR-031 (Graceful fallback).** An adopted puppet whose roster entry cannot be resolved converts
  to a stationary AI agent with a warning log — never a crash. PLANNED
- **SR-032 (No duplicates).** After migration, the new host's native systems must not re-spawn
  adopted NPCs. Guaranteed structurally by SR-022: native `IsAlreadySpawned` matches by
  `agent.Origin == locationCharacter.AgentOrigin`, and adopted puppets carry the local entry's
  origin. VERIFIED-BY-DECOMPILE (live check outstanding)

## 4. Interactions and combat

- **SR-040 (Conversations).** Hero-NPC conversations keep the existing server lock. On lock grant
  the host pauses the NPC (`SetIsAIPaused`) so the remote conversation anchors to a stationary
  agent; released on conversation end. Ambient conversations stay local and unheld (accepted jank).
  PLANNED
- **SR-041 (Damage).** v1: non-hosts cannot damage NPC puppets (`LocationPvpBlockPatch` drops blows
  to `Controller == None` humans). Host-side NPC deaths replicate via the real Blow replayed inside
  a replicated-death scope exempt from the PvP block. PLANNED
- **SR-042 (Passages/doors).** Players use passages locally, unaffected. NPC puppets never use
  passages themselves (no navigator); host-side passage traffic arrives as spawn/despawn records
  (SR-026). PLANNED

---

## Appendix A — Native-API verification (decompiled 2026-08-04, game v1.2.x DLLs)

Verified against `SandBox.dll`, `TaleWorlds.CampaignSystem.dll`, `TaleWorlds.MountAndBlade.dll`
via ilspycmd. File/line references are to the decompiled sources.

- **V1 — Population cadence.** `MissionAgentHandler.SpawnLocationCharacters()` is called **once**
  from each mission controller's `AfterStart` (`TownCenterMissionController`,
  `VillageMissionController`, `IndoorMissionController`, `HouseMissionController`, plus special
  missions). There is no tick re-invocation. It first fires
  `CampaignEventDispatcher.LocationCharactersAreReadyToSpawn(FindUnusedUsablePointCount())` — the
  event ambient campaign behaviors (`CommonTownsfolkCampaignBehavior`,
  `TownMerchantsCampaignBehavior`, `CommonVillagersCampaignBehavior`,
  `RecruitmentAgentSpawnBehavior` — all registered on exactly this event) use to **add** ambient
  roster entries, sized by unused spawn-point capacity and scaled by time-of-day/prosperity/
  weather. Then it spawns each roster entry not already spawned and fast-forwards navigators
  (`SimulateAgent`, 35–50 ticks). ⇒ The host's late population pass must call the per-entry public
  APIs (`SpawnDefaultLocationCharacter` per unspawned entry + `SimulateAgent`) and **must not**
  re-call `SpawnLocationCharacters()` — re-firing the event double-adds roster entries.
- **V2 — Safe suppression points.** Void or null-tolerated (safe to prefix-skip):
  `SpawnLocationCharacters`, `SpawnDefaultLocationCharacter` (all consumers null-check or `?.`),
  `SpawnEnteringLocationCharacter`, `SpawnWanderingAgentWithDelay`. **Never** skip
  `SpawnWanderingAgentWithInitialFrame` — callers dereference its return unconditionally.
- **V3 — Animals.** `SandBoxHelpers.MissionHelper.Spawn{Horses,Sheeps,Cows,Geese,Chicken,Hogs}`
  iterate scene entities tagged `sp_horse`/`sp_sheep`/… and call `Mission.SpawnMonster(...)` →
  `CreateHorseAgentFromRosterElements` + `BuildAgent` — this **bypasses**
  `SpawnAgent(AgentBuildData, bool)`, so animal capture needs its own postfix on the core
  `SpawnMonster(EquipmentElement, EquipmentElement, in Vec3, in Vec2, int)` overload (byref pins).
  Animal spawn positions are scene-deterministic; town/village controllers gate the herd spawns on
  `!Campaign.Current.IsNight`.
- **V4 — Already-spawned bookkeeping.** `MissionAgentHandler.IsAlreadySpawned(origin)` =
  `Mission.Agents.Any(x => x.Origin == locationCharacter.AgentOrigin)` — **reference identity** on
  the roster entry's origin (`LocationCharacter.AgentOrigin => AgentData.AgentOrigin`).
  `GetAgentBuildData()` is an extension: `new AgentBuildData(locationCharacter.AgentData)`.
  ⇒ Building puppets from the local entry's `AgentData`/origin satisfies all native lookups.
- **V5 — AI re-creation.** `CampaignMissionComponent.OnAgentCreated` adds a
  `CampaignAgentComponent` to **every** agent in campaign missions (puppets included). The native
  per-agent spawn ends with `CreateAgentNavigator(locationCharacter)`,
  `locationCharacter.AddBehaviors(agent)`, `locationCharacter.AfterAgentCreated?.Invoke(agent)`,
  plus `SetActionSet(locationCharacter.ActionSetCode)` — the adoption recipe.
- **V6 — Player spawn.** `SandBoxHelpers.MissionHelper.SpawnPlayer` — fully disjoint from all
  suppressed entry points; it does go through `Mission.SpawnAgent`, so host capture excludes the
  player agent explicitly.
- **V7 — Conversations.** `MissionConversationLogic.OnAgentInteraction` →
  `StartConversation(agent)` → `ConversationManager.SetupAndStartMissionConversation`. The hold on
  the host is `agent.SetIsAIPaused(true/false)` for v1.
- **V8 — Removal virtuals.** `MissionBehavior.OnAgentDeleted(Agent)` fires when the engine deletes
  an agent (fade-out completion; state → `Deleted`); `OnAgentRemoved(affected, affector, state,
  blow)` fires on kill/KO/rout removal. Both are plain MissionBehavior overrides — no Harmony
  needed for despawn/death capture.
- **V9 — Animal movement.** The mod's movement `AgentData` packet reads only universal `Agent`
  members (position/direction/velocity/input/flags) — herd animals ride the standard packet path;
  free horses ride the existing masterless-mount path (`MountMovementPacket`).

## Appendix B — Design refinements locked during verification

- **R1 — Roster reconstruction replaces parallel seeded rolling.** Non-hosts suppress
  `SpawnLocationCharacters` wholesale (the roster event never fires locally), so their rosters hold
  only server-synced hero entries. Ambient puppet records embed `LocationCharacterData`; the
  non-host binder first tries an existing **unbound** local entry for the same character (heroes,
  merchants), else reconstructs one via `LocationCharacterFactory` and the existing apply-side
  helpers. This removes the fragile cross-client determinism contract (entry-time-dependent
  time-of-day/weather/prosperity counts) that occurrence-index matching would have needed; the
  ambient seed patch (`AmbientSpawnSeedPatch`) remains only for the host's own roll and is a
  retirement candidate after live validation. `CivilianAgentCountPinPatch` stays — it makes crowd
  size independent of which player happens to be host.
- **R2 — Passage tick is host-only.** `LocationComplex.AgentPassageUsageTick()` (every 30 s from
  `MissionAgentHandler.OnMissionTick`) picks a **random** non-fixed roster character from another
  location and moves it into the current one (`MBRandom`). On non-hosts this mutates the roster
  with unsynchronized RNG — it is suppressed alongside the spawn entry points.
