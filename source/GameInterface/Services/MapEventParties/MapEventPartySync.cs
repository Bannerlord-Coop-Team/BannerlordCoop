using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties
{
    internal class MapEventPartySync : IDynamicSync
    {
        public MapEventPartySync(DynamicSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.GainedInfluence)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.GainedRenown)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.GoldLost)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.MoraleChange)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.Party)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.PlunderedGold)));

            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventParty), nameof(MapEventParty._contributionToBattle)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventParty), nameof(MapEventParty._healthyManCountAtStart)));
        }
    }
}
