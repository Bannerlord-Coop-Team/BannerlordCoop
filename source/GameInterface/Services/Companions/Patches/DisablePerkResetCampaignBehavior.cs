using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Cheats.Patches
{
    [HarmonyPatch(typeof(PerkResetCampaignBehavior))]
    internal class DisablePerkResetCampaignBehavior
    {
        [HarmonyPatch(nameof(PerkResetCampaignBehavior.RegisterEvents))]
        static bool Prefix()
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }
    }
}