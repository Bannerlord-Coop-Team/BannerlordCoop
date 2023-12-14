using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ItemRosters.Messages.Commands.Internal;
using HarmonyLib;
using Serilog;
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
            if (pb == null)
                return;

            if (ModInformation.IsServer)
            {
                MessageBroker.Instance.Publish(instance, new PrepareItemRosterUpdated(pb.Id, rosterElement, number));
            }
        }
    }
}
