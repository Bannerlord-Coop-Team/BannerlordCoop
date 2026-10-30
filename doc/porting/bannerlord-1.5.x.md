# Porting co-op to Bannerlord v1.5.x

This page records what changed between Bannerlord v1.4.8 and v1.5.6 (the branch was ported to v1.5.3,
then v1.5.4, built unchanged on v1.5.5, and now builds against v1.5.6) and how co-op follows it on the
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

## v1.5.6

v1.5.6 changed the perk lookup signatures, which broke the Release build at 11 call sites in 6
GameInterface files. The port changes only those call shapes (2026-10-29, with `mb2`
pointing at that install, on top of `8e738614b`).

- `MobileParty.HasPerk` has one overload, `HasPerk(PerkObject perk, bool checkSecondaryRole = false)`.
  v1.5.5 had `HasPerk(perk, out Hero perkOwnerHero, bool checkSecondaryRole = false)` and an overload
  that also took a `BattleEnvironment`. v1.5.6 reads the environment from `CurrentBattleEnvironment`
  (through `PerkHelper.TryGetPerkEffectBonus`) and finds the owner through the internal
  `GetPerkOwnerHero`, so a caller no longer gets the perk owner back. Co-op discarded it (`out _`)
  at every site.
- `PerkHelper.AddPerkBonusForCharacter` and `AddEpicPerkBonusForCharacter` moved their
  `BattleEnvironment` from the second parameter to an optional last one (default
  `BattleEnvironment.Any`), and `AddEpicPerkBonusForCharacter` gained an optional
  `effectiveSkill = -1` before it. The wage model replacement passes `-1` and the party's
  `CurrentBattleEnvironment`, the argument shape vanilla v1.5.6 `DefaultPartyWageModel.GetTotalWage`
  passes, so the land or naval environment still applies.
- No vanilla logic change needed porting. Diffed against v1.5.5, the vanilla methods these files
  copy changed only in these calls; `GetTotalWage` also drops discarded `_ =` statements, and its
  `AidCorps` handling and trait and governor tail are the same in both versions.

Ported files:

- `DefaultPartyWageModelInterface.cs`: the `AidCorps` and `AddPerkFactor` `HasPerk` calls, and the
  `DeepPockets` and `PriceOfLoyalty` `PerkHelper` calls in the v1.5.6 argument order.
- `DefaultItemDiscardModelInterface.cs`: `GivingHands` and `PaidInPromise` drop `out _`.
- `MapTracksCampaignBehaviorInterface.cs`: `Ranger` and `Tracker` drop `out _`.
- `FoodConsumptionBehaviorInterface.cs`: `Breeder` drops `out _`.
- `DisableFindingItemOnMapBehavior.cs`: `BeastWhisperer` drops `out _`.
- `VillageHostileActionInterface.cs`: `InBestLight` drops `out _`.

Proof, run in the port worktree on 2026-10-29 against v1.5.6 (`Version.xml` says
`Singleplayer Value="v1.5.6"`), with `ModsRoot` pointing at a missing folder so nothing was
deployed:

- Release build (`dotnet build Coop.sln -c Release`): incremental `263 Warning(s)`, `0 Error(s)`;
  full rebuild with `--no-incremental` `1848 Warning(s)`, `0 Error(s)` (the log prints each warning
  twice). None are engine binding warnings.
