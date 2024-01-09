using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.GameDebug.Patches;
using GameInterface.Services.ItemRosters.Messages.Events;
using HarmonyLib;
using Serilog;
using Serilog.Core;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Patches
{
    [HarmonyPatch(typeof(ItemRoster))]
    internal class ItemRosterPatch
    {
        private static readonly ILogger logger = LogManager.GetLogger<ItemRosterPatch>();

        public static AllowedInstance<ItemRoster> AllowedInstance = new();

        [HarmonyPatch(nameof(ItemRoster.AddToCounts), new[] { typeof(EquipmentElement), typeof(int) })]
        [HarmonyPrefix]
        public static bool AddToCountsPrefix(ItemRoster __instance, ref int __result, EquipmentElement rosterElement, int number)
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
                    return true;
                } else
                {
                    __result = -1;
                    return false;
                }
            } else
            {
                CallStackValidator.Validate(__instance, AllowedInstance);

                if (!AllowedInstance.IsAllowed(__instance))
                {
                    __result = -1;
                    return false;
                }
                else
                    return true;
            }
        }

        public static void AddToCountsOverride(ItemRoster itemRoster, EquipmentElement rosterElement, int amount)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = itemRoster;
                itemRoster.AddToCounts(rosterElement, amount);
            }
        }
    }
}
