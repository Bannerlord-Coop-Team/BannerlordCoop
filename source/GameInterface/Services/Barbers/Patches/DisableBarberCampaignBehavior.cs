using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Banners.Patches;

[HarmonyPatch(typeof(BarberCampaignBehavior))]
internal class DisableBarberCampaignBehavior
{
    [HarmonyPatch(nameof(BarberCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
