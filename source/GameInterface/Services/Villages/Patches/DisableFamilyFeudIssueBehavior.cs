using HarmonyLib;
using SandBox.CampaignBehaviors;
using SandBox.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(FamilyFeudIssueBehavior))]
internal class DisableFamilyFeudIssueBehavior
{
    [HarmonyPatch(nameof(FamilyFeudIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
