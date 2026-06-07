using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(RomanceCampaignBehavior))]
internal class DisableRomanceCampaignBehavior
{
    [HarmonyPatch(nameof(RomanceCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
