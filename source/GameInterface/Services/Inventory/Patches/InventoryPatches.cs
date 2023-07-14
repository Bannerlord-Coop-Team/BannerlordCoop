using Common.Messaging;
using Common.Util;
using GameInterface.Services.Inventory.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Patches
{
    /// <summary>
    /// Patch for ItemRoster
    /// </summary>
    [HarmonyPatch(typeof(PartyBase), nameof(PartyBase.ItemRoster), MethodType.Setter)]
    public class InventoryPatches
    {
        public static readonly AllowedInstance<MobileParty> AllowedInstance = new AllowedInstance<MobileParty>();

        private static bool Prefix(ref PartyBase __instance)
        {
            if (AllowedInstance.IsAllowed(__instance.MobileParty)) return true;

            if (ModInformation.IsClient && __instance.MobileParty.StringId != MobileParty.MainParty?.StringId) return false;

            List<string> itemIds = new List<string>();
            List<string> modifierIds = new List<string>();
            List<int> amounts = new List<int>();

            ItemRoster itemRoster = __instance.ItemRoster;

            if (itemRoster == null) return true;

            for (int i = 0; i < __instance.ItemRoster?.Count; i++)
            {
                itemIds.Add(itemRoster[i].EquipmentElement.Item.StringId);
                modifierIds.Add(itemRoster[i].EquipmentElement.ItemModifier?.StringId);
                amounts.Add(itemRoster[i].Amount);
            }

            var message = new ItemRosterUpdateAttempted(itemIds.ToArray(), modifierIds.ToArray(), amounts.ToArray(), __instance.MobileParty.StringId);

            MessageBroker.Instance.Publish(__instance, message);

            return false;
        }


    }
}