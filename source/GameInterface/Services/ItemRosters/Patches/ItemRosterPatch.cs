using Common.Messaging;
using GameInterface.Services.ItemRosters.Messages.Events;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Patches
{
    [HarmonyPatch(typeof(ItemRoster))]
    internal class ItemRosterPatch
    {
        [HarmonyPatch(nameof(ItemRoster.AddToCounts), new[] { typeof(EquipmentElement), typeof(int) })]
        [HarmonyPrefix]
        public static void AddToCountsPrefix(ItemRoster __instance, EquipmentElement rosterElement, int number)
        {
            if(ItemRosterLookup.TryGetValue(__instance, out var pb))
            {
                if (ModInformation.IsServer)
                {
                    MessageBroker.Instance.Publish(__instance, new ItemRosterUpdate(
                        pb.Id,
                        rosterElement.Item.StringId,
                        rosterElement.ItemModifier?.StringId,
                        number
                    ));
                }
            }
        }
    }
}
