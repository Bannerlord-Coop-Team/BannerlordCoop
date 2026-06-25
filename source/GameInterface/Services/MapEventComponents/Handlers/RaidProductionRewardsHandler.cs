using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ItemObjects;
using GameInterface.Services.MapEventComponents.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEventComponents.Handlers;

internal class RaidProductionRewardsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<RaidProductionRewardsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ItemObjectRegistry itemObjectRegistry;

    public RaidProductionRewardsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ItemObjectRegistry itemObjectRegistry)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.itemObjectRegistry = itemObjectRegistry;

        messageBroker.Subscribe<RaidProductionRewardsUpdated>(Handle_RaidProductionRewardsUpdated);
        messageBroker.Subscribe<RaidLootedItemsUpdated>(Handle_RaidLootedItemsUpdated);
        messageBroker.Subscribe<NetworkRaidProductionRewardsUpdated>(Handle_NetworkRaidProductionRewardsUpdated);
        messageBroker.Subscribe<NetworkRaidLootedItemsUpdated>(Handle_NetworkRaidLootedItemsUpdated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RaidProductionRewardsUpdated>(Handle_RaidProductionRewardsUpdated);
        messageBroker.Unsubscribe<RaidLootedItemsUpdated>(Handle_RaidLootedItemsUpdated);
        messageBroker.Unsubscribe<NetworkRaidProductionRewardsUpdated>(Handle_NetworkRaidProductionRewardsUpdated);
        messageBroker.Unsubscribe<NetworkRaidLootedItemsUpdated>(Handle_NetworkRaidLootedItemsUpdated);
    }

    private void Handle_RaidProductionRewardsUpdated(MessagePayload<RaidProductionRewardsUpdated> payload)
    {
        if (ModInformation.IsClient)
            return;

        var component = payload.What.Component;
        if (!objectManager.TryGetIdWithLogging(component, out var componentId))
            return;

        var itemIds = new List<string>();
        var values = new List<float>();
        if (component._raidProductionRewards != null)
        {
            foreach (var reward in component._raidProductionRewards)
            {
                if (!TryGetRewardItemId(reward.Key, out var itemId))
                    continue;

                itemIds.Add(itemId);
                values.Add(reward.Value);
            }
        }

        network.SendAll(new NetworkRaidProductionRewardsUpdated(componentId, itemIds.ToArray(), values.ToArray()));
    }

    private void Handle_RaidLootedItemsUpdated(MessagePayload<RaidLootedItemsUpdated> payload)
    {
        if (ModInformation.IsClient)
            return;

        var data = payload.What;
        if (data.MobileParty == null || data.LootedItems == null || data.LootedItems.Count == 0)
            return;

        if (!objectManager.TryGetIdWithLogging(data.MobileParty, out var partyId))
            return;

        var itemIds = new List<string>();
        var amounts = new List<int>();
        foreach (var element in data.LootedItems)
        {
            var item = element.EquipmentElement.Item;
            if (item == null || element.Amount <= 0)
                continue;

            if (!TryGetRewardItemId(item, out var itemId))
                continue;

            itemIds.Add(itemId);
            amounts.Add(element.Amount);
        }

        if (itemIds.Count == 0)
            return;

        network.SendAll(new NetworkRaidLootedItemsUpdated(partyId, itemIds.ToArray(), amounts.ToArray()));
    }

    private void Handle_NetworkRaidProductionRewardsUpdated(MessagePayload<NetworkRaidProductionRewardsUpdated> payload)
    {
        if (ModInformation.IsServer)
            return;

        var data = payload.What;
        GameThread.RunSafe(() => ApplyRewards(data), context: nameof(Handle_NetworkRaidProductionRewardsUpdated));
    }

    private void Handle_NetworkRaidLootedItemsUpdated(MessagePayload<NetworkRaidLootedItemsUpdated> payload)
    {
        if (ModInformation.IsServer)
            return;

        var data = payload.What;
        GameThread.RunSafe(() => ApplyLootedItems(data), context: nameof(Handle_NetworkRaidLootedItemsUpdated));
    }

    private void ApplyRewards(NetworkRaidProductionRewardsUpdated data)
    {
        try
        {
            if (!objectManager.TryGetObjectWithLogging<RaidEventComponent>(data.ComponentId, out var component))
                return;

            var rewards = component._raidProductionRewards ?? new Dictionary<ItemObject, float>();
            rewards.Clear();

            var count = Math.Min(data.ItemIds?.Length ?? 0, data.Values?.Length ?? 0);
            for (int i = 0; i < count; i++)
            {
                if (!TryGetRewardItem(data.ItemIds[i], out var item))
                    continue;

                rewards[item] = data.Values[i];
            }

            using (new AllowedThread())
            {
                component._raidProductionRewards = rewards;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to apply raid production rewards update");
        }
    }

    private void ApplyLootedItems(NetworkRaidLootedItemsUpdated data)
    {
        try
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.PartyId, out var mobileParty))
                return;

            var lootedItems = new ItemRoster();
            var count = Math.Min(data.ItemIds?.Length ?? 0, data.Amounts?.Length ?? 0);
            for (int i = 0; i < count; i++)
            {
                if (data.Amounts[i] <= 0)
                    continue;

                if (!TryGetRewardItem(data.ItemIds[i], out var item))
                    continue;

                lootedItems.AddToCounts(new EquipmentElement(item), data.Amounts[i]);
            }

            if (lootedItems.Count == 0)
                return;

            CampaignEventDispatcher.Instance.OnItemsLooted(mobileParty, lootedItems);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to apply raid looted items update");
        }
    }

    private bool TryGetRewardItemId(ItemObject item, out string itemId)
    {
        if (objectManager.TryGetId(item, out itemId))
            return true;

        if (itemObjectRegistry.TryRegisterExistingItem(item, out itemId))
            return true;

        return objectManager.TryGetIdWithLogging(item, out itemId);
    }

    private bool TryGetRewardItem(string itemId, out ItemObject item)
    {
        if (objectManager.TryGetObject(itemId, out item))
            return true;

        if (itemObjectRegistry.TryGetRegisteredItem(itemId, out item))
            return true;

        return objectManager.TryGetObjectWithLogging(itemId, out item);
    }
}