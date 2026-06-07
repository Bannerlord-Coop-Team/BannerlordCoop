using HarmonyLib;
using SandBox.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Hideouts.Patches.Disable;

[HarmonyPatch(typeof(HideoutConversationsCampaignBehavior))]
internal class DisableHideoutConversationsCampaignBehavior
{
    [HarmonyPatch(nameof(HideoutConversationsCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
