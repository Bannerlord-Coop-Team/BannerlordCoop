using HarmonyLib;
using SandBox.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Players.Patches;

[HarmonyPatch(typeof(RetirementCampaignBehavior))]
internal class DisableRetirementCampaignBehavior
{
    [HarmonyPatch(nameof(RetirementCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
