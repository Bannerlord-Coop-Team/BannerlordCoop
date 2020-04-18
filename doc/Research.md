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