using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior))]
internal class DisableGangLeaderNeedsToOffloadStolenGoodsIssueBehavior
{
    [HarmonyPatch(nameof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
