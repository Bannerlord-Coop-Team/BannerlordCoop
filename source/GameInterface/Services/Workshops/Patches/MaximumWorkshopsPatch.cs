using GameInterface.Policies;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops.Patches;

[HarmonyPatch(typeof(DefaultWorkshopModel))]
internal class MaximumWorkshopsPatch
{
    /// <summary>
    /// This readonly property is used to manage the size of WorkshopsCampaignBehavior._workshopData and WorkshopsCampaignBehavior._warehouseRosterPerSettlement
    /// The problem with the vanilla implementation is it only accounts for one player and multiple players will exceed the vanilla limit if they max out clan tiers and owned workshops
    /// This patch sets the maximum to be the number of workshops across the map to keep track of workshop data belonging to many players
    /// </summary>
    [HarmonyPatch(nameof(DefaultWorkshopModel.MaximumWorkshopsPlayerCanHave), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool MaximumWorkshopsPlayerCanHavePrefix(ref int __result)
    {
        // If called on allowed thread return original value
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        int numberOfWorkshops = 0;
        foreach (Town town in Town.AllTowns)
        {
            foreach (Workshop workshop in town.Workshops)
            {
                numberOfWorkshops++;
            }
        }

        __result = numberOfWorkshops;

        return false;
    }
}
