using GameInterface.Services;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents;

internal interface IBattleMissionInitializer : IGameAbstraction
{
    int Priority { get; }

    bool CanHandle(MapEvent mapEvent);

    /// <summary>
    /// Builds the mission-open record for this battle type. <paramref name="context"/> carries optional
    /// server-snapshotted inputs (e.g. the siege wall level) for types that must not read live campaign
    /// state; initializers that need no such input ignore it.
    /// </summary>
    MissionInitializerRecord Create(MapEvent mapEvent, int randomTerrainSeed, AtmosphereInfo atmosphereOnCampaign, BattleMissionStartContext context = null);
}
