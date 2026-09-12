using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.ItemRosters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TownMarketDatas.Patches;
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

        internal static bool IsRegistered(ItemRoster itemRoster)
            => ContainerProvider.TryResolve<IObjectManager>(out var objectManager) &&
               objectManager.TryGetId(itemRoster, out _);

        [HarmonyPatch(nameof(ItemRoster.AddToCounts), new[] { typeof(EquipmentElement), typeof(int) })]
        [HarmonyPrefix]
        public static bool AddToCountsPrefix(ItemRoster __instance, ref int __result, EquipmentElement rosterElement, int number)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                if (IsRegistered(__instance))
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
            if (!IsRegistered(__instance)) return;

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
                return !IsRegistered(__instance);
            }

            if (!IsRegistered(__instance)) return true;

            MessageBroker.Instance.Publish(__instance, new ItemRosterCleared(__instance));

            return true; // Allow on server
        }

        public static void AddToCountsOverride(ItemRoster itemRoster, EquipmentElement rosterElement, int amount)
        {
            GameThread.Run(() =>
            {
                using (new AllowedThread())
                using (TownMarketDataPatches.SuppressReceivedRosterUpdate())
                {
                    itemRoster?.AddToCounts(rosterElement, amount);
                }
            });
        }

        public static void ClearOverride(ItemRoster itemRoster)
        {
            GameThread.Run(() =>
            {
                using (new AllowedThread())
                {
                    itemRoster?.Clear();
                }
            });
        }
    }
}
