using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEventComponents.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MapEventComponents.Handlers;

internal class RaidProductionRewardsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<RaidProductionRewardsHandler>();

    private const string ItemIdPrefix = nameof(ItemObject) + "_";

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public RaidProductionRewardsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

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

        var settlement = component.MapEvent?.MapEventSettlement;
        network.SendAll(new NetworkRaidProductionRewardsUpdated(
            componentId,
            itemIds.ToArray(),
            values.ToArray(),
            component.MapEvent?.WasEverInLootingPhase == true,
            component.RaidDamage,
            settlement != null,
            settlement?.SettlementHitPoints ?? 0f,
            settlement?.Village?.Hearth ?? 0f));
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
                component.RaidDamage = data.RaidDamage;
                if (data.HasSettlementState && component.MapEvent?.MapEventSettlement != null)
                {
                    var settlement = component.MapEvent.MapEventSettlement;
                    settlement.SettlementHitPoints = data.SettlementHitPoints;
                    if (settlement.Village != null)
                        settlement.Village.Hearth = data.VillageHearth;
                }
                if (data.WasEverInLootingPhase && component.MapEvent != null)
                    component.MapEvent.WasEverInLootingPhase = true;
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
        itemId = null;

        if (item == null)
            return false;

        if (objectManager.TryGetId(item, out itemId))
            return true;

        if (TryRegisterRewardItem(item, out itemId))
            return true;

        return objectManager.TryGetIdWithLogging(item, out itemId);
    }

    private bool TryGetRewardItem(string itemId, out ItemObject item)
    {
        if (objectManager.TryGetObject(itemId, out item))
            return true;

        if (TryGetRegisteredRewardItem(itemId, out item))
            return true;

        return objectManager.TryGetObjectWithLogging(itemId, out item);
    }

    private bool TryRegisterRewardItem(ItemObject item, out string itemId)
    {
        itemId = null;

        if (item == null || string.IsNullOrEmpty(item.StringId))
            return false;

        itemId = ItemIdPrefix + item.StringId;
        if (objectManager.Contains(itemId))
        {
            if (objectManager.TryGetObject<ItemObject>(itemId, out var registeredItem) &&
                registeredItem != item &&
                registeredItem.StringId == item.StringId)
            {
                objectManager.Remove(registeredItem);
                if (objectManager.AddExisting(itemId, item))
                    return objectManager.TryGetId(item, out itemId);
            }

            return objectManager.TryGetId(item, out itemId);
        }

        if (objectManager.AddExisting(itemId, item) == false)
            return false;

        return objectManager.TryGetId(item, out itemId);
    }

    private bool TryGetRegisteredRewardItem(string itemId, out ItemObject item)
    {
        var stringId = GetRewardItemStringId(itemId);
        if (string.IsNullOrEmpty(stringId))
        {
            item = null;
            return false;
        }

        var mbObjectManager = MBObjectManager.Instance;
        if (mbObjectManager == null)
        {
            item = null;
            return false;
        }

        item = mbObjectManager.GetObject<ItemObject>(stringId) ??
               mbObjectManager.GetObjectTypeList<ItemObject>().FirstOrDefault(i => i.StringId == stringId);
        if (item == null)
            return false;

        if (TryRegisterRewardItem(item, out var registeredItemId) == false)
            return false;

        if (objectManager.TryGetObject(itemId, out item))
            return true;

        return objectManager.TryGetObject(registeredItemId, out item);
    }

    private static string GetRewardItemStringId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return null;

        return itemId.StartsWith(ItemIdPrefix, StringComparison.Ordinal)
            ? itemId.Substring(ItemIdPrefix.Length)
            : itemId;
    }
}