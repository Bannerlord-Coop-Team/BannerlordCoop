using GameInterface.AutoSync;
using GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;

internal class HeadmanNeedsGrainSync : IAutoSync
{
    public HeadmanNeedsGrainSync(AutoSyncRegistry registry)
    {
        registry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(HeadmanNeedsGrainProsperityPatches),
            nameof(HeadmanNeedsGrainProsperityPatches.SetProsperity)));
    }
}
