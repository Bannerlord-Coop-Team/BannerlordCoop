using GameInterface.AutoSync;
using HarmonyLib;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines
{
    internal class SiegeEnginesContainerSync: IAutoSync
    {
        public SiegeEnginesContainerSync(AutoSyncRegistry autoSyncBuilder)
        {
            // Fields
            // The deployed/reserved collections are hand-synced through SiegeEnginesContainerPatches
            // because their writes are per-index array stores AutoSync cannot intercept.
            autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEnginesContainer), nameof(SiegeEnginesContainer.SiegePreparations)));
        }
    }
}
