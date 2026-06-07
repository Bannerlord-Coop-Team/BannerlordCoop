using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Buildings.Patches;

[HarmonyPatch(typeof(BuildingsCampaignBehavior))]
internal class DisableBuildingsCampaignBehavior
{
    [HarmonyPatch(nameof(BuildingsCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
