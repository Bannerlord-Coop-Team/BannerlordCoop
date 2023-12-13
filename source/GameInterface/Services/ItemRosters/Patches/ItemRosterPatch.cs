using Common.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Patches
{
    [HarmonyPatch(typeof(ItemRoster))]
    internal class ItemRosterPatch
    {
        private static readonly ItemRosterPatch instance = new();

        [HarmonyPatch(nameof(ItemRoster.AddToCounts), new[] { typeof(EquipmentElement), typeof(int) })]
        [HarmonyPrefix]
        public static void AddToCountsPrefix(ItemRoster __instance, EquipmentElement rosterElement, int number)
        {
            var pb = ItemRosterMapper.Instance.Get(__instance);
        }
    }
}
