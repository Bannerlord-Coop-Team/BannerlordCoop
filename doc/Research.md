## Input: Party movement on campaign map
Main entry point: `SandBox.View.Map.MapScreen HandleLeftMouseButtonClick`
`MobileParty.Position2D` - Teleports party directly to position
`MapState.ProcessTravel` - Sends party to position. Already contains some defunct multiplayer logic.

## Sync
First managed code tick is received in `TaleWorlds.DotNet.Managed.ApplicationTick` which delegates to `IManagedComponent.OnApplicationTick` and eventuelly ends up in `Module.CurrentModule.OnApplicationTick` (base game module).

Campaign ticks are generated in `CampaignEvents.SignalPeriodicEvents` with `Campaign.Current.CampaignStartTime.ElapsedHoursUntilNow` (=> delta to `Campaign.Current.MapTimeTracker`) as a base timer. Updated in `Campaign.TickMapTime`. Base Tick is tied to `MapState.OnMapModeTick` which is invoked from a `MapState.OnTick` (inherited from `GameState`).

## Saving & Loading
`MBSaveLoad`
`SaveManager`
`InMemDriver`

## Randomness
TaleWorld.Core.MBRandom

## Time control
Campaign.SetTimeSpeed
Campaign.TimeControlMode
Campaign.SetTimeControlModeLock

## MBSubModuleBase initialization
### Order
```
                 OnSubModuleLoad
LoadGame                +              new campaign
+---------------------+ | +-----------------------+
|                       v                         |
|            +----+OnGameStart+-----+             |
|            |                      |             |
|            |                      |             |
|            v                      v             |
|      OnGameLoaded          OnCampaignStart      |
|            +                      +             |
|            +----------+-----------+             |
|                       v                         |
|          OnGameInitializationFinished           |
|                       +                         |
|                       |                         |
|                       v                         |
|                   DoLoading+------+             |
|                                   |             |
|                       +           |             |
|                       |           v             |
|                       |     OnGameCreated       |
|                       |                         |
+-----------------------+-------------------------+
```
#### Parameters
#### Game
In singleplayer, the first parameter seems to always be `game.GameType is TaleWorlds.CampaignSystem.Campaign`. 
#### initializerObject
`OnGameStart`, `OnGameLoaded`, `OnCampaignStart` and `OngameCreated` receive a second `object` parameter. In singleplayer this is a `TaleWorlds.CampaignSystem.CampaignGameStarter`, otherwise a `TaleWorlds.MountAndBlase.BaseGameStarter`. Especially in campaign, this seems to be to be a significant entry point to adding content to the game.
### Oddities
- Modules cannot be added after `OnGameStart`. They can still be accessed afterwards, but won't have any effect.
- `CampaignGameStarter` is invalidated and mostly moved to the `Campaign` right before `OnGameInitializationFinished`. `Campaign` does not store any of the behaviours befor that, do not access!
## Persistence
### Behaviours
#### IDataStore
Serialize & deserialize `CampaignBehaviorBase` data. Implemented in `TaleWorld.CampaignSystem.CampaignBehaviorDataStore.BehaviorSaveData`.