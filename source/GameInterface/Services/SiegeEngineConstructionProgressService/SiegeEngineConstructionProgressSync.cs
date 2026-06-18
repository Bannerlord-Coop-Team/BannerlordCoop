using GameInterface.AutoSync;
using GameInterface.AutoSync;
using HarmonyLib;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesConstructionProgress
{
    internal class SiegeEngineConstructionProgressSync : IAutoSync
    {
        public SiegeEngineConstructionProgressSync(AutoSyncRegistry autoSyncBuilder)
        {
            // Props
            //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeEngineConstructionProgress), nameof(SiegeEngineConstructionProgress.Progress)));
            //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeEngineConstructionProgress), nameof(SiegeEngineConstructionProgress.Hitpoints)));
            //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeEngineConstructionProgress), nameof(SiegeEngineConstructionProgress.MaxHitPoints)));

            // Fields
            //autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEngineConstructionProgress), nameof(SiegeEngineConstructionProgress.SiegeEngine)));
        }
    }
}