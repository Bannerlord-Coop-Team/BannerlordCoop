using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ItemRosters.Messages.Events;
using HarmonyLib;
using Serilog;
using Serilog.Core;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Patches
{
    [HarmonyPatch(typeof(ItemRoster))]
    internal class ItemRosterPatch
    {
        private static readonly ILogger logger = LogManager.GetLogger<ItemRosterPatch>();

        [HarmonyPatch(nameof(ItemRoster.AddToCounts), new[] { typeof(EquipmentElement), typeof(int) })]
        [HarmonyPrefix]
        public static void AddToCountsPrefix(ItemRoster __instance, EquipmentElement rosterElement, int number)
        {
            if (ModInformation.IsServer)
            {
                if (ItemRosterLookup.TryGetValue(__instance, out var pb))
                {
                    MessageBroker.Instance.Publish(__instance, new ItemRosterUpdate(
                        pb.Id,
                        rosterElement.Item.StringId,
                        rosterElement.ItemModifier?.StringId,
                        number
                    ));
                } else
                {
                    logger.Error("Unmanaged item roster updated");
                }
            }
        }
    }
}
