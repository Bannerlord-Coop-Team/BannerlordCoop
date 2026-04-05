using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks
{
    internal class StanceLinkSync : IAutoSync
    {
        public StanceLinkSync(IAutoSyncBuilder autoSyncBuilder)
        {
            //Properties
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.Faction1)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.Faction2)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.StanceType)));

            //Fields
            autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink.BehaviorPriority)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._troopCasualties1)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._troopCasualties2)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._dailyTributeFrom1To2)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._peaceDeclarationDate)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._stanceType)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._successfulRaids1)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._successfulRaids2)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._successfulSieges1)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._successfulSieges2)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._totalTributePaidFrom1To2)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._warStartDate)));
        }
    }
}
