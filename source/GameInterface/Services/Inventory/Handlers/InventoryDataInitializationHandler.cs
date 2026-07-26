using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Inventory.Interfaces;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Inventory.Handlers;

internal class InventoryDataInitializationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<InventoryDataInitializationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionInventoryPlayerDataInterface sessionInventoryPlayerDataInterface;

    private InventoryPlayerData inventoryPlayerData;

    public InventoryDataInitializationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionInventoryPlayerDataInterface sessionInventoryPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionInventoryPlayerDataInterface = sessionInventoryPlayerDataInterface;

        messageBroker.Subscribe<InitializeClientInventoryData>(Handle);
        messageBroker.Subscribe<PlayerHeroChanged>(Handle);
        messageBroker.Subscribe<NetworkInitializeServerInventoryDataKeys>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeClientInventoryData>(Handle);
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle);
        messageBroker.Unsubscribe<NetworkInitializeServerInventoryDataKeys>(Handle);
    }

    private void Handle(MessagePayload<InitializeClientInventoryData> obj)
    {
        inventoryPlayerData = obj.What.InventoryPlayerData;
    }

    // Need to load inventory data when the hero changes for the player
    private void Handle(MessagePayload<PlayerHeroChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.NewHero, out string playerHeroId)) return;

        ViewDataTrackerCampaignBehavior viewDataTrackerCampaignBehavior = Campaign.Current.GetCampaignBehavior<ViewDataTrackerCampaignBehavior>();

        viewDataTrackerCampaignBehavior._inventoryItemLocks = GetInventoryItemLocks(playerHeroId);
        viewDataTrackerCampaignBehavior._inventorySortPreferences = GetInventorySortPreferences(playerHeroId);

        network.SendAll(new NetworkInitializeServerInventoryDataKeys(playerHeroId));
    }

    private void Handle(MessagePayload<NetworkInitializeServerInventoryDataKeys> obj)
    {
        sessionInventoryPlayerDataInterface.AddPlayerKeys(obj.What.PlayerHeroId);
    }

    private List<string> GetInventoryItemLocks(string playerHeroId)
    {
        var inventoryItemLocks = new List<string>();

        // Null and key check for players without existing inventory data
        if (inventoryPlayerData?.PlayerInventoryLocks?.ContainsKey(playerHeroId) != true) return inventoryItemLocks;

        return inventoryPlayerData.PlayerInventoryLocks[playerHeroId];
    }

    private Dictionary<int, Tuple<int, int>> GetInventorySortPreferences(string playerHeroId)
    {
        var inventorySortPreferences = new Dictionary<int, Tuple<int, int>>();

        // Null and key check for players without existing inventory data
        if (inventoryPlayerData?.PlayerInventorySortPreferences?.ContainsKey(playerHeroId) != true) return inventorySortPreferences;

        return inventoryPlayerData.PlayerInventorySortPreferences[playerHeroId];
    }
}
