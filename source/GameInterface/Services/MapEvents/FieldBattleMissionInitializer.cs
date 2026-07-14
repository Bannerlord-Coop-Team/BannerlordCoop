using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents;

internal class FieldBattleMissionInitializer : IBattleMissionInitializer
{
    public int Priority => 0;

    public bool CanHandle(MapEvent mapEvent) => true;

    public MissionInitializerRecord Create(MapEvent battle, int randomTerrainSeed, AtmosphereInfo atmosphereOnCampaign, BattleMissionStartContext context = null)
    {
        bool isNavalEncounter = PlayerEncounter.IsNavalEncounter();
        CampaignVec2 position = MobileParty.MainParty.Position;

        IMapScene mapSceneWrapper = Campaign.Current.MapSceneWrapper;
        MapPatchData mapPatchAtPosition = mapSceneWrapper.GetMapPatchAtPosition(position);

        string battleScene = Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(mapPatchAtPosition, isNavalEncounter);
        MissionInitializerRecord record = new MissionInitializerRecord(battleScene);
        TerrainType faceTerrainType2 = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(MobileParty.MainParty.CurrentNavigationFace);
        record.TerrainType = (int)faceTerrainType2;
        RecordDefaults.ApplyDamageMultipliers(record);
        record.NeedsRandomTerrain = false;
        RecordDefaults.ApplyCampaignMode(record, atmosphereOnCampaign);

        // Seed chosen server-side and carried in NetworkStartAttackMission so every
        // client uses the same terrain seed for this battle.
        RecordDefaults.ApplyTerrainSeed(record, randomTerrainSeed);
        record.SceneHasMapPatch = true;
        record.DecalAtlasGroup = 2;
        record.PatchCoordinates = mapPatchAtPosition.normalizedCoordinates;
        position = battle.AttackerSide.LeaderParty.Position;
        Vec2 v2 = position.ToVec2();
        position = battle.DefenderSide.LeaderParty.Position;
        record.PatchEncounterDir = (v2 - position.ToVec2()).Normalized();

        return record;
    }
}
