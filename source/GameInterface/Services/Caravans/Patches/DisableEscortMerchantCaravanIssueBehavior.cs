using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Caravans.Patches;

[HarmonyPatch(typeof(EscortMerchantCaravanIssueBehavior))]
internal class DisableEscortMerchantCaravanIssueBehavior
{
    [HarmonyPatch(nameof(EscortMerchantCaravanIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
