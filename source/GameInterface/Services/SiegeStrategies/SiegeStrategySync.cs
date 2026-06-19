using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.SiegeStrategies
{
    internal class SiegeStrategySync : IAutoSync
    {
        public SiegeStrategySync(AutoSyncRegistry autoSyncBuilder)
        {
            //Properties
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeStrategy), nameof(SiegeStrategy.Name)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeStrategy), nameof(SiegeStrategy.Description)));
        }
    }
}
