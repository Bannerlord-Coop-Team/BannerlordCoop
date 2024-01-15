using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.GameDebug.Patches;
using GameInterface.Services.ItemRosters.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemRosters.Patches
{
    [HarmonyPatch(typeof(ItemRoster))]
    internal class ItemRosterPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ItemRosterPatch>();

        public static AllowedInstance<ItemRoster> AllowedInstance = new();

        [HarmonyPatch(nameof(ItemRoster.AddToCounts), new[] { typeof(EquipmentElement), typeof(int) })]
        [HarmonyPrefix]
        public static bool AddToCountsPrefix(ItemRoster __instance, ref int __result, EquipmentElement rosterElement, int number)
        {
            // If AddToCountsOverride is called allow original
            if (AllowedInstance.IsAllowed(__instance)) return true;

            CallStackValidator.Validate(__instance, AllowedInstance);

            // Skip if client
            if (ModInformation.IsClient)
            {
                __result = -1;
                return false;
            }

            if (ItemRosterLookup.TryGetValue(__instance, out var partyBase) == false)
            {
                Logger.Error("Unable to find party from item roster");
                __result = -1;
                return false;
            }

            // Publish on server
            MessageBroker.Instance.Publish(__instance, new ItemRosterUpdated(
                        partyBase.Id,
                        rosterElement.Item.StringId,
                        rosterElement.ItemModifier?.StringId,
                        number
                    ));

            return true;
        }

        public static void AddToCountsOverride(ItemRoster itemRoster, EquipmentElement rosterElement, int amount)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = itemRoster;
                GameLoopRunner.RunOnMainThread(() =>
                {
                    itemRoster.AddToCounts(rosterElement, amount);
                }, true);
            }
        }
    }
}
