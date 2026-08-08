using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing.Interfaces;
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

namespace GameInterface.Services.Smithing.Handlers;

internal class CraftingCampaignBehaviorTownOrderHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorTownOrderHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ICraftingCampaignBehaviorInterface craftingCampaignBehaviorInterface;

    public CraftingCampaignBehaviorTownOrderHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ICraftingCampaignBehaviorInterface craftingCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.craftingCampaignBehaviorInterface = craftingCampaignBehaviorInterface;

        messageBroker.Subscribe<TownOrderCreated>(Handle_TownOrderCreated);
        messageBroker.Subscribe<NetworkCreateTownOrder>(Handle_NetworkCreateTownOrder);

        messageBroker.Subscribe<CraftingOrderReplaced>(Handle_CraftingOrderReplaced);
        messageBroker.Subscribe<NetworkReplaceCraftingOrder>(Handle_NetworkReplaceCraftingOrder);

        messageBroker.Subscribe<CompleteOrderServer>(Handle_CompleteOrderServer);
        messageBroker.Subscribe<NetworkCompleteOrderClients>(Handle_NetworkCompleteOrderClients);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TownOrderCreated>(Handle_TownOrderCreated);
        messageBroker.Unsubscribe<NetworkCreateTownOrder>(Handle_NetworkCreateTownOrder);

        messageBroker.Unsubscribe<CraftingOrderReplaced>(Handle_CraftingOrderReplaced);
        messageBroker.Unsubscribe<NetworkReplaceCraftingOrder>(Handle_NetworkReplaceCraftingOrder);

        messageBroker.Unsubscribe<CompleteOrderServer>(Handle_CompleteOrderServer);
        messageBroker.Unsubscribe<NetworkCompleteOrderClients>(Handle_NetworkCompleteOrderClients);
    }

    private void Handle_TownOrderCreated(MessagePayload<TownOrderCreated> obj)
    {
        CreateTownOrderServer(obj.What);
    }

    private void Handle_NetworkCreateTownOrder(MessagePayload<NetworkCreateTownOrder> obj)
    {
        CreateTownOrder(obj.What);
    }
    private void Handle_CraftingOrderReplaced(MessagePayload<CraftingOrderReplaced> obj)
    {
        SendCraftingOrderReplaced(obj.What);
    }

    private void Handle_NetworkReplaceCraftingOrder(MessagePayload<NetworkReplaceCraftingOrder> obj)
    {
        ReplaceCraftingOrder(obj.What);
    }

    private void Handle_CompleteOrderServer(MessagePayload<CompleteOrderServer> obj)
    {
        CompleteOrderServer(obj.What);
    }

    private void Handle_NetworkCompleteOrderClients(MessagePayload<NetworkCompleteOrderClients> obj)
    {
        CompleteOrderClients(obj.What);
    }

    private void CreateTownOrderServer(TownOrderCreated obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingBehavior)) return;

            // Replace TaleWorlds implementation for server
            float townOrderDifficulty = CraftingCampaignBehavior.GetTownOrderDifficulty(obj.OrderOwner.CurrentSettlement.Town, obj.OrderSlot);
            int pieceTier = (int)townOrderDifficulty / 50;
            CraftingTemplate randomElement = CraftingTemplate.All.GetRandomElement<CraftingTemplate>();
            string nextTownOrderId = craftingBehavior.GetNextTownOrderId();

            WeaponDesign weaponDesignTemplate = new WeaponDesign(randomElement, TextObject.GetEmpty(), craftingBehavior.GetWeaponPieces(randomElement, pieceTier), nextTownOrderId);

            CraftingOrder order;
            order = new CraftingOrder(obj.OrderOwner, townOrderDifficulty, weaponDesignTemplate, randomElement, obj.OrderSlot, nextTownOrderId);
            using (new AllowedThread())
            {
                order.PreCraftedWeaponDesignItem.StringId = nextTownOrderId;
            }

            craftingBehavior._craftingOrders[obj.OrderOwner.CurrentSettlement.Town].AddTownOrder(order);

            SendTownOrderCreated(obj, order, randomElement, pieceTier, nextTownOrderId);
        });
    }

    private void SendTownOrderCreated(TownOrderCreated obj, CraftingOrder craftingOrder, CraftingTemplate randomElement, int pieceTier, string nextTownOrderId)
    {
        if (!objectManager.TryGetIdWithLogging(obj.OrderOwner, out var orderOwnerId)) return;
        if (!objectManager.TryGetIdWithLogging(randomElement, out var randomElementId)) return;

        if (!objectManager.TryGetIdWithLogging(craftingOrder, out var craftingOrderId)) return;

        // Send to clients from server
        NetworkCreateTownOrder message = new(
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
        GameThread.RunSafe(() =>
        {
            if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.RandomElementId, out CraftingTemplate randomElement)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.OrderOwnerId, out Hero orderOwner)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingOrderId, out CraftingOrder craftingOrder)) return;

            using (new AllowedThread())
            {
                WeaponDesign weaponDesignTemplate = new WeaponDesign(randomElement, TextObject.GetEmpty(), craftingBehavior.GetWeaponPieces(randomElement, obj.PieceTier), obj.NextTownOrderId);
                craftingOrder._weaponDesignTemplate = weaponDesignTemplate;
                Crafting.GenerateItem(weaponDesignTemplate, TextObject.GetEmpty(), orderOwner.Culture, randomElement.ItemModifierGroup, ref craftingOrder.PreCraftedWeaponDesignItem, obj.NextTownOrderId);
                craftingOrder._preCraftedWeaponDesignItemData = new CraftingCampaignBehavior.CraftedItemInitializationData(craftingOrder.WeaponDesignTemplate, craftingOrder.PreCraftedWeaponDesignItem.Name, craftingOrder.OrderOwner.Culture);

                // Replace TaleWorlds implementation
                craftingBehavior._craftingOrders[orderOwner.CurrentSettlement.Town].AddTownOrder(craftingOrder);
            }

            // Need to refresh client weapon designs for potential new orders while in CraftingState
            MessageBroker.Instance.Publish(this, new RefreshWeaponDesignVM(orderOwner.CurrentSettlement.Town));
        });
    }

    private void SendCraftingOrderReplaced(CraftingOrderReplaced obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.Town, out var townId)) return;

        // Send to clients from server
        NetworkReplaceCraftingOrder message = new(
            townId,
            obj.DifficultyLevel
        );
        network.SendAll(message);
    }

    private void ReplaceCraftingOrder(NetworkReplaceCraftingOrder obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.TownId, out Town town)) return;

            // Replace TaleWorlds implementation
            craftingBehavior._craftingOrders[town].Slots[obj.DifficultyLevel] = null; // Equivalent to craftingCampaignBehavior._craftingOrders[town].RemoveTownOrder(order)
            //craftingBehavior.CreateTownOrder(hero, obj.DifficultyLevel); // Changes applied on clients from CreateTownOrder call in ReplaceCraftingOrder patch
        });
    }

    private void CompleteOrderServer(CompleteOrderServer data)
    {
        GameThread.RunSafe(() =>
        {
            if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingBehavior)) return;

            var craftingOrder = data.CraftingOrder;
            var craftedItem = data.CraftedItem;
            var town = data.Town;
            var completerHero = data.CompleterHero;

            // Replace TaleWorlds implementation
            int amount = craftingBehavior.CalculateOrderPriceDifference(craftingOrder, craftedItem);
            GiveGoldAction.ApplyBetweenCharacters(null, data.MainHero, amount, false);

            Hero orderOwner = craftingOrder.OrderOwner;
            CraftingOrder previousOrder = null;

            craftingBehavior.GetOrderResult(craftingOrder, craftedItem, out var isSucceed, out _, out _, out _);
            if (craftingBehavior._craftingOrders[town].CustomOrders.Contains(craftingOrder))
            {
                craftingBehavior._craftingOrders[town].RemoveCustomOrder(craftingOrder);
            }
            else
            {
                if (craftingOrder.IsLordOrder)
                {
                    craftingBehavior.ChangeCraftedOrderWithTheNoblesWeaponIfItIsBetter(craftedItem, craftingOrder);
                    if (orderOwner.PartyBelongedTo != null)
                    {
                        craftingBehavior.GiveTroopToNobleAtWeaponTier((int)craftedItem.Tier, orderOwner);
                    }
                    if (isSucceed && completerHero.GetPerkValue(DefaultPerks.Crafting.SteelMaker3))
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(completerHero, orderOwner, (int)DefaultPerks.Crafting.SteelMaker3.SecondaryBonus, true);
                    }
                }
                else
                {
                    orderOwner.AddPower((float)(craftedItem.Tier + 1));
                    if (isSucceed && completerHero.GetPerkValue(DefaultPerks.Crafting.ExperiencedSmith))
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(completerHero, orderOwner, (int)DefaultPerks.Crafting.ExperiencedSmith.SecondaryBonus, true);
                    }
                }
                previousOrder = craftingBehavior._craftingOrders[town].Slots[craftingOrder.DifficultyLevel];

                craftingBehavior._craftingOrders[town].RemoveTownOrder(craftingOrder);
            }

            CampaignEventDispatcher.Instance.OnCraftingOrderCompleted(town, craftingOrder, craftedItem, completerHero);

            if (!objectManager.TryGetIdWithLogging(data.Town, out var townId)) return;
            if (!objectManager.TryGetIdWithLogging(data.CraftingOrder, out var craftingOrderId)) return;
            if (!objectManager.TryGetIdWithLogging(data.CraftedItem, out var craftedItemId)) return;
            if (!objectManager.TryGetIdWithLogging(data.CompleterHero, out var completerHeroId)) return;

            network.SendAll(new NetworkCompleteOrderClients(townId, craftingOrderId, craftedItemId, completerHeroId));

            // Remove previous order from objectManager
            // Queue destroying the instance after sending NetworkCompleteOrderClients message
            // Destroying the instance before sending client message prevents clients from being able to resolve the removed crafting order by network id
            if (previousOrder is not null)
            {
                MessageBroker.Instance.Publish(null, new InstanceDestroyed<CraftingOrder>(previousOrder));
            }
        });
    }

    private void CompleteOrderClients(NetworkCompleteOrderClients obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out var craftingBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.TownId, out Town town)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CompleterHeroId, out Hero completerHero)) return;
            if (!objectManager.TryGetObjectWithLogging(obj.CraftingOrderId, out CraftingOrder craftingOrder)) return;
            //if (!objectManager.TryGetObjectWithLogging(obj.CraftedItemId, out ItemObject craftedItem)) return;

            using (new AllowedThread())
            {
                // Replace TaleWorlds implementation for clients
                if (craftingBehavior._craftingOrders[town].CustomOrders.Contains(craftingOrder))
                {
                    craftingBehavior._craftingOrders[town].RemoveCustomOrder(craftingOrder);
                }
                else
                {
                    craftingBehavior._craftingOrders[town].RemoveTownOrder(craftingOrder);
                }

                // Crafted item hasn't been created by this point. This call might be needed on clients later though for quests
                //CampaignEventDispatcher.Instance.OnCraftingOrderCompleted(town, craftingOrder, craftedItem, completerHero);

                MessageBroker.Instance.Publish(this, new RefreshWeaponDesignVM(town));
            }
        });
    }
}
