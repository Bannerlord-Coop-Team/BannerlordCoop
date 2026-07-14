using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Builds the walls-assault siege <see cref="MissionInitializerRecord"/>. The scene is the fixed settlement
/// scene keyed by wall level (no terrain seed on the siege path); the wall level is taken from the siege
/// snapshot carried in the start message, never re-read from live settlement state, so a joiner entering
/// mid-assault loads the same scene as the first entrant even though the campaign-side siege container keeps
/// syncing on their machine. Selected ahead of the raid and field initializers for an
/// <see cref="MapEvent.IsSiegeAssault"/> event.
/// </summary>
internal class SiegeAssaultMissionInitializer : IBattleMissionInitializer
{
    // Above VillageRaidMissionInitializer (100) and FieldBattleMissionInitializer (0), so a siege assault
    // resolves to this initializer before the more general handlers get a look.
    public int Priority => 200;

    public bool CanHandle(MapEvent mapEvent) => mapEvent.IsSiegeAssault;

    public MissionInitializerRecord Create(MapEvent battle, int randomTerrainSeed, AtmosphereInfo atmosphereOnCampaign, BattleMissionStartContext context = null)
    {
        // The wall level MUST come from the server siege snapshot, not live settlement state: the live
        // container keeps syncing after the assault opens, and a late joiner reading it would load a
        // different wall scene than the first entrant. Fail loudly rather than silently fall back.
        if (context?.WallLevel == null)
        {
            throw new InvalidOperationException(
                "SiegeAssaultMissionInitializer requires a BattleMissionStartContext carrying the snapshot WallLevel; " +
                "refusing to build the siege scene from live settlement state.");
        }

        int wallLevel = context.WallLevel.Value;

        var settlement = battle.MapEventSettlement;
        if (settlement == null)
        {
            throw new InvalidOperationException(
                "SiegeAssaultMissionInitializer could not resolve the map event settlement; cannot build the siege scene.");
        }

        // The scene is the fixed settlement scene keyed by wall level. Mirrors vanilla
        // CreateSandBoxMissionInitializerRecord; atmosphere is client-local, same tolerance as the field path.
        string sceneName = settlement.LocationComplex.GetLocationWithId("center").GetSceneName(wallLevel);
        var record = new MissionInitializerRecord(sceneName)
        {
            SceneLevels = Campaign.Current.Models.LocationModel.GetUpgradeLevelTag(wallLevel) + " siege",
            TerrainType = (int)Campaign.Current.MapSceneWrapper.GetFaceTerrainType(MobileParty.MainParty.CurrentNavigationFace),
            DecalAtlasGroup = 3,
        };

        RecordDefaults.ApplyDamageMultipliers(record);
        RecordDefaults.ApplyCampaignMode(record, Campaign.Current.Models.MapWeatherModel.GetAtmosphereModel(MobileParty.MainParty.Position));

        return record;
    }
}
