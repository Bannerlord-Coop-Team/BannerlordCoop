using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

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
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            // Server should create random town orders to be consistent for all clients
            if (ModInformation.IsClient) return false;

            // Publish message with data for clients
            var message = new TownOrderCreated(__instance, orderOwner, orderSlot);
            MessageBroker.Instance.Publish(__instance, message);

            // Skip original
            return false;
        }

        [HarmonyPatch("ReplaceCraftingOrder")]
        [HarmonyPrefix]
        public static bool ReplaceCraftingOrder(ref CraftingCampaignBehavior __instance, Town town, CraftingOrder order)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            // Shouldn't ever be true, just in case
            if (ModInformation.IsClient) return false;

            // Clear existing CraftingOrder on clients
            int difficultyLevel = order.DifficultyLevel;
            var message = new CraftingOrderReplaced(__instance, town, difficultyLevel);
            MessageBroker.Instance.Publish(__instance, message);

            CraftingOrder previousOrder = __instance._craftingOrders[town].Slots[difficultyLevel];

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

            // Remove previous order from objectManager
            if (previousOrder is not null)
            {
                MessageBroker.Instance.Publish(null, new InstanceDestroyed<CraftingOrder>(previousOrder));
            }

            // Skip original
            return false;
        }

        [HarmonyPatch("CompleteOrder")]
        [HarmonyPrefix]
        public static bool CompleteOrder(ref CraftingCampaignBehavior __instance, Town town, CraftingOrder craftingOrder, ItemObject craftedItem, Hero completerHero)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            Logger.Information("CompleteOrderPatch Attempting to get order result....");
            bool flag = false;
            using (new AllowedThread())
            {
                __instance.GetOrderResult(craftingOrder, craftedItem, out flag, out var _, out var _, out var _);
            }
            Logger.Information("CompleteOrderPatch Order result ran.");

            // Publish message with data
            var message = new OrderCompleted(__instance, town, craftingOrder, craftedItem, completerHero, Hero.MainHero, flag);
            MessageBroker.Instance.Publish(__instance, message);

            // Skip original
            return false;
        }
    }
}
