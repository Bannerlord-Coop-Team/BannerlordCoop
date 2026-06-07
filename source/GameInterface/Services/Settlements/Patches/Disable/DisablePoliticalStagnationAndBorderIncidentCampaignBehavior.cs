using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Settlements.Patches.Disable;


[HarmonyPatch(typeof(PoliticalStagnationAndBorderIncidentCampaignBehavior))]
internal class DisablePoliticalStagnationAndBorderIncidentCampaignBehavior
{
    [HarmonyPatch(nameof(PoliticalStagnationAndBorderIncidentCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
