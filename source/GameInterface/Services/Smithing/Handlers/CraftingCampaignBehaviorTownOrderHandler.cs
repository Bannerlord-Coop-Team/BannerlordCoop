using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ItemObjects.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Messages;
using Serilog;
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
        private readonly IBinaryPackageFactory binaryPackageFactory;
        private readonly IItemObjectInterface itemObjectInterface;

        public CraftingCampaignBehaviorTownOrderHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network,
            IBinaryPackageFactory binaryPackageFactory,
            IItemObjectInterface itemObjectInterface)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.binaryPackageFactory = binaryPackageFactory;
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
            SendTownOrderCreated(obj.What);
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

        private void SendTownOrderCreated(TownOrderCreated obj)
        {
            if (!objectManager.TryGetId(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.CraftingCampaignBehavior?.GetType());
                return;
            }
            if (!objectManager.TryGetId(obj.RandomElement, out var randomElementId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.RandomElement?.GetType());
                return;
            }
            if (!objectManager.TryGetId(obj.OrderOwner, out var orderOwnerId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.OrderOwner?.GetType());
                return;
            }

            // Send to clients from server
            NetworkCreateTownOrder message = new(
                craftingCampaignBehaviorId,
                obj.TownOrderDifficulty,
                obj.PieceTier,
                randomElementId,
                orderOwnerId,
                obj.OrderSlot
            );
            network.SendAll(message);
        }

        private void CreateTownOrder(NetworkCreateTownOrder obj)
        {
            if (!objectManager.TryGetObject(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior))
            {
                Logger.Error("Unable to get object for craftingCampaignBehaviorId {id}", obj.CraftingCampaignBehaviorId);
                return;
            }
            if (!objectManager.TryGetObject(obj.RandomElementId, out CraftingTemplate randomElement))
            {
                Logger.Error("Unable to get object for randomElementId {id}", obj.RandomElementId);
                return;
            }
            if (!objectManager.TryGetObject(obj.OrderOwnerId, out Hero orderOwner))
            {
                Logger.Error("Unable to get object for orderOwnerId {id}", obj.OrderOwnerId);
                return;
            }

            // Replace TaleWorlds implementation
            string nextTownOrderId = craftingCampaignBehavior.GetNextTownOrderId();
            WeaponDesign weaponDesignTemplate = new WeaponDesign(randomElement, TextObject.GetEmpty(), craftingCampaignBehavior.GetWeaponPieces(randomElement, obj.PieceTier), nextTownOrderId);
            craftingCampaignBehavior._craftingOrders[orderOwner.CurrentSettlement.Town].AddTownOrder(new CraftingOrder(orderOwner, obj.TownOrderDifficulty, weaponDesignTemplate, randomElement, obj.OrderSlot, nextTownOrderId));
        }

        private void SendCraftingOrderReplaced(CraftingOrderReplaced obj)
        {
            if (!objectManager.TryGetId(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.CraftingCampaignBehavior?.GetType());
                return;
            }
            if (!objectManager.TryGetId(obj.Town, out var townId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.Town?.GetType());
                return;
            }

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
            if (!objectManager.TryGetObject(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior))
            {
                Logger.Error("Unable to get object for craftingCampaignBehaviorId {id}", obj.CraftingCampaignBehaviorId);
                return;
            }
            if (!objectManager.TryGetObject(obj.TownId, out Town town))
            {
                Logger.Error("Unable to get object for townId {id}", obj.TownId);
                return;
            }

            // Replace TaleWorlds implementation
            craftingCampaignBehavior._craftingOrders[town].Slots[obj.DifficultyLevel] = null; // Equivalent to craftingCampaignBehavior._craftingOrders[town].RemoveTownOrder(order), CraftingOrder can't be registered
            //craftingCampaignBehavior.CreateTownOrder(hero, obj.DifficultyLevel); // Changes applied on clients from CreateTownOrder call in ReplaceCraftingOrder patch
        }

        private void SendOrderCompleted(OrderCompleted obj)
        {
            if (!objectManager.TryGetId(obj.CraftingCampaignBehavior, out var craftingCampaignBehaviorId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.CraftingCampaignBehavior?.GetType());
                return;
            }
            if (!objectManager.TryGetId(obj.Town, out var townId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.Town?.GetType());
                return;
            }
            if (!objectManager.TryGetId(obj.CompleterHero, out var completerHeroId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.CompleterHero?.GetType());
                return;
            }
            if (!objectManager.TryGetId(obj.MainHero, out var mainHeroId))
            {
                Logger.Error("Unable to get network ID for instance of type {type}", obj.MainHero?.GetType());
                return;
            }

            CraftingOrderBinaryPackage craftingOrderBinaryPackage = binaryPackageFactory.GetBinaryPackage<CraftingOrderBinaryPackage>(obj.CraftingOrder);
            byte[] craftingOrderData = BinaryFormatterSerializer.Serialize(craftingOrderBinaryPackage);

            byte[] craftedItemData = itemObjectInterface.PackageItemObject(obj.CraftedItem);

            // Send to clients from server
            NetworkCompleteOrderServer message = new(
                craftingCampaignBehaviorId,
                townId,
                craftingOrderData,
                craftedItemData,
                completerHeroId,
                mainHeroId,
                obj.Flag
            );
            network.SendAll(message);
        }

        private void CompleteOrderServer(NetworkCompleteOrderServer obj)
        {
            if (!objectManager.TryGetObject(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior))
            {
                Logger.Error("Unable to get object for craftingCampaignBehaviorId {id}", obj.CraftingCampaignBehaviorId);
                return;
            }
            if (!objectManager.TryGetObject(obj.TownId, out Town town))
            {
                Logger.Error("Unable to get object for townId {id}", obj.TownId);
                return;
            }
            if (!objectManager.TryGetObject(obj.CompleterHeroId, out Hero completerHero))
            {
                Logger.Error("Unable to get object for completerHeroId {id}", obj.CompleterHeroId);
                return;
            }
            if (!objectManager.TryGetObject(obj.MainHeroId, out Hero mainHero))
            {
                Logger.Error("Unable to get object for mainHeroId {id}", obj.MainHeroId);
                return;
            }

            CraftingOrderBinaryPackage craftingOrderBinaryPackage = BinaryFormatterSerializer.Deserialize<CraftingOrderBinaryPackage>(obj.CraftingOrderData);
            CraftingOrder craftingOrder = craftingOrderBinaryPackage.Unpack<CraftingOrder>(binaryPackageFactory);

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
                    orderOwner.AddPower((float)(craftedItem.Tier + 1));
                    if (obj.Flag && completerHero.GetPerkValue(DefaultPerks.Crafting.ExperiencedSmith))
                    {
                        // Manage hero relations with dynamic sync
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(completerHero, orderOwner, (int)DefaultPerks.Crafting.ExperiencedSmith.SecondaryBonus, true);
                    }
                }
                craftingCampaignBehavior._craftingOrders[town].RemoveTownOrder(craftingOrder);
            }

            CampaignEventDispatcher.Instance.OnCraftingOrderCompleted(town, craftingOrder, craftedItem, completerHero);
        }

        private void CompleteOrderClients(NetworkCompleteOrderClients obj)
        {
            if (!objectManager.TryGetObject(obj.CraftingCampaignBehaviorId, out CraftingCampaignBehavior craftingCampaignBehavior))
            {
                Logger.Error("Unable to get object for craftingCampaignBehaviorId {id}", obj.CraftingCampaignBehaviorId);
                return;
            }
            if (!objectManager.TryGetObject(obj.TownId, out Town town))
            {
                Logger.Error("Unable to get object for townId {id}", obj.TownId);
                return;
            }
            if (!objectManager.TryGetObject(obj.CompleterHeroId, out Hero completerHero))
            {
                Logger.Error("Unable to get object for completerHeroId {id}", obj.CompleterHeroId);
                return;
            }

            CraftingOrderBinaryPackage craftingOrderBinaryPackage = BinaryFormatterSerializer.Deserialize<CraftingOrderBinaryPackage>(obj.CraftingOrderData);
            CraftingOrder craftingOrder = craftingOrderBinaryPackage.Unpack<CraftingOrder>(binaryPackageFactory);

            ItemObject craftedItem = itemObjectInterface.UnpackItemObject(obj.CraftedItemData);

            // Replace TaleWorlds implementation for clients
            Hero orderOwner = craftingOrder.OrderOwner;
            if (craftingCampaignBehavior._craftingOrders[town].CustomOrders.Contains(craftingOrder))
            {
                craftingCampaignBehavior._craftingOrders[town].RemoveCustomOrder(craftingOrder);
            }
            else
            {
                if (craftingOrder.IsLordOrder)
                {
                    craftingCampaignBehavior.ChangeCraftedOrderWithTheNoblesWeaponIfItIsBetter(craftedItem, craftingOrder);
                }
                else
                {
                    orderOwner.AddPower((float)(craftedItem.Tier + 1));
                }
                craftingCampaignBehavior._craftingOrders[town].RemoveTownOrder(craftingOrder);
            }

            CampaignEventDispatcher.Instance.OnCraftingOrderCompleted(town, craftingOrder, craftedItem, completerHero);
        }
    }
}
