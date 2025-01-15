using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventSides
{
    internal class MapEventSideSync : IAutoSync
    {
        public MapEventSideSync(IAutoSyncBuilder autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventSide), nameof(MapEventSide.CasualtyStrength)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventSide), nameof(MapEventSide.LeaderParty)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventSide), nameof(MapEventSide.MissionSide)));




        }
    }
}
