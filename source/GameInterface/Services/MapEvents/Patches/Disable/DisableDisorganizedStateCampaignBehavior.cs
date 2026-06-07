using HarmonyLib;
using GameInterface.Policies;

namespace GameInterface.Services.Battles.Patches.Disable;

[HarmonyPatch("DisorganizedStateCampaignBehavior", "RegisterEvents")]
internal class DisableDisorganizedStateCampaignBehavior
{
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
