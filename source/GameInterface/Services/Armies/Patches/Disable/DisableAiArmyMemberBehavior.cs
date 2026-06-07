using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Armies.Patches.Disable;

[HarmonyPatch(typeof(AiArmyMemberBehavior))]
internal class DisableAiArmyMemberBehavior
{
    [HarmonyPatch(nameof(AiArmyMemberBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
