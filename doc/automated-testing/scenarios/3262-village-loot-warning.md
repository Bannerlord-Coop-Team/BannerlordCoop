# Issue 3262: village raid loot warning

This is a source-bound Debug scenario. It proves the production hostile-action, battle-simulation, loot, and warning-dismissal paths without foregrounding a game window or injecting input. UI wiring is excluded and unverified; it is not a manual-testing gate.

Run only on the enrolled Local lane with one dedicated authoritative server and one connected client on a fresh copy of the disposable baseline. Before the run, record the source head/tree, baseline save SHA-256, Debug build identity, game version, and deployed assembly hashes. The state command reports `buildVersion`, `sourceCommit`, and `assemblyMvid` on both sides; they must match the recorded source receipt.

## Baseline

The client party starts on land, holding outside settlements, armies, sieges, and map events. It has one healthy leader and no companions or hero prisoners. Its faction differs from Polisia's. Polisia is normal, not under siege or in a map event, and has no visiting mobile parties. Its existing native mobile militia must be registered, active, inside the village, and outside a battle.

The fixed target is **Polisia**, settlement `village_ES1_2`, village component `village_comp_ES1_2`, bound to **Danustica**, settlement `town_ES1`, in the Southern Empire. The fixture validates the actual save state before changing it.

The fixture changes campaign state permanently and has no restore command. Reload the whole copied baseline after a failure or completed case. Do not save fixture state over the baseline.

## Handler-driven scenario

On the server, run:

```text
coop.debug.players.list
coop.debug.mapevent.allow_raid_ai_intervention on
coop.debug.mapevent.raid_loot_warning_prepare only-connected disposable-baseline
coop.debug.mapevent.raid_loot_warning_state only-connected
```

`only-connected` requires exactly one registered client. Otherwise, use the exact `ControllerId` returned by `coop.debug.players.list` for every command below.

The preparation state must show 61 player members with zero wounded, an empty settlement roster, eight unwounded native mobile militia members, `militiaOwnsParty=true`, `villageState=Normal`, `villageHitPoints=1`, `atWar=true`, and `allowRaidAiIntervention=true`. Preserve its `fixtureToken`, controller id, party id, troop ids, and source identity.

On the client, read the replicated setup, then request the real settlement entry:

```text
coop.debug.mapevent.raid_loot_warning_state only-connected
coop.debug.mapevent.raid_loot_warning_start only-connected
```

`raid_loot_warning_start` calls only the normal settlement-encounter action. The client patch sends its normal entry request, and the normal server approval replies back onto the client game thread. It does not write a map event or client screen state directly.

Do not invoke `raid_loot_warning_start` again and do not request the raid yet. Poll the existing read-only state on the client no more than once per second for up to 30 seconds. Before advancing, require `clientActionPhase=settlement-entry-requested`, `mapEventId=null`, `partyCurrentSettlementId=village_ES1_2`, `encounterSettlementId=village_ES1_2`, and a non-null `encounterState`. A second entry command is rejected without sending another entry request. A premature raid command is rejected and leaves this pending state unchanged.

After that approved state is observed, request the real hostile action:

```text
coop.debug.mapevent.raid_loot_warning_request_raid only-connected
```

`raid_loot_warning_request_raid` only accepts the observed normal settlement encounter, then calls `IVillageHostileActionInterface.RequestHostileAction(Raid)`. That follows the normal client request, server validation/application/approval, network reply, and client presentation path without blocking the game thread or writing a map event directly.

Poll the read-only state on both sides, no more than once per second. The client `mapEventId` must equal the server `capturedMapEventId`; the server `fixturePhase` must be `captured`; the event must be a `RaidEventComponent` for `village_ES1_2`; and the client must report `clientActionPhase=raid-requested`. Any other event, participant set, or missing capture ends the attempt.

Use the existing Debug command that invokes the production encounter-menu consequence for **Send troops**:

```text
coop.debug.map_event.choose_battle_mode simulation
```

This calls `EncounterGameMenuBehavior.game_menu_encounter_order_attack_on_consequence`, including the normal client/server simulation gate. Wait for the read-only client state to report `simulationResultVisible=true`, then complete the real result action:

```text
coop.debug.mapevent.raid_loot_warning_complete_simulation only-connected
```

The command only runs `GauntletMapBattleSimulationView`'s real `ExecuteQuitAction` after the authoritative simulation is finished. Wait for `partyLootActive=true`, then run:

```text
coop.debug.mapevent.raid_loot_warning_complete_party only-connected
```

That command only runs the real loot Party screen completion through `PartyScreenHelper.CloseScreen(isForced: false)`.

Wait for the client to report `inventoryActive=true`, `topScreen=GauntletInventoryScreen`, `otherItemCount>0`, and `otherGrainCount=1`. On the server, require `fixturePhase=seeded`, `seedApplied=true`, `rawLootGrainCount=1`, and the same captured event id. The fixture's one-grain normalization is server-side setup for this exact winning raid only; the inventory result itself is replicated through the normal result path.

Drive the normal inventory completion and the exact affirmative action captured from its normal inquiry:

```text
coop.debug.mapevent.raid_loot_warning_show only-connected
coop.debug.mapevent.raid_loot_warning_state only-connected
coop.debug.mapevent.raid_loot_warning_accept only-connected
```

`raid_loot_warning_show` calls the active `GauntletInventoryScreen.ExecuteConfirm`, captures only the resulting `str_leaving_loot_behind` inquiry, and requires its native affirmative callback. `raid_loot_warning_accept` hides that same inquiry and invokes its captured affirmative callback. It does not synthesize completion state or invoke a replacement handler.

Within 30 seconds after acceptance, require on the client: `inventoryActive=false`, `partyLootActive=false`, `inquiryActive=false`, `clientPendingLootWarning=false`, and `clientActionPhase=loot-warning-accepted`. Within a further 30 seconds, require the client to return to Polisia: `topScreen=MapScreen`, `menuId=village`, `encounterState=Begin`, and `partyCurrentSettlementId=encounterSettlementId=village_ES1_2`. The raid defender battle ends without looting the village, so this matching settlement encounter is expected. Require `simulationActive` to be null or false and `simulationResultVisible=false`; all closed-loot and warning checks above must still hold. On both sides, require `partyCurrentSettlementId=village_ES1_2`, `villageState=Normal`, `mapEventId=null`, `partyHasMapEvent=false`, and `villageHasMapEvent=false`. On the server, require the same captured event id, `capturedMapEventFinalized=true`, `capturedMapEventRegistered=false`, and `capturedMapEventStillAttached=false`. Observe the client state for five more seconds; the matching village encounter must remain, with no battle, simulation result, warning or loot screen reappearing.

## Evidence and failures

Preserve every server/client command result, the fixture token, source receipt, timestamps, and copied logs before a relaunch. A failed capture, simulation, seed, client replication, warning callback, or cleanup is a failed attempt, not a pass. Preserve it, clean up the owned Local run once, and reload the copied baseline only after recording the result.

Do not infer a pass from unit tests, an empty inventory, a warning that was not produced by `ExecuteConfirm`, or a synthetic map-event/screen write. This scenario never activates a window, sends native input, moves the cursor, or requires an operator to click a UI element.