- Engine member audit (TAOM's `tools/check_engine_member_refs.ps1`, no baseline, exit code 0):
  GameInterface.dll `checked 6374 distinct engine refs, 0 unresolved (0 new)`, Coop.Core.dll
  `checked 205 distinct engine refs, 0 unresolved (0 new)`, Missions.dll
  `checked 1485 distinct engine refs, 0 unresolved (0 new)`.
- Unit tests (`dotnet test CoopUnitTests.slnf -c Release --no-build --no-restore`, the CI command,
  exit code 0): 4,122 passed, 14 skipped, 0 failed. VerificationHarness.Tests, which is in
  `Coop.sln` but not in the slnf, run separately: Failed 0, Passed 86, Skipped 0, Total 86.
  4,122 + 86 = 4,208, and 4,208 minus the 19 cases added since `2923a2972` = 4,189, the v1.5.5
  count, so that count most likely included VerificationHarness.Tests. No unit test was removed.
- E2E tests in 16 shards: 2,727 tests, 2,722 passed, 5 skipped, 0 failed. The 15 more than v1.5.5
  are E2E cases added since `2923a2972` (9 Fact plus 6 InlineData, in MapEventLoadCleanerTests and
  BanditPartyComponentTests). 5 are skipped, the same number as on v1.5.5.
- Not covered: no in-game or live co-op session, on one machine.

Patched method bodies (2026-10-29, v1.5.5 to v1.5.6): a snapshot generator written for this check
(kept outside the repo) loads the Release build and lists every engine method Coop patches with
Harmony, resolved the way Coop applies it at run time: patch classes through Harmony's own
`PatchClassProcessor`, the targets of the generated AutoSync patch classes, the registry lifetime
constructors and destroy methods, and the manual `harmony.Patch` calls. Test assemblies are left
out, and so are the 2 targets outside the game (one in GameInterface, one in `0Harmony`). It writes
the list as `patch-targets.md` in the snapshot format of TAOM's `tools/engine_body_diff.py`, with an
empty `gamemodel-bases.md` and `reflection-sites.md`, and the tool diffed the body of every target
between the v1.5.5 and v1.5.6 decompiles (`--old-suffix _v1.5.5`). Its summary counts, verbatim:

- `old=v1.5.5.124170 new=v1.5.6.125053`
- `skipped third-party engine copies (old): Coop=40`
- `skipped third-party engine copies (new): Coop=40`
- `patch-targets.md: rows_in_file=6308 parsed=6308 unparsed=0`
- `gamemodel-bases.md: rows_in_file=0 parsed=0 unparsed=0`
- `reflection-sites.md: rows_in_file=0 parsed=0 unparsed=0`
- `unparsed=0`
- `rows=4567 same=4521 changed=43 unresolved=3`

The 6,308 rows name 4,529 distinct engine members. The tool adds 38 rows of its own (its
`Deserialize(MBObjectManager)` sweep covers 49 types, 11 of them already Coop targets), and none of
those changed. It found 14 members on a base type (`INHERITED`), all the same; for the 8 implicit
constructors among them the type's field initializers were also compared, and they match. The 3
unresolved members were extracted from both decompiles by hand and are the same. So of Coop's 4,529
members, 43 changed and 4,486 did not. Each of the 43 changed targets, and the 3 resolved by hand,
was judged twice against the Coop patches that bind it (46 targets, 92 verdicts). None breaks, so no
code changed and the build and test results above stand.

Changed targets that blanket patches reach (AutoSync `*_DynamicPatches`, `*CollectionPatches` and
`HeroFieldPatches` run transpilers over every method a type declares and rewrite only stores to
synced fields or calls on synced collections):

- `MobileParty.HasPerk`: no break; v1.5.6 removed the `out Hero` and `BattleEnvironment` overloads,
  and no synced field or collection is touched.
- `MobileParty.GetPerkOwnerHero`: no break; new in v1.5.6 (the owner switch moved out of `HasPerk`),
  swept like every declared method, and it writes no synced field.
- `Hero.GetPerkValue`: no break; the patched one-argument overload is unchanged, v1.5.6 removed the
  `BattleEnvironment` overload.
- `CharacterObject.GetPerkValue`: no break; the patched one-argument overload is unchanged, v1.5.6
  removed the four-argument overload.
- `Hero.CanBecomePrisoner`: no break; only the `Debug.FailedAssert` line number changed.
- `MobileParty.ComputePath`: no break; only the `Debug.FailedAssert` line number changed.
- `MapEventSide.ApplySimulationDamageToSelectedTroop`: no break; the `DoctorsOath` checks drop
  `out perkOwnerHero`, and no synced store moved.
- `MapEvent.SimulateSingleTroopHit`: no break; a troop of a party that is not mobile now gets
  `Naval` or `Land` instead of `Any`, and no synced store changed.
- `Hideout.ResetNextPossibleAttackTime`: no break; new in v1.5.6, and the sweep covers it, so its
  store to the synced `_nextPossibleAttackTime` goes through the intercept like every other.
- `BuildingHelper.ChangeCurrentBuildingQueue` (also `BuildingHelperPatches`): no break; only the
  `Debug.FailedAssert` line number changed.
- `Equipment.get_Item` (unresolved, an indexer): same in both versions, both indexers.
- `Equipment.set_Item` (unresolved, an indexer): same in both versions, both indexers.
- `TroopUpgradeTracker..ctor` (unresolved, `LifetimePatches[TroopUpgradeTrackerRegistry]`): an
  implicit constructor; its 3 field initializers are the same.

Changed targets of `AllowItemRostersInGUI` and `AllowTroopRostersInGUI`, a prefix and a finalizer
that only open and close Coop's thread guard around the whole call, so no body change can escape
them:

- `TooltipRefresherCollection.RefreshSettlementTooltip`: no break; compiler local renames, and the
  hint lines moved into `AddTooltipHintProperties`, still inside the call.
- `TooltipRefresherCollection.RefreshArmyTooltip`: no break; the extend key hint block moved into
  `AddTooltipHintProperties`.
- `TooltipRefresherCollection.RefreshClanTooltip`: no break; the same hint block move.
- `TooltipRefresherCollection.RefreshEncounterTooltip`: no break; the same hint block move.
- `TooltipRefresherCollection.RefreshKingdomTooltip`: no break; the same hint block move.
- `TooltipRefresherCollection.RefreshMapEventTooltip`: no break; the same hint block move.
- `TooltipRefresherCollection.RefreshMobilePartyTooltip`: no break; the same hint block move.
- `TooltipRefresherCollection.RefreshSiegeEventTooltip`: no break; the same hint block move.
- `GauntletInformationView.OnShowTooltip`: no break; only the `Debug.FailedAssert` line numbers
  changed.
- `VillagerCampaignBehavior.CalculateConversationPartyBribeAmount`: no break; the `SaltTheEarth`
  check drops `out perkOwnerHero`.

Changed targets of prefixes that skip or gate the original:

- `DefaultPartyWageModel.GetTotalWage` (`DefaultPartyWageModelPatches`): no break; the prefix runs
  Coop's re-implementation instead, which this port moved to the v1.5.6 perk calls (see the ported
  files above).
