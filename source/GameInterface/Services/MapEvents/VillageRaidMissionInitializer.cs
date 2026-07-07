using Common.Logging;
using SandBox;
using Serilog;
using System;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;

namespace GameInterface.Services.MapEvents;

internal class VillageRaidMissionInitializer : IBattleMissionInitializer
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(VillageRaidMissionInitializer));

    private const string VillageCenterLocationId = "village_center";
    private const string LandRaidSceneLevel = "land_raid";

    public int Priority => 100;

    public bool CanHandle(MapEvent battle)
    {
        if (!battle.IsRaidHostileAction())
            return false;

        return TryGetVillageScene(battle, out _, out _, logWarnings: true);
    }

    public MissionInitializerRecord Create(MapEvent battle, int randomTerrainSeed, AtmosphereInfo atmosphereOnCampaign)
    {
        if (!TryGetVillageScene(battle, out Settlement settlement, out string villageScene, logWarnings: false))
            throw new InvalidOperationException("Village raid mission initializer could not resolve a village raid scene");

        MissionInitializerRecord record = SandBoxMissions.CreateSandBoxMissionInitializerRecord(
            villageScene,
            LandRaidSceneLevel,
            false,
            DecalAtlasGroup.Battle);
        record.RandomTerrainSeed = randomTerrainSeed;
        record.PlayingInCampaignMode = true;
        record.AtmosphereOnCampaign = atmosphereOnCampaign;

        Logger.Information("[BattleSync] Using village raid battle scene {Scene} for settlement {Settlement}",
            villageScene,
            settlement.Name);
        return record;
    }

    private static bool TryGetVillageScene(MapEvent battle, out Settlement settlement, out string villageScene, bool logWarnings)
    {
        settlement = battle.MapEventSettlement;
        villageScene = null;

        if (settlement?.IsVillage != true)
            return false;

        if (settlement.LocationComplex == null)
        {
            if (logWarnings)
                Logger.Warning("[BattleSync] Raid map event settlement {Settlement} has no location complex; falling back to field battle scene", settlement.Name);
            return false;
        }

        int upgradeLevel = settlement.IsTown ? settlement.Town.GetWallLevel() : 1;
        villageScene = settlement.LocationComplex.GetScene(VillageCenterLocationId, upgradeLevel);
        if (string.IsNullOrEmpty(villageScene))
        {
            if (logWarnings)
                Logger.Warning("[BattleSync] Raid map event settlement {Settlement} has no {LocationId} scene for upgrade level {UpgradeLevel}; falling back to field battle scene",
                    settlement.Name,
                    VillageCenterLocationId,
                    upgradeLevel);
            return false;
        }

        return true;
    }
}
