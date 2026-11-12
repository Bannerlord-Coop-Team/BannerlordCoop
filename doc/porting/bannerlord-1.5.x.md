# Porting co-op to Bannerlord v1.5.x

This page records what changed between Bannerlord v1.4.8 and v1.5.7 (the branch was ported to v1.5.3,
then v1.5.4, built unchanged on v1.5.5, was ported to v1.5.6, and now builds against v1.5.7) and how
co-op follows it on the
`bannerlord-1.5.x` branch. `development` stays on v1.4.8. Use it to review the port, and as a
checklist when porting other code that runs against the game, such as the dedicated server. The
epic #3836 tracks the v1.5 work that remains. Each port commit body lists its changes.

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

These change synced state, so they run on the server only (`HeroDailyXpCampaignBehaviorPatches`,
`EmptyClanPartiesCampaignBehaviorPatches` and `PartyConfigurationCampaignBehaviorPatches`):
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

## Changes in v1.5.6

- `MobileParty.HasPerk` has one overload, `HasPerk(PerkObject perk, bool checkSecondaryRole = false)`,
  which reads `CurrentBattleEnvironment` and no longer hands back the perk owner. `PerkHelper`'s
  `AddPerkBonusForCharacter` and `AddEpicPerkBonusForCharacter` take the `BattleEnvironment` last, and
  the epic one gained `effectiveSkill = -1`; the wage model replacement passes both as vanilla does.
- `Ship.ResetUnlockedUpgradePieces` is gone. The `Ship.CanHaveUnlockedPieces` setter builds the piece
  list from the new `CanHaveUnlockedUpgradePiece` event, which War Sails answers with the main party,
  so co-op patches the setter and answers that event yes for every player's party
  (`ShipMainPartyPatches.cs`, `ShipPiecesPatches.cs`).
- `TraitLevelingHelper.OnLordExecuted` is gone; the honor and mercy loss comes from
  `ExecutionCampaignBehavior` starting a blood feud for the main hero, so the `KillCharacterAction`
  replacement charges a player killer itself (`ApplyExecutionTraitPenalty`).
- `MapTrackerManager.Refresh(party)` adds or removes the tracker (`PlayerMapTracking.cs`).
  `castle_outside` has its own `castle_outside_on_init`, which `SavedMenuLoadPatch.cs` also reads.
- `MapEventSide` gained ship lists created by field initializers and `OnLoad`, so
  `MapEventSideRegistry.OnClientCreated` calls `OnLoad`.
- Ransom offers ask the new `CampaignEventDispatcher.CanHeroBeReleased`, and ransom and mercenary
  offers skip a player in `IsInNavalAutoTravel` instead of `IsInRaftState`; co-op's copies follow.
- AutoSync `*_DynamicPatches`, `*CollectionPatches` and `HeroFieldPatches` transpile every method a
  type declares, so a new vanilla method on a synced type is covered without a change.

## Changes in v1.5.7

- `CraftingCampaignBehavior.CreateTownOrder` takes the `Town` instead of reading the owner's current
  settlement. `TownOrderCreated` and `NetworkCreateTownOrder` carry the town, because the owner may
  have left it by the time a client applies the order.
- `MapEventParty.OnTroopScoreHit`, `MapEventSide` and `PartyGroupTroopSupplier` gained a `captain`,
  used by `CombatXpModel.GetXpFromHit`. `CoopAgentOrigin` sends it in `NetworkTroopScoreHit`
  (`CaptainId`, null without one); an unresolved captain is logged and the hit still counts.
- `SiegeAftermathCampaignBehavior` saves `_settlementProsperityCache` and resets it in
  `OnMapEventEnded`. Clients never run its writer, so co-op's client branch and
  `SwitchLocalPartyToSettlementTaken` reset it, otherwise their aftermath tooltips show a wrong loss.
- `CalculateMobilePartyMemberSizeLimit` and `GetPartyMemberSizeLimit` take `isCurrentlyAtSea`; the
  player caravan prefix reads it.
- `DisbandPartyCampaignBehavior.OnMobilePartyDestroyed` sets the AutoSynced `IsDisbanding` after the
  destroy is announced, so a client can log one `Failed to get` line.
- `IssuesCampaignBehavior.OnGameLoaded` migrates saves older than v1.5.6.125291 on the server
  (behind `MBSaveLoad.IsUpdatingGameVersion`): it removes dead heroes, and disabled heroes outside
  `Clan.PlayerClan`, from rosters. Co-op player clans are not `Clan.PlayerClan`, but co-op disables a
  hero only for deletion, heir selection and creation rollback.

## Loading a v1.4.8 save (#4615)

Vanilla v1.5.7 migrates a save written before v1.5.0 while it loads. The server loads the save; a
client loads the server's transfer save, which carries v1.5.7 in its metadata
(`SaveInterface.EnsureUsableGameVersion`), so the version-gated migrations run on the server only.
They all run inside `Campaign.DoLoadingForGameType` (`OnGameLoaded`, then `OnSessionStart`), before
the server enters `ServerRunningState`, so `ServerSyncPolicy.AllowOriginal` is true and the AutoSync
setters and the `MapEventSide.RemovePartyInternal` prefix run vanilla and publish nothing. Players
are registered later, at `CampaignReady` (`MapScreen.OnInitialize`), through
`RegisterAllGameObjects` and `SaveGameHandler.Handle_AllGameObjectsRegistered`.