- `FindingItemOnMapBehavior.DailyTickParty` (`FindingItemOnMapBehaviorPatches`): no break; the
  `BeastWhisperer` check drops `out`, which `DisableFindingItemOnMapBehavior.cs` now follows.
- `FoodConsumptionBehavior.CheckAnimalBreeding` (`FoodConsumptionBehaviorPatches`): no break; the
  `Breeder` check drops `out`, which `FoodConsumptionBehaviorInterface.cs` now follows.
- `VillageHostileActionCampaignBehavior.village_force_volunteers_ended_successfully_on_consequence`
  (`VillageForceActionSuccessConsequencePatch`): no break; a parameterless gate, and the
  `InBestLight` check drops `out`.
- `SiegeEventCampaignBehavior.OnSiegeEngineDestroyed` (`SiegeEventCampaignBehaviorPatches`): no
  break; a server-only gate, and the Pick Them Off The Walls check folds into one condition with the
  same result.
- `SiegeEventCampaignBehavior.OnSiegeEngineBuilt` (`SiegeEventCampaignBehaviorPatches`): no break; a
  server-only gate, and the perk check drops `out`.
- `DefaultClanPoliticsModel.CalculateInfluenceChangeInternal` (`DefaultClanPoliticsModelPatches`):
  no break; the signature is unchanged, and the `ImmortalCharm` call drops `BattleEnvironment.Any`.
- `MobilePartyHelper.CreateNewClanMobileParty` (`MobilePartyHelperPatches`): no break; only the
  `Debug.FailedAssert` line number changed.
- `DefaultRansomValueCalculationModel.PrisonerRansomValue` (`RansomPlayerValuePatch`): no break; the
  signature is unchanged, and the `Manhunter` call drops `BattleEnvironment.Any`.
