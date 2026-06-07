using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(TransferPrisonerBarterBehavior))]
internal class DisableTransferPrisonerBarterBehavior
{
    [HarmonyPatch(nameof(TransferPrisonerBarterBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
