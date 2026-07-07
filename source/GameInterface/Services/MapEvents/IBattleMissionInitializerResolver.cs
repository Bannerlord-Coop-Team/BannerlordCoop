using GameInterface.Services;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents;

internal interface IBattleMissionInitializerResolver : IGameAbstraction
{
    MissionInitializerRecord Create(MapEvent mapEvent, int randomTerrainSeed, AtmosphereInfo atmosphereOnCampaign);
}