- `MapEvent.OnAfterLoad` removes every party that fails `CanPartyJoinBattle`, behind
  `MBSaveLoad.IsUpdatingGameVersion && LastLoadedGameVersion.IsOlderThan("v1.5.0")`. No change
  needed: `Postfix_CanPartyJoinBattle` (`MapEventPatches.cs`) reads player parties through
  `PlayerManager.TryGetControlledObjectInfo`, which is empty until the players are registered, so
  `ContainsPlayerParty` is false and the postfix returns vanilla's result for every party. The
  missing AI join window doesn't strip AI parties. A player battle, siege or raid still in progress
  is then finalized at `CampaignReady` by `MapEventLoadCleaner`, as for any loaded save.
- `VillageHostileActionCampaignBehavior.OnGameEarlyLoaded` rewrites the removed village menu ids
  in `MapStateData.GameMenuId` and finishes the encounter. Not version-gated. No change needed: it
  reads the save's own menu, which is the server's, and `SavedMenuLoadPatch` drops any menu the
  server has. A rejoining client's menu comes from its party state (`HeroInterface.SwitchToPlayer`,
  `SiegeEventInterface.RestoreReloadedPlayer`), not from the save's menu id.
- `MapEventComponent.AfterLoad` runs `UpdateMapEventSettlement` for every unfinished event the map
  event manager lists, on every load. v1.4.8 saved the settlement on `MapEvent` (save id 103) and
  v1.5 saves it on the component (id 100), so a v1.4.8 save has none until this runs. For listed
  events it derives the same settlement v1.4.8's `MapEvent.Initialize` set, and the AutoSync setter
  publishes nothing during the load. An event the manager doesn't list never gets it, so the
  orphan pass found no village to reset after an orphaned raid or force action; `MapEventLoadCleaner`
  now restores an unlisted event's unsaved state before it resets the village.
- `MapEvent.OnLateLoad` creates the siege, sally out and siege outside components the v1.4.8 save
  lacks, behind `LastLoadedGameVersion < "v1.5.0"`, for listed and unlisted events alike.
- Not checked live: a v1.4.8 dedicated server save with a player battle, a siege and a village raid
  in progress loading on the v1.5.7 server.

## Known gaps

