using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Patches
{
    [HarmonyPatch(typeof(CraftingCampaignBehavior))]
    internal class TownOrdersPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehavior>();

        [HarmonyPatch("CreateTownOrder")]
        [HarmonyPrefix]
        public static bool CreateTownOrder(ref CraftingCampaignBehavior __instance, Hero orderOwner, int orderSlot)
        {
            // Server should create random town orders to be consistent for all clients
            if (ModInformation.IsClient) return false;

            // Replace TaleWorlds implementation for server
            float townOrderDifficulty = CraftingCampaignBehavior.GetTownOrderDifficulty(orderOwner.CurrentSettlement.Town, orderSlot);
            int pieceTier = (int)townOrderDifficulty / 50;
            CraftingTemplate randomElement = CraftingTemplate.All.GetRandomElement<CraftingTemplate>();
            string nextTownOrderId = __instance.GetNextTownOrderId();
            WeaponDesign weaponDesignTemplate = new WeaponDesign(randomElement, TextObject.GetEmpty(), __instance.GetWeaponPieces(randomElement, pieceTier), nextTownOrderId);
            __instance._craftingOrders[orderOwner.CurrentSettlement.Town].AddTownOrder(new CraftingOrder(orderOwner, townOrderDifficulty, weaponDesignTemplate, randomElement, orderSlot, nextTownOrderId));

            // Publish message with data for clients
            var message = new TownOrderCreated(__instance, townOrderDifficulty, pieceTier, randomElement, orderOwner, orderSlot);
            MessageBroker.Instance.Publish(__instance, message);

            // Skip original
            return false;
        }

        [HarmonyPatch("ReplaceCraftingOrder")]
        [HarmonyPrefix]
        public static bool ReplaceCraftingOrder(ref CraftingCampaignBehavior __instance, Town town, CraftingOrder order)
        {
            // Shouldn't ever be true, just in case
            if (ModInformation.IsClient) return false;

            // Clear existing CraftingOrder on clients
            int difficultyLevel = order.DifficultyLevel;
            var message = new CraftingOrderReplaced(__instance, town, difficultyLevel);
            MessageBroker.Instance.Publish(__instance, message);

            // Replace TaleWorlds implementation
            MBList<Hero> mblist = new MBList<Hero>();
            mblist.AddRange(town.Settlement.HeroesWithoutParty);
            foreach (MobileParty mobileParty in town.Settlement.Parties)
            {
                if (mobileParty.LeaderHero != null && !mobileParty.IsMainParty)
                {
                    mblist.Add(mobileParty.LeaderHero);
                }
            }
            __instance._craftingOrders[town].RemoveTownOrder(order);
            Hero targetHero = null;
            if (mblist.Count > 0)
            {
                targetHero = mblist.GetRandomElement<Hero>();
                __instance.CreateTownOrder(targetHero, difficultyLevel); // Call includes TownOrderCreated message from patch to update clients
            }

            // Skip original
            return false;
        }

        [HarmonyPatch("CompleteOrder")]
        [HarmonyPrefix]
        public static bool CompleteOrder(ref CraftingCampaignBehavior __instance, Town town, CraftingOrder craftingOrder, ItemObject craftedItem, Hero completerHero)
        {
            __instance.GetOrderResult(craftingOrder, craftedItem, out var flag, out var _, out var _, out var _);

            // Publish message with data
            var message = new OrderCompleted(__instance, town, craftingOrder, craftedItem, completerHero, Hero.MainHero, flag);
            MessageBroker.Instance.Publish(__instance, message);

            // Skip original
            return false;
        }
    }
}
