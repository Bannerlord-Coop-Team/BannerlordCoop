using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesConstructionProgress
{
    internal class SiegeEngineConstructionProgressSync : IAutoSync
    {
        public SiegeEngineConstructionProgressSync(IAutoSyncBuilder autoSyncBuilder)
        {
            // Props
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeEngineConstructionProgress), nameof(SiegeEngineConstructionProgress.Progress)));
        }
    }
}