- `DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty`
  (`DefaultPartySizeLimitModelPatches`): no break; only the `Debug.FailedAssert` line number
  changed.
- `SafePassageBarterable.Apply` (`SiegeEntryFlowPatches`): no break; only the `Debug.FailedAssert`
  line number changed, in the branch the prefix hands to vanilla.
- `MissionGauntletBattleScore.OnMissionScreenTick` (`TournamentScoreboardInputPatches`): no break;
  the scoreboard hotkey also checks `CanOpenScoreboard()`, and the prefix still skips the whole body
  during a co-op tournament.

Changed targets of postfixes, finalizers and transpilers:

- `MapScreen.GetEscapeMenuItems` (`EscapeMenuCoopOptionsPatch`, `EscapeMenuDisableSavePatch`,
  `EscapeMenuInviteFriendsPatch`): no break; `cannotQuickSave` now comes from
  `MBSaveLoad.IsQuickSaveBlockedBySaveLimit()`, and the postfixes find items by text or index.
- `MapScreen.HandleLeftMouseButtonClick` (`DisableMapClickTimeChange`, `PlayerPartyTeleportPatches`,
  `DisableGameMenuPausePatches`): no break; a hotkey read moved into a local and
  `HandleClickTimeChange` gained a second `!MapState.AtMenu` guard, while the transpilers still
  replace every `Campaign.TimeControlMode` setter call.
- `MapScreen.HandleMouse` (`MapSiegeHoverCursorPatch`, `DisableGameMenuPausePatches`): no break; a
  new parley branch before the map click calls `HandleParleyClick`, which no Coop patch targets.
- `MapScreen.CloseCampaignOptions` (`UpdateCampaignOptionsPatch`): no break; only the
  `Debug.FailedAssert` line numbers changed.
- `SPOrderOfBattleVM.GetAgentTooltip` (`CaptainTooltipPatches`): no break; the transpiler's single
  anchor, the one `Hero.FindFirst` call, is still there once.
- `PerkHelper.AddPerkBonusForTown` (`CoopClanGovernorPatches`): no break; the bonus arguments moved
  into locals, and the transpiler rewrites every `Hero.CurrentSettlement` call wherever it sits.
- `DefaultWorkshopModel.GetEffectiveConversionSpeedOfProduction` (`CoopClanGovernorPatches`): no
  break; the `Sweatshops` call drops `BattleEnvironment.Any`, and `Hero.CurrentSettlement` is still
  read once.
- `DefaultPartySpeedCalculatingModel.CalculateFinalSpeed` (`MobilePartyRobustnessPatches`,
  `PartyDebugBuffPatches`): no break; the `UncannyInsight` call takes the v1.5.6 argument order, and
  the finalizer and the `mobileParty` postfix bind as before.
- `DefaultTroopSacrificeModel.GetLostTroopCount` (`TroopSacrificeModelPartyPatch`): no break; the
  `Improviser` call takes the v1.5.6 argument order, and the transpiler swaps the `MainParty` and
  `PlayerCharacter` getters wherever they sit.
- `GainKingdomInfluenceAction.ApplyInternal` (`GainKingdomInfluenceActionPatch`): no break; the
  `Besieged` call takes the v1.5.6 argument order, and the postfix reads only the unchanged
  arguments.
- `ContainerLoadData.FillObject` (`ContainerLoadDataPatches`): no break; two calls gained a null
  check, and the transpiler's single `IDictionary.Add` anchor is still there once.

