using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SettlementComponents
{
    internal class SettlementComponentSync : IAutoSync
    {
        public SettlementComponentSync(IAutoSyncBuilder autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SettlementComponent), nameof(SettlementComponent.Gold)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SettlementComponent), nameof(SettlementComponent.IsOwnerUnassigned)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SettlementComponent), nameof(SettlementComponent.Owner)));
        }
    }
}
