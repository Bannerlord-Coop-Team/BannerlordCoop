using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
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

        [HarmonyPatch(nameof(ItemRoster.AddToCounts), new[] { typeof(EquipmentElement), typeof(int) })]
        [HarmonyPrefix]
        public static bool AddToCountsPrefix(ItemRoster __instance, ref int __result, EquipmentElement rosterElement, int number)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client changed managed {var}", nameof(ItemRoster.AddToCounts));
                return true;
            }

            return true; // Allow on server
        }

        [HarmonyPatch(nameof(ItemRoster.AddToCounts), new[] { typeof(EquipmentElement), typeof(int) })]
        [HarmonyPostfix]
        public static void AddToCountsPostfix(ItemRoster __instance, ref int __result, EquipmentElement rosterElement, int number)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return;

            // Don't publish unsucessful calls
            if (__result == -1) return;

            if (ModInformation.IsClient) return;

            var message = new ItemRosterUpdated(
                __instance,
                rosterElement.Item,
                rosterElement.ItemModifier,
                number);

            MessageBroker.Instance.Publish(__instance, message);
        }

        [HarmonyPatch(nameof(ItemRoster.Clear))]
        [HarmonyPrefix]
        public static bool ClearPrefix(ItemRoster __instance)
        {
            // Skip this prefix, if called by the mod
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                return false; // Disallow on clients
            } 

            MessageBroker.Instance.Publish(__instance, new ItemRosterCleared(__instance));

            return true; // Allow on server
        }

        public static void AddToCountsOverride(ItemRoster itemRoster, EquipmentElement rosterElement, int amount)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    itemRoster?.AddToCounts(rosterElement, amount);
                }
            });
        }

        public static void ClearOverride(ItemRoster itemRoster)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    itemRoster?.Clear();
                }
            });
        }
    }
}
