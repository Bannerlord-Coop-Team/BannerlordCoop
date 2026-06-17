using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeStrategies
{
    internal class SiegeStrategySync : IDynamicSync
    {
        public SiegeStrategySync(DynamicSyncRegistry autoSyncBuilder)
        {
            //Properties
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeStrategy), nameof(SiegeStrategy.Name)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeStrategy), nameof(SiegeStrategy.Description)));
        }
    }
}