- Village force actions always take the encounter path; v1.5's no-resist path is not supported yet.
- Blood feuds and empty clan parties are keyed on the local main hero and player clan. They run on
  the server only and are not multiplayer-aware yet (blood feuds: #3819).
- Naval (War Sails) content stays unsupported, including the set sail and disembark leave paths.
- v1.5 leaves a `BattleWreckage` after a field battle with a winner and at least 15 casualties: a
  battle site on land, a wreckage at sea. `BattleWreckageCampaignBehavior` is disabled on the server
  and on clients (`BattleWreckageCampaignBehaviorPatches`). Co-op's `MBObjectBasePatches.StringIdPrefix`
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
    save made after the fix has none.
  - Load repair, for an affected save. At `CampaignReady` the server runs
    `MapEventLoadCleaner.FinalizePlayerMapEvents`, then `RepairOrphanedMapEvents`, then enters the
    running state (`InitialServerState`). The player pass handles each event in three stages: repair
    the party-side links, finalize, then disband the army and release the parties. The orphan pass
    releases the parties of every finalized event that a party or settlement still links to, logging
    one Warning per event; an unlisted live event is only logged. A throw in a stage is logged as an
    Error, and the remaining events, the other pass and server start still run, where one throw used
    to exit the dedicated server (exit 3). The army disband and party release run even when the
    finalize step throws, but not when the link repair throws.
  - Both passes run before the server state that lets lifetime patches publish, as on `development`,
    so a party, army or battle they end keeps its registry id and an object they create gets none.
  - Checked on 2026-10-22 on a dedicated server with a copy of the damaged save: it released 7
    events (`Created_97`, 139, 141, 149, 171, 187 and 201), unregistered the 7 parties vanilla
    destroyed while releasing them, then loaded, served and autosaved. Not yet checked: that no
    `set_MapEventSide` exception follows once time runs. With no client connected the pulse line
    reads `timeMode=Stop players=0`, so that check waits for a client. On 2026-10-23 a modded server
    with one client ran 11 in-game days on a new campaign without that exception; time running on a
    repaired damaged save is still unchecked.
  - Follow-ups, done: the player pass restores an unlisted live event's unsaved state before it
    finalizes it, so the field battle party removal no longer throws before the component finalizes
    (#4615), and the orphan pass ends an unlisted live event whether or not vanilla set
    `WaitingRemoval` (the merge below). A successful `FinalizeEvent` in that pass still publishes no
    `MapEventFinalized` or `InstanceDestroyed<MapEvent>`, as on `development`.
  - A wreckage already in a hosted save (for example from a single-player v1.5 campaign) no longer
    expires, because the server no longer registers the hourly tick. Clients still draw it and can
    click it, which opens a menu clients never registered (existing client path; in-game check owed).
- v1.5 spawns deserter looter parties with a null `PartyTemplate` (co-op's own
  `DesertersCampaignBehaviorPatches` calls `CreateLooterParty(..., null, ...)`). The bandit init
  args handler now accepts a null template on the send and receive paths, as the caravan handler does
  for a null leader, so the server no longer logs a failed id lookup on every deserter spawn. Players
  saw no difference before: clients never run the vanilla hook that reads those init args, and the
  party's clan and home settlement reach them through the party's own sync.
- Open: in the E2E harness a client's new looter party sits at (0, 0) instead of its spawn position,
  with or without a template. Unchecked whether a live client corrects it through later movement
  sync. Also unguarded: a bandit component created with no init args at all (vanilla's party
  conversion actions) makes the send handler throw; the broker logs it. No caller in co-op found.
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
- v1.5.7 `MenuHelper.EncounterAttackConsequence` and `EncounterOrderAttack` skip
  `BeHostileAction.ApplyEncounterHostileAction` when the opposing leader party is a villager or
  caravan party. The co-op copy, `MapEventHostileActionConsequences.Apply`, does not, so attacking
  such an encounter of a faction not at war still declares the war and costs 10 relation on the
  server. Left as it is: a second player can join that fight without the conversation, and whether it
  starts a war is a maintainer's decision. No test covers the copy.
- v1.5.7 `AnimationPoint.Tick` no longer sets the loop start action channel while a pair state is
  active. Co-op migrates paired points between clients (`LocationAuthorityMigrator`,
  `LocationPointUseLifecycle`); it needs a two-client tavern check.
- `CoopTroopSupplier.OnTroopScoreHit` is an empty method nothing calls, still with the v1.5.6
  parameters.
- N executions from one clan cost N penalties, where vanilla charges once per blood feud; per player
  feud state does not exist (#3819), and v1.4.8 also charged each execution.
- Not done yet: a live session with a listen-server host and two clients, and CI, whose build image
  still carries v1.4.8 game assemblies.

## Porting the dedicated server

The dedicated server is built from this repository's module plus its own host program, so the
co-op logic it runs is already ported. The host program's port is on the `bannerlord-1.5.x` branch
of `Bannerlord-Coop-Team/BannerlordCoop.DedicatedServer`, which tracks its status in
`docs/bannerlord-1.5.x-port.md`. It builds against v1.5.5 and has booted to `SERVING` with clients
joined (2026-10-21 and 22). When porting host code, check first wherever it:

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

The TaleWorlds dedicated server moved to v1.5.7 on Steam on 2026-11-10 (app 1863440, buildid
26569029). From v1.5.6 it moves to .NET 8, while `BannerlordCoop.DedicatedServer` pins .NET 6
(`scripts/prepare-dotnet-runtime.ps1` accepts only a `6.0.x` runtime), so its host needs a .NET 8
runtime.

A staged server that carries a module's `ModuleData` but not its art has no skeleton for the action
sets that module declares, so each bone lookup in its `Monsters` XML fails with
`WARNING: could not find bone index for: <skeleton>::<bone>` and an "Always Ignore?" message box (150
of them took about 8 minutes on one boot, 2026-10-22). The server engine loads
`<module>/DsAssetPackages/*.tpac` for every module and the client never reads that folder, so a module
can ship a skeleton-only package there (TAOM's `taom_creature_skeletons.tpac` cut the load from 480 s
to 11 ms). Copying a module's whole client `.tpac` packages crashed native init.

Method that worked for this port: compile against the new version and check that every referenced
engine member resolves (TAOM's `tools/check_engine_member_refs.ps1`); apply every Harmony patch the
way `source/GameInterface.Tests/PatchTest.cs` does, so a missing target fails the run (every class
binds except `MobilePartyAIRobustnessPatches`, commented out on `development` too); diff the body of
every patched game method between the old and new decompiles (TAOM's `tools/engine_body_diff.py`) and
port the changes into any patch that replaces the original; then run the tests and a live session
with the server and two clients. A version bump moves `source/Coop/Coop.csproj` `GameVersion`,
`README.md` and `TestApplicationVersionPatch.FallbackVersion`. On Windows, run E2E in 16 shards
(`sh ../.github/scripts/run-e2e-shard.sh <n> 16` from `source`); with 8, a shard's test filter can
exceed the command-line length limit.

## Merge of development (2026-10-30)

`development` at `c8233b09b` was merged in, and the load repair flow follows its API, so the orphan
pass ends a live event the manager no longer lists. The load repairs run while originals are allowed,
so lifetime patches publish nothing there, as on `development`. When `development` is merged again, diff the Harmony targets it added or changed against
v1.5, because copies written on v1.4.8 (the ransom and mercenary offers) still ran v1.4.8 logic.
