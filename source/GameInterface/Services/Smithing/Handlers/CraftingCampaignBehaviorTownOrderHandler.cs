using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ItemObjects.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Handlers
{
    internal class CraftingCampaignBehaviorTownOrderHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorTownOrderHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly IItemObjectInterface itemObjectInterface;

        public CraftingCampaignBehaviorTownOrderHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network,
            IItemObjectInterface itemObjectInterface)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.itemObjectInterface = itemObjectInterface;
            messageBroker.Subscribe<TownOrderCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateTownOrder>(Handle);
            messageBroker.Subscribe<CraftingOrderReplaced>(Handle);
            messageBroker.Subscribe<NetworkReplaceCraftingOrder>(Handle);
            messageBroker.Subscribe<OrderCompleted>(Handle);
            messageBroker.Subscribe<NetworkCompleteOrderServer>(Handle);
            messageBroker.Subscribe<NetworkCompleteOrderClients>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<TownOrderCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateTownOrder>(Handle);
            messageBroker.Unsubscribe<CraftingOrderReplaced>(Handle);
            messageBroker.Unsubscribe<NetworkReplaceCraftingOrder>(Handle);
            messageBroker.Unsubscribe<OrderCompleted>(Handle);
            messageBroker.Unsubscribe<NetworkCompleteOrderServer>(Handle);
            messageBroker.Unsubscribe<NetworkCompleteOrderClients>(Handle);
        }

        private void Handle(MessagePayload<TownOrderCreated> obj)
        {
            CreateTownOrderServer(obj.What);
        }

        private void Handle(MessagePayload<NetworkCreateTownOrder> obj)
        {
            CreateTownOrder(obj.What);
        }
        private void Handle(MessagePayload<CraftingOrderReplaced> obj)
        {
            SendCraftingOrderReplaced(obj.What);
        }

        private void Handle(MessagePayload<NetworkReplaceCraftingOrder> obj)
        {
            ReplaceCraftingOrder(obj.What);
        }

        private void Handle(MessagePayload<OrderCompleted> obj)
        {
            SendOrderCompleted(obj.What);
        }

        private void Handle(MessagePayload<NetworkCompleteOrderServer> obj)
        {
            NetworkCompleteOrderClients message = new(obj.What);
            network.SendAll(message);

            CompleteOrderServer(obj.What);
        }

        private void Handle(MessagePayload<NetworkCompleteOrderClients> obj)
        {
            CompleteOrderClients(obj.What);
        }

        private void CreateTownOrderServer(TownOrderCreated obj)
        {
            // Replace TaleWorlds implementation for server
            float townOrderDifficulty = CraftingCampaignBehavior.GetTownOrderDifficulty(obj.OrderOwner.CurrentSettlement.Town, obj.OrderSlot);
            int pieceTier = (int)townOrderDifficulty / 50;
            CraftingTemplate randomElement = CraftingTemplate.All.GetRandomElement<CraftingTemplate>();
            string nextTownOrderId = obj.CraftingCampaignBehavior.GetNextTownOrderId();

            WeaponDesign weaponDesignTemplate = new WeaponDesign(randomElement, TextObject.GetEmpty(), obj.CraftingCampaignBehavior.GetWeaponPieces(randomElement, pieceTier), nextTownOrderId);
            objectManager.AddNewObject(weaponDesignTemplate, out var weaponDesignId);

            CraftingOrder order;
            order = new CraftingOrder(obj.OrderOwner, townOrderDifficulty, weaponDesignTemplate, randomElement, obj.OrderSlot, nextTownOrderId);
            using (new AllowedThread())
            {
                order.PreCraftedWeaponDesignItem.StringId = nextTownOrderId;
            }

            obj.CraftingCampaignBehavior._craftingOrders[obj.OrderOwner.CurrentSettlement.Town].AddTownOrder(order);

            SendTownOrderCreated(obj, order, randomElement, pieceTier, nextTownOrderId);
        }

        private void SendTownOrderCreated(TownOrderCreated obj, CraftingOrder craftingOrder, CraftingTemplate randomElement, int pieceTier, string nextTownOrderId)
        {
            if (!objectManager.TryGetIdWithLogging(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.OrderOwner, out var orderOwnerId)) return;
            if (!objectManager.TryGetIdWithLogging(randomElement, out var randomElementId)) return;

            if (!objectManager.AddNewObject(craftingOrder, out var craftingOrderId) &&
                !objectManager.TryGetIdWithLogging(craftingOrder, out craftingOrderId)) return;

            // Send to clients from server
            NetworkCreateTownOrder message = new(
                craftingCampaignBehaviorId,
                orderOwnerId,
                craftingOrderId,
                randomElementId,
                pieceTier,
                nextTownOrderId
            );
            network.SendAll(message);
        }

        private void CreateTownOrder(NetworkCreateTownOrder obj)
        {
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.RandomElementId, out CraftingTemplate randomElement)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.OrderOwnerId, out Hero orderOwner)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingOrderId, out CraftingOrder craftingOrder)) return;

            WeaponDesign weaponDesignTemplate = new WeaponDesign(randomElement, TextObject.GetEmpty(), craftingCampaignBehavior.GetWeaponPieces(randomElement, obj.PieceTier), obj.NextTownOrderId);
            craftingOrder._weaponDesignTemplate = weaponDesignTemplate;

            using (new AllowedThread())
            {
                Crafting.GenerateItem(weaponDesignTemplate, TextObject.GetEmpty(), orderOwner.Culture, randomElement.ItemModifierGroup, ref craftingOrder.PreCraftedWeaponDesignItem, obj.NextTownOrderId);
            }
            craftingOrder._preCraftedWeaponDesignItemData = new CraftingCampaignBehavior.CraftedItemInitializationData(craftingOrder.WeaponDesignTemplate, craftingOrder.PreCraftedWeaponDesignItem.Name, craftingOrder.OrderOwner.Culture);

            // Replace TaleWorlds implementation
            craftingCampaignBehavior._craftingOrders[orderOwner.CurrentSettlement.Town].AddTownOrder(craftingOrder);

            // Need to refresh client weapon designs for potential new orders while in CraftingState
            MessageBroker.Instance.Publish(this, new RefreshWeaponDesignVM(orderOwner.CurrentSettlement.Town));
        }

        private void SendCraftingOrderReplaced(CraftingOrderReplaced obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.Town, out var townId)) return;

            // Send to clients from server
            NetworkReplaceCraftingOrder message = new(
                craftingCampaignBehaviorId,
                townId,
                obj.DifficultyLevel
            );
            network.SendAll(message);
        }

        private void ReplaceCraftingOrder(NetworkReplaceCraftingOrder obj)
        {
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.TownId, out Town town)) return;

            // Replace TaleWorlds implementation
            craftingCampaignBehavior._craftingOrders[town].Slots[obj.DifficultyLevel] = null; // Equivalent to craftingCampaignBehavior._craftingOrders[town].RemoveTownOrder(order)
            //craftingCampaignBehavior.CreateTownOrder(hero, obj.DifficultyLevel); // Changes applied on clients from CreateTownOrder call in ReplaceCraftingOrder patch
        }

        private void SendOrderCompleted(OrderCompleted obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.Town, out var townId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.CompleterHero, out var completerHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.MainHero, out var mainHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.CraftingOrder, out var craftingOrderId)) return; // CraftingOrderId is returning as null, unable to craft items from crafting orders generated since game start because of it

            byte[] craftedItemData = itemObjectInterface.PackageItemObject(obj.CraftedItem);

            // Send to clients from server
            NetworkCompleteOrderServer message = new(
                craftingCampaignBehaviorId,
                townId,
                craftingOrderId,
                craftedItemData,
                completerHeroId,
                mainHeroId,
                obj.Flag
            );
            network.SendAll(message);
        }

        private void CompleteOrderServer(NetworkCompleteOrderServer obj)
        {
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.TownId, out Town town)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CompleterHeroId, out Hero completerHero)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.MainHeroId, out Hero mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingOrderId, out CraftingOrder craftingOrder)) return;

            ItemObject craftedItem = itemObjectInterface.UnpackItemObject(obj.CraftedItemData);

            // Replace TaleWorlds implementation

            // Manage hero gold with dynamic sync
            int amount = craftingCampaignBehavior.CalculateOrderPriceDifference(craftingOrder, craftedItem);
            GiveGoldAction.ApplyBetweenCharacters(null, mainHero, amount, false);

            Hero orderOwner = craftingOrder.OrderOwner;
            if (craftingCampaignBehavior._craftingOrders[town].CustomOrders.Contains(craftingOrder))
            {
                craftingCampaignBehavior._craftingOrders[town].RemoveCustomOrder(craftingOrder);
            }
            else
            {
                if (craftingOrder.IsLordOrder)
                {
                    // Manage Hero.BattleEquipment with dynamic sync
                    craftingCampaignBehavior.ChangeCraftedOrderWithTheNoblesWeaponIfItIsBetter(craftedItem, craftingOrder);
                    if (orderOwner.PartyBelongedTo != null)
                    {
                        // Manage party roster with dynamic sync
                        craftingCampaignBehavior.GiveTroopToNobleAtWeaponTier((int)craftedItem.Tier, orderOwner);
                    }
                    if (obj.Flag && completerHero.GetPerkValue(DefaultPerks.Crafting.SteelMaker3))
                    {
                        // Manage hero relations with dynamic sync
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(completerHero, orderOwner, (int)DefaultPerks.Crafting.SteelMaker3.SecondaryBonus, true);
                    }
                }
                else
                {
                    // Manage Hero.Power with dynamic sync
                    orderOwner.AddPower((float)(craftedItem.Tier + 1));
                    if (obj.Flag && completerHero.GetPerkValue(DefaultPerks.Crafting.ExperiencedSmith))
                    {
                        // Manage hero relations with dynamic sync
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(completerHero, orderOwner, (int)DefaultPerks.Crafting.ExperiencedSmith.SecondaryBonus, true);
                    }
                }
                CraftingOrder previousOrder = craftingCampaignBehavior._craftingOrders[town].Slots[craftingOrder.DifficultyLevel];

                craftingCampaignBehavior._craftingOrders[town].RemoveTownOrder(craftingOrder);

                // Remove previous order from objectManager
                if (previousOrder is not null)
                {
                    MessageBroker.Instance.Publish(null, new InstanceDestroyed<CraftingOrder>(previousOrder));
                }
            }

            CampaignEventDispatcher.Instance.OnCraftingOrderCompleted(town, craftingOrder, craftedItem, completerHero);
        }

        private void CompleteOrderClients(NetworkCompleteOrderClients obj)
        {
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.TownId, out Town town)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CompleterHeroId, out Hero completerHero)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingOrderId, out CraftingOrder craftingOrder)) return;

            ItemObject craftedItem = itemObjectInterface.UnpackItemObject(obj.CraftedItemData);

            // Replace TaleWorlds implementation for clients
            if (craftingCampaignBehavior._craftingOrders[town].CustomOrders.Contains(craftingOrder))
            {
                craftingCampaignBehavior._craftingOrders[town].RemoveCustomOrder(craftingOrder);
            }
            else
            {
                craftingCampaignBehavior._craftingOrders[town].RemoveTownOrder(craftingOrder);
            }

            CampaignEventDispatcher.Instance.OnCraftingOrderCompleted(town, craftingOrder, craftedItem, completerHero);

            MessageBroker.Instance.Publish(this, new RefreshWeaponDesignVM(town)); // Causes some errors sometimes with each being different and hard to replicate. Such as one in in RichText.cs and one in the GauntletLayer
        }
    }
}
