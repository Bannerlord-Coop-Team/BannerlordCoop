# Porting co-op to Bannerlord v1.5.x

This page records what changed between Bannerlord v1.4.8 and v1.5.4 and how co-op follows it on the
`bannerlord-1.5.x` branch. `development` stays on v1.4.8. Use it to review the port, and as a
checklist when porting other code that runs against the game, such as the dedicated server. The
epic #3836 tracks the v1.5 work that remains.

## Branch and commits

The port landed on `bannerlord-1.5.x` as twelve commits on top of `development` at `5f4d63cc`. The
first eleven port co-op to v1.5.3, one area each. The twelfth follows v1.5.4, which replaced v1.5.3 on
the Steam beta on 2026-10-05.

| Commit | Area |
|---|---|
| `1f79bc389` Target Bannerlord v1.5.3 | Version pins |
| `ebd51807c` Move map event state onto the v1.5 components | Map events |
| `4411780ec` Follow the v1.5 visibility rework | Visibility and hideouts |
| `57e655f0d` Port the clan screen to v1.5 party configuration | Clan screen |
| `fdcd2469c` Follow the v1.5 village hostile action split | Villages, settlement leave |
| `4fa2addcb` Follow the v1.5 execution and hero changes | Executions, heroes, companions |
| `0a7fa23f4` Use the v1.5 kingdom FormalName | Kingdoms |
| `da677fa7f` Port v1.5 perk and trait effect changes | Perks, models, mercenaries |
| `bd2356501` Follow other v1.5 campaign API changes | Map trackers, incidents, new behaviors |
| `1bf6d75e2` Follow v1.5 mission changes | Missions |
| `9cca25cd2` Update tests for Bannerlord v1.5.3 | Tests |
| `0d8dd2280` Follow Bannerlord v1.5.4 | v1.5.4 changes |

A second comparison of v1.4.8 and v1.5.4 behavior after the port found more gaps, fixed in:

