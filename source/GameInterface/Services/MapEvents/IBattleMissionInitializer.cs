using GameInterface.Services;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents;

internal interface IBattleMissionInitializer : IGameAbstraction
{
    int Priority { get; }

    bool CanHandle(MapEvent mapEvent);

    MissionInitializerRecord Create(MapEvent mapEvent, int randomTerrainSeed, AtmosphereInfo atmosphereOnCampaign);
}
