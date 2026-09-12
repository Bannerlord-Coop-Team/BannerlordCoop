using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents;

internal class FieldBattleMissionInitializer : IBattleMissionInitializer
{
    public int Priority => 0;

    public bool CanHandle(MapEvent mapEvent) => true;

    public MissionInitializerRecord Create(MapEvent battle, int randomTerrainSeed, AtmosphereInfo atmosphereOnCampaign)
    {
        bool isNavalEncounter = battle.IsNavalMapEvent;
        CampaignVec2 position = battle.Position;

        IMapScene mapSceneWrapper = Campaign.Current.MapSceneWrapper;
        MapPatchData mapPatchAtPosition = mapSceneWrapper.GetMapPatchAtPosition(position);

        string battleScene = GetBattleSceneForMapPatch(mapPatchAtPosition, position, isNavalEncounter);
        MissionInitializerRecord record = new MissionInitializerRecord(battleScene);
        TerrainType faceTerrainType2 = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(position.Face);
        record.TerrainType = (int)faceTerrainType2;
        record.DamageToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
        record.DamageFromPlayerToFriendsMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
        record.NeedsRandomTerrain = false;
        record.PlayingInCampaignMode = true;

        // Seed chosen server-side and carried in NetworkStartAttackMission so every
        // client uses the same terrain seed for this battle.
        record.RandomTerrainSeed = randomTerrainSeed;
        record.AtmosphereOnCampaign = atmosphereOnCampaign;
        record.SceneHasMapPatch = true;
        record.DecalAtlasGroup = 2;
        record.PatchCoordinates = mapPatchAtPosition.normalizedCoordinates;
        position = battle.AttackerSide.LeaderParty.Position;
        Vec2 v2 = position.ToVec2();
        position = battle.DefenderSide.LeaderParty.Position;
        record.PatchEncounterDir = (v2 - position.ToVec2()).Normalized();

        return record;
    }

    private static string GetBattleSceneForMapPatch(MapPatchData mapPatch, CampaignVec2 position, bool isNavalEncounter)
    {
        var battleScenes = GameSceneDataManager.Instance.SingleplayerBattleScenes
            .Where(scene => scene.MapIndices.Contains(mapPatch.sceneIndex) && scene.IsNaval == isNavalEncounter)
            .ToMBList();

        if (battleScenes.Count == 1)
        {
            return battleScenes[0].SceneID;
        }

        if (battleScenes.IsEmpty())
        {
            Campaign.Current.MapSceneWrapper.GetEnvironmentTerrainTypesCount(in position, out TerrainType terrainType);
            battleScenes = GameSceneDataManager.Instance.SingleplayerBattleScenes
                .Where(scene => scene.Terrain == terrainType && scene.IsNaval == isNavalEncounter)
                .ToMBList();

            if (battleScenes.IsEmpty())
            {
                battleScenes = GameSceneDataManager.Instance.SingleplayerBattleScenes
                    .Where(scene => scene.IsNaval == isNavalEncounter)
                    .ToMBList();
            }

            if (battleScenes.IsEmpty())
            {
                battleScenes = GameSceneDataManager.Instance.SingleplayerBattleScenes.ToMBList();
            }
        }
        else if (battleScenes.Count > 1 && isNavalEncounter)
        {
            Campaign.Current.MapSceneWrapper.GetEnvironmentTerrainTypesCount(in position, out TerrainType terrainType);
            var terrainBattleScenes = battleScenes.Where(scene => scene.Terrain == terrainType).ToMBList();
            if (!terrainBattleScenes.IsEmpty())
            {
                battleScenes = terrainBattleScenes;
            }
        }

        return battleScenes.GetRandomElement().SceneID;
    }
}
