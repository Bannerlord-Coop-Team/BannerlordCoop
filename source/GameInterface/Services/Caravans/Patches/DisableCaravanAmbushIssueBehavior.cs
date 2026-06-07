using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Caravans.Patches;

[HarmonyPatch(typeof(CaravanAmbushIssueBehavior))]
internal class DisableCaravanAmbushIssueBehavior
{
    [HarmonyPatch(nameof(CaravanAmbushIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