| Commit | Area |
|---|---|
| `9fa7f447e` Replicate the v1.5 party command resets | Clan screen |
| `453e8a842` Follow more v1.5 campaign changes | Heroes, executions, barters, governors |
| `b02090451` Keep the hideout ambush deployment with co-op | Missions |
| `8f4be3d91` Keep AI from attacking next to stronger players | AI |
| `96bcc4718` Sync volunteers taken by garrison auto-recruitment | Recruitment (not a v1.5 change; the bug is also on `development`, PR #3816) |
| `68b137a70` Fix review findings in the v1.5 follow-ups | AI, captures, tests |

Each commit body lists its changes.

## Engine changes and how co-op follows them

### Map events

- The battle type, the battle settlement and the siege flags moved from `MapEvent` onto
  `MapEventComponent` subclasses. `MapEvent.EventType` now reads `Component.GetBattleType()`, so a map
  event without a component throws.
- Siege assaults, sally outs and siege outside battles got their own components, and
  `BlockadeBattleMapEvent` became `BlockadeBattleEventComponent`. Each has a registry and synced
  fields in `source/GameInterface/Services/MapEventComponents`.
- `MapEventManager.Start*` is gone. Battles are created through the component factories in
  `MapEventBattleFactory`.
- `MapEvent.Initialize(attacker, defender, component)` creates the map event visual itself. A host
  without a map event visual creator gets a no-op visual from `HeadlessMapEventVisualPatch`.
- Pulling nearby parties into the player's battle moved to `AddNearbyPartiesToPlayerMapEvent` on each
  component. Co-op skips it on every component (`SkipAddNearbyPartiesToPlayerMapEventPatch` in
  `PlayerEncounterPatches.cs`).
- Looting goes through the component `CanLoot*` checks (`MapEventResultsInterface`).
- Objects created on a client from the network skip their constructors. The v1.5 field battle
  component keeps party position lists that only its constructor and `OnAfterLoad` create, so
  `FieldBattleEventComponentRegistry.OnClientCreated` calls `OnAfterLoad`. Without it, a party joining
  a field battle on a client throws.

### Visibility and hideouts

- `MobileParty.IsSpotted`, `PartyBase.UpdateVisibilityAndInspected`, `ISpottable` and
  `Hideout.IsSpotted` are gone. Visibility comes from `MapVisibilityModel`, and a spotted hideout is a
  visible settlement.
- `PartyBaseVisibilityExtensions.UpdateVisibilityAndInspected` keeps the old entry point on top of the
  model, including co-op's rule for parties inside settlements. The server patch moved to the
  `MobileParty.IsVisible` getter (`PartyVisibilityServerPatches.cs`).
- `Settlement.IsVisible` is synced in place of `Hideout.IsSpotted` (`HideoutSync.cs`). A new
  settlement is constructed visible; `Hideout.OnInit` hides a hideout.

### Clan screen and party configuration

- `ClanPartyItemVM` is abstract, with `ClanPartyItemWithPartyVM` and `ClanPartyItemWithHeroVM` rows.
  Co-op patches both.
- The party objective dropdown became four per-leader toggles stored on the hero (`CanJoinArmy`,
  `CanRaid`, `CanDonateTroopsToGarrison`, `CanHaveFleet`). Changes are sent to the server and the
  hero properties are synced; `PartyConfigurationBinaryPackage` serializes the new
  `PartyConfiguration`.
- Role assignment moved to `ClanPartiesVM.AssignHeroToRole`. AI army calls skip lords who may not
  join armies, as v1.5 does.
- v1.5 resets a hero's commands when the hero stops being a companion, becomes the player character
  or leaves the player clan, by clearing a field that is not synced. The server makes each reset
  through the synced hero properties and counts every player's clan as the player clan
  (`PartyConfigurationCampaignBehaviorPatches.cs`). The server's heir switch never raises the player
  character event, so it resets the heir itself (`HeirSelectionHandler.cs`).

### Villages and leaving settlements

- The village behavior's `StartHostileAction` is gone. `WillVillageResistHostileAction` now sends each
  raid or force action into a resist or a no-resist menu, each with its own continue consequence.
  Co-op intercepts the resist continue consequences and treats every village as resisting, so the
  encounter path is always used (`VillageHostileActionStartPatch.cs`).
- "Forget it" on the hostile action menus returns to the village menu, so it is no longer a
  settlement leave.
- A party at sea leaves a settlement at its port, still sailing.

### Executions, heroes and companions

- Executions moved into `ExecutionCampaignBehavior` with a blood feud system, and
  `CharacterRelationCampaignBehavior` no longer handles hero deaths. `ExecutionCampaignBehavior` runs
  on the server only (`DisableExecutionCampaignBehavior.cs`).
- Several vanilla methods co-op re-implements changed; the port follows them (stealth equipment in
  heir selection, aging and execution marks, kingdom discontinuation, companion removal, companion
  party top-up, execution scenes, blood feud prisoner sales).
- The execute answer to a defeated lord now opens a prompt whose "Forget It" takes the lord prisoner.
  A client cannot take prisoners itself, so co-op sends that capture to the server like the capture
  answer (`LordConversationsCampaignBehaviorPatches.cs`).
- A calculating player loses relation on first meeting a town notable. The server applies it to the
  player who met the notable, on the first meeting it records for that player (`HeroMeetingHandler.cs`).

### Kingdoms

- `EncyclopediaTitle` became `FormalName`. `ChangeKingdomName` and `KingdomManager.CreateKingdom`
  take the formal name.

### Perks, trait effects and models

- `HasPerk` and the `PerkHelper` methods take a land or naval environment, and the scattered
  `IsCurrentlyAtSea` checks moved into perk metadata.
- Personality traits gained effects in several models. The party wage model replacement
  (`DefaultPartyWageModelInterface.cs`) was re-derived from v1.5.3.
- Player governors count as present for the new governor trait effects, as co-op already did for
  governor perks (`CoopClanGovernorPatches.cs`, and the garrison wage in the wage model replacement).
- A generous hero can hire more mercenaries than the town stock, on the client and the server.

### Other campaign APIs

- `MapTrackerProvider` became `Campaign.MapTrackerManager`.
- Incidents moved to `IncidentManager`.
- `Campaign` takes `AdvancedStartOptionsData` (`GameStateInterface.cs`).
- Party morale changes are floats.
- Safe passage no longer hides the bribed parties from the AI for 32 hours; they decide again at once.
  Co-op's lord and bandit barter handlers do the same.
- v1.5 stops an AI party from starting any attack while a main party stronger than itself is near
  it. Co-op applies that check to every active player party; an offline player's party is parked
  and does not count (`DefaultMobilePartyAIModelPatches.cs`).

### New v1.5 campaign behaviors

These change synced state, so they run on the server only (`DisableV15CampaignBehaviors.cs`):
`HeroDailyXpCampaignBehavior` (player heroes get no daily xp, as vanilla gives none to the player),
`EmptyClanPartiesCampaignBehavior` (clients report no empty clan parties, since that list belongs to
the server's player clan) and `PartyConfigurationCampaignBehavior` (its resets reach clients through
`PartyConfigurationCampaignBehaviorPatches.cs`, above). `BattleWreckageCampaignBehavior` is off on
every role (below).

### Missions

- `Mission.SpawnAgent` takes two more optional parameters.
- The hideout ambush controller fields are read only and are written through `FieldRefAccess`.
- Deployment finishes through `Mission.OnInitialSpawnCompleted` (`CoopHideoutMissionLogic.cs`). The
  hideout ambush controller now also finishes it in `OnAfterMissionLoadingFinished`, while the mission
  is still loading; co-op's controller skips that, so deployment still waits for the battle session.
- Save loading moved to `SaveLoadVM.LoadSavesAsync` (`MissionsLoadUI.cs`).

## Changes in v1.5.4

- Lord defection no longer prunes old attempts; a refusal cools down for one season. `NeverExpire`
  keeps every refusal in its cooldown (`LordDefectionRetryPatches.cs`).
- `MapVisibilityModel` moved its fixed cases (true sight, the player's captor, inactive parties,
  garrisons, militia) into `IsVisibilityPersistent`. Co-op's visibility update leaves those parties
  as they are, like the vanilla sweep.
- `KingdomManager.CreateKingdom` lost its separate formal name argument.
- Re-implemented methods follow v1.5.4: abdication passes the throne to the most influential clan
  that may rule, the siege aftermath army member test changed, and selling no prisoners sends nothing.

## v1.5.5

The branch builds and tests against v1.5.5 with no code change (2026-10-21, Steam beta, with
`mb2` pointing at that install):

- Release build: 0 errors. The engine member audit resolves every reference (GameInterface 6,371,
  Coop.Core 205, Missions 1,485; 0 unresolved).
- Unit tests: 4,189 passed, 14 skipped, 0 failed.
- E2E tests in 16 shards: 2,712 tests, 2,707 passed, 5 skipped, 0 failed.

## Known gaps

- Village force actions always take the encounter path; v1.5's no-resist path is not supported yet.
- Blood feuds and empty clan parties are keyed on the local main hero and player clan. They run on
  the server only and are not multiplayer-aware yet (blood feuds: #3819).
- Naval (War Sails) content stays unsupported, including the set sail and disembark leave paths.
- v1.5 leaves a `BattleWreckage` after a field battle with a winner and at least 15 casualties: a
  battle site on land, a wreckage at sea. `BattleWreckageCampaignBehavior` is disabled on the server
  and on clients (`DisableBattleWreckageCampaignBehavior`). Co-op's `MBObjectBasePatches.StringIdPrefix`
  drops the `StringId` of a server-made wreckage, and vanilla's `CampaignObjectManager.AddWreckage` adds
  it to the saved list before it reads that id, so a field battle that made a wreckage threw inside
  `MapEvent.FinalizeEventAux` and left an id-less wreckage in the save. Vanilla needs 15+ casualties,
  a winner, no quest party, fewer than 50 wreckages, a qualifying party mix and a site away from
  settlements and rivers, and makes small sites 85% of the time. It was not only AI battles:
  `IsPlayerMapEvent` checks the server's own main party, so a co-op player's field battle could throw
  too. Every later load then
  crashed in `CampaignObjectManager.InitializeManagerObjectLists` (seen 2026-10-22 on v1.5.5).
  - What this loses: looter-faction parties no longer head for land battle sites (the only parties that
    target land wreckages). Players never saw wrecks
    in co-op anyway, since nothing syncs them (#3837, #3838). Re-enabling needs the wreckage to get an
    id (a sync registry, or `typeof(BattleWreckage)` in `MBObjectBasePatches.allowedTypes` after
    proving `CreateWreckage`'s log entry and map interactable work under co-op).
  - Save repair: `IdlessWreckageLoadRepairPatch` is a prefix on `CampaignObjectManager.InitializeOnLoad`
    (`OnGameLoaded` fires too late) and removes wreckages with a null `StringId` before the object
    lists are built, on the server and on clients, logging how many it dropped. Saves already
    written by an affected server load again once the fix is deployed on both sides. Checked live on
    2026-10-22: a server save hit by 7 throws logged "Dropped 7 battle wreckages saved without an id",
    loaded, and a client that had crashed on it rejoined.
  - The wreckage repair does not undo the rest of the damage. A throw skipped the end of
    `FinalizeEventAux`, so that battle's parties stay attached to an event that never finished, and an
    AI encounter with such a party threw `NullReferenceException` in `PartyBase.set_MapEventSide`
    from `StartBattleAction` (seen twice on 2026-10-22, events `Created_139` and `Created_171`). A
    save made after the fix has none. For an affected save, the server now runs a load repair at
    `CampaignReady` (`MapEventLoadCleaner.RepairOrphanedMapEvents`) that releases the parties of
    every half-finished event a party or settlement still links to, logging one Warning per event.
    Live check on a damaged save owed.
  - Both load passes (the player map event pass and the orphan pass) run before the server state
    that lets lifetime patches publish. Each pass therefore records the campaign's parties first and
    unregisters every one that left the campaign during the pass, including a party destroyed
    elsewhere by a cascade (a ruler killed in the battle can destroy a kingdom's clans). A throw while
    one event is handled is logged as an Error, and the remaining events, the other pass and server
    start still run. In the player pass, the army disband and party release run even when the
    finalize throws.
  - Open (follow-ups): the player pass finalizes an unlisted live event without first restoring its
    unsaved state, so that finalize can throw; the orphan pass then releases the event only if
    vanilla had already set `WaitingRemoval`. A successful `FinalizeEvent` in the player pass
    publishes neither `MapEventFinalized` nor `InstanceDestroyed<MapEvent>`, because the sync policy
    still allows originals at that point.
  - A wreckage already in a hosted save (for example from a single-player v1.5 campaign) no longer
    expires, because the server no longer registers the hourly tick. Clients still draw it and can
    click it, which opens a menu clients never registered (existing client path; in-game check owed).
- Open: v1.5 spawns deserter looter parties with a null `PartyTemplate`
  (`BanditPartyComponent.CreateLooterParty(..., null, ...)` from `DesertersCampaignBehavior`).
  `BanditPartyComponentHandler` logs a failed template lookup and returns, so those parties' setup
  never reaches clients. The fix is to allow a null template on both the send and receive paths, as the
  caravan handler does for a null leader. Needs its own issue; client-side effect unchecked.
- A failed courtship can be retried after a season in v1.5. The co-op server refuses that retry, and a
  client loses its courtship attempt history when it reconnects.
- Two v1.5 AI reactions check the server's main party and player clan, so they never fire for co-op
  players: when a player's clan changes kingdom, lords no longer at war with it keep chasing the
  player until their next AI decision, and an AI party that agreed not to attack a player can still
  pick the settlement that player last attacked.
- v1.5 lowers a calculating player's relation with a town notable at their first meeting. Co-op
  clears a player's meeting records when an heir takes over (`CoopSessionMigrationRules.cs`), so a
  calculating heir takes that penalty again with notables the family had already met; vanilla's heir
  keeps the met state and does not.
- v1.5 kills a player marked to die in battle when the battle ends. That listener checks the server's
  main hero, so a co-op player probably dies at the next daily tick instead; not checked in game.

## Porting the dedicated server

The dedicated server is built from this repository's module plus its own host program, so the
co-op logic it runs is already ported. The host program's port is on the `bannerlord-1.5.x` branch
of `Bannerlord-Coop-Team/BannerlordCoop.DedicatedServer`, which tracks its status in
`docs/bannerlord-1.5.x-port.md`; it compiles against v1.5.4 and has not booted yet. When porting
host code, check first wherever it:

- creates or loads a campaign: `new Campaign(CampaignGameMode.Campaign, new AdvancedStartOptionsData())`;
- lists or loads saves: `SaveLoadVM.LoadSavesAsync`, then `RefreshSaves`;
- runs without a map event visual creator, or relies on map trackers (`Campaign.MapTrackerManager`);
- creates kingdoms: `KingdomManager.CreateKingdom` without the separate formal name argument;
- loads saves on a host whose `MBSaveLoad.CurrentVersion` is empty: `IsUpdatingGameVersion` is then
  false for every save, so the v1.5 save migrations never run and a pre-v1.5 save fails to load (one
  of the dedicated server's two port blockers, fixed on its branch);
- compares game versions with clients (v1.5.4), or lists the modules it loads. `ModuleValidator`
  refuses DLC modules (`ValidateNoDlc`, which catches the modules `deploy/Server Instructions.md` tells
  hosts to turn off) and leaves modules whose id starts with `DedicatedServer.` out of the module
  comparison.

Method that worked for this port: compile against v1.5.4; apply every Harmony patch the way
`source/GameInterface.Tests/PatchTest.cs` does, so a missing target fails the run; diff the body of
every patched game method between the v1.4.8 and v1.5.4 decompiles and port the changes into any
patch that replaces the original; then run the tests and a live session with the server and two
clients.

## Verification

- The whole solution builds against v1.5.4. Every Harmony patch class binds, except
  `MobilePartyAIRobustnessPatches`, whose class-level `[HarmonyPatch]` is commented out on
  `development` too.
- The bodies of all 2,336 game methods GameInterface patched at the port (`0d8dd2280`) were diffed
  from v1.4.8 to v1.5.3 and from v1.5.3 to v1.5.4, and the changes reviewed. The patch targets added
  since were written against v1.5.4.
- Unit tests (4,103 passed, 14 skipped) and E2E tests (2,712, 5 of them skipped as on `development`)
  pass in Release. On Windows, run E2E in 16 shards
  (`sh ../.github/scripts/run-e2e-shard.sh <n> 16` from `source`): with 8, a shard's test filter can
  exceed the command-line length limit.
- v1.5.5: see the v1.5.5 section above.
- Not done yet: a live session with a server and two clients, a dedicated server boot on v1.5.4 or v1.5.5, and
  CI, whose build image still carries v1.4.8 game assemblies.
