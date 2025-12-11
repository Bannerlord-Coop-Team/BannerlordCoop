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
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.ItemRosters.Patches
{
    [HarmonyPatch(typeof(ItemRoster))]
    internal class ItemRosterPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ItemRosterPatch>();
        private static bool TryResolvePartyBase(ItemRoster roster, out PartyBase partyBase)
        {
            if (ItemRosterLookup.TryGetValue(roster, out partyBase)) return true;

            var campaignManager = Campaign.Current?.CampaignObjectManager;
            if (campaignManager != null)
            {
                foreach (var mp in campaignManager.MobileParties)
                {
                    var pb = mp?.Party;
                    if (pb != null && pb.ItemRoster == roster)
                    {
                        partyBase = pb;
                        ItemRosterLookup.Set(roster, partyBase);
                        return true;
                    }
                }

                foreach (var settlement in campaignManager.Settlements)
                {
                    var pb = settlement?.Party;
                    if (pb != null && pb.ItemRoster == roster)
                    {
                        partyBase = pb;
                        ItemRosterLookup.Set(roster, partyBase);
                        return true;
                    }
                }
            }

            foreach (var mp in MobileParty.All)
            {
                var pb = mp?.Party;
                if (pb != null && pb.ItemRoster == roster)
                {
                    partyBase = pb;
                    ItemRosterLookup.Set(roster, partyBase);
                    return true;
                }
            }

            foreach (var settlement in Settlement.All)
            {
                var pb = settlement?.Party;
                if (pb != null && pb.ItemRoster == roster)
                {
                    partyBase = pb;
                    ItemRosterLookup.Set(roster, partyBase);
                    return true;
                }
            }

            partyBase = null;
            return false;
        }

        [HarmonyPatch(nameof(ItemRoster.AddToCounts), new[] { typeof(EquipmentElement), typeof(int) })]
        [HarmonyPrefix]
        public static bool AddToCountsPrefix(ItemRoster __instance, ref int __result, EquipmentElement rosterElement, int number)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                __result = -1;
                return false; // Disallow clients
            }

            return true; // Allow on server
        }

        [HarmonyPatch(nameof(ItemRoster.AddToCounts), new[] { typeof(EquipmentElement), typeof(int) })]
        [HarmonyPostfix]
        public static void AddToCountsPostfix(ItemRoster __instance, ref int __result, EquipmentElement rosterElement, int number)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return;

            if (ModInformation.IsClient)
            {
                return;
            }

            if (__result == -1)
            {
                return; // Don't publish unsucessful calls
            }

            if (TryResolvePartyBase(__instance, out var partyBase) == false)
            {
                Logger.Error("Unable to find party from item roster");
                return;
            }

            MessageBroker.Instance.Publish(__instance, new ItemRosterUpdated(
                        partyBase.Id,
                        rosterElement.Item.StringId,
                        rosterElement.ItemModifier?.StringId,
                        number));
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

            if (TryResolvePartyBase(__instance, out var partyBase) == false)
            {
                Logger.Error("Unable to find party from item roster");
                return false;
            }

            MessageBroker.Instance.Publish(__instance, new ItemRosterCleared(partyBase.Id));
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
