using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Settlements.Patches;


[HarmonyPatch(typeof(RebellionsCampaignBehavior))]
internal class RebellionsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(RebellionsCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
