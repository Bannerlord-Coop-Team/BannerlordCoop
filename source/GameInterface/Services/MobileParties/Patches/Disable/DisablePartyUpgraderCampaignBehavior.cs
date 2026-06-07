using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PartyUpgraderCampaignBehavior))]
internal class DisablePartyUpgraderCampaignBehavior
{
    [HarmonyPatch(nameof(PartyUpgraderCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