Open: the TaleWorlds v1.5.6 dedicated server moves to .NET 8 and has not shipped on Steam yet (app
1863440 is still build 25805354, v1.5.5). `BannerlordCoop.DedicatedServer` pins .NET 6
(`scripts/prepare-dotnet-runtime.ps1` accepts only a `6.0.x` runtime), so its host will need a .NET 8
runtime once that server ships.

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
  - Both passes run before the server state that lets lifetime patches publish. Each therefore
    records the campaign's parties first and, in a `finally`, publishes `InstanceDestroyed<MobileParty>`
    for every one that left the campaign during the pass, including a party destroyed elsewhere by a
    cascade (a ruler killed in the battle can destroy a kingdom's clans).
  - Checked on 2026-10-22 on a dedicated server with a copy of the damaged save: it released 7
    events (`Created_97`, 139, 141, 149, 171, 187 and 201), unregistered the 7 parties vanilla
    destroyed while releasing them, then loaded, served and autosaved. Not yet checked: that no
    `set_MapEventSide` exception follows once time runs. With no client connected the pulse line
    reads `timeMode=Stop players=0`, so that check waits for a client. On 2026-10-23 a modded server
    with one client ran 11 in-game days on a new campaign without that exception; time running on a
    repaired damaged save is still unchecked.
  - Open (follow-ups): the player pass finalizes an unlisted live event without first restoring its
    unsaved state, so that finalize can throw; the orphan pass then releases the event only if
    vanilla had already set `WaitingRemoval`. A successful `FinalizeEvent` in the player pass
    publishes neither `MapEventFinalized` nor `InstanceDestroyed<MapEvent>`, because the sync policy
    still allows originals at that point.
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

Seen 2026-10-22 hosting a mod with custom creatures, fixed in that mod: a staged server that
carries a module's `ModuleData` but not its art has no skeleton for the action sets that module
declares. The engine then falls back to an arbitrary loaded skeleton, so when `Game.LoadBasicFiles`
reads the module's `Monsters` XML, each bone lookup fails against that skeleton (the log named
`aserai_castle_gate_inner_l1_plank_skeleton`). Each failure prints
`WARNING: could not find bone index for: <skeleton>::<bone>` with a stack dump and an "Always
Ignore?" message box. On one boot, 150 of them took about 8 minutes before the campaign
finished loading. Not caused by co-op code. The server engine loads `<module>/DsAssetPackages/*.tpac`
for every loaded module, and the game client never reads that folder, so a module can ship the
skeletons its action sets need there: a skeleton-only package with the cooked-package header and
the items copied unchanged works (copying the module's whole client `.tpac` packages crashed native
init). TAOM ships one (`taom_creature_skeletons.tpac`, 6 skeletons); on the same stage and save the
`Monsters` load fell from 479,705 ms to 11 ms with no warnings and the server reached `SERVING`;
without it the warnings came back. The dedicated server repo's Windows staging script copies the
folder because its data-only exclude list does not name it.

Method that worked for this port: compile against v1.5.4; apply every Harmony patch the way
`source/GameInterface.Tests/PatchTest.cs` does, so a missing target fails the run; diff the body of
every patched game method between the v1.4.8 and v1.5.4 decompiles and port the changes into any
patch that replaces the original; then run the tests and a live session with the server and two
clients.

## Verification

- The whole solution builds against v1.5.4, against v1.5.5 with no code change (see "v1.5.5"), and
  against v1.5.6 after the perk call port (see "v1.5.6"). Every Harmony patch class binds, except
  `MobilePartyAIRobustnessPatches`, whose class-level `[HarmonyPatch]` is commented out on
  `development` too.
- The bodies of all 2,336 game methods GameInterface patched at the port (`0d8dd2280`) were diffed
  from v1.4.8 to v1.5.3 and from v1.5.3 to v1.5.4, and the changes reviewed. The patch targets added
  since were written against v1.5.4. From v1.5.5 to v1.5.6 the bodies of all 4,529 engine members
  Coop patches today were diffed: 43 changed, none breaks a patch (see "Patched method bodies" in
  "v1.5.6").
- Unit tests and E2E tests (5 E2E tests skipped, as on `development`) pass in Release; the current
  counts are in the v1.5.6 section above. On Windows, run E2E in 16 shards
  (`sh ../.github/scripts/run-e2e-shard.sh <n> 16` from `source`): with 8, a shard's test filter can
  exceed the command-line length limit.
- v1.5.6: see the v1.5.6 section above for the current counts (v1.5.5's are in the v1.5.5 section).
- Not done yet: a live session with a listen-server host and two clients, and CI, whose build image
  still carries v1.4.8 game assemblies. The dedicated server has booted and served clients on v1.5.5
  (see "Porting the dedicated server").
