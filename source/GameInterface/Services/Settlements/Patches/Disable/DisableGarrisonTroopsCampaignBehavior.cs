using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Settlements.Patches.Disable;


[HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior))]
internal class DisableGarrisonTroopsCampaignBehavior
{
    [HarmonyPatch(nameof(GarrisonTroopsCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
