using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Alleys.Patches;

[HarmonyPatch(typeof(AlleyCampaignBehavior))]
internal class DisableAlleyCampaignBehavior
{
    [HarmonyPatch(nameof(AlleyCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
