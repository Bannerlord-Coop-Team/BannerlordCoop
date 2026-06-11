using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

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
