using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Inventory.Interfaces;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;

namespace GameInterface.Services.Inventory.Handlers;

internal class InventoryStatesHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<InventoryStatesHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly ISessionInventoryPlayerDataInterface sessionInventoryPlayerDataInterface;

    public InventoryStatesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        ISessionInventoryPlayerDataInterface sessionInventoryPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.sessionInventoryPlayerDataInterface = sessionInventoryPlayerDataInterface;

        messageBroker.Subscribe<SaveItemLockStates>(Handle_SaveItemLockStates);
        messageBroker.Subscribe<NetworkSaveItemLockStates>(Handle_NetworkSaveItemLockStates);

        messageBroker.Subscribe<SaveItemSortStates>(Handle_SaveItemSortStates);
        messageBroker.Subscribe<NetworkSaveItemSortStates>(Handle_NetworkSaveItemSortStates);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SaveItemLockStates>(Handle_SaveItemLockStates);
        messageBroker.Unsubscribe<NetworkSaveItemLockStates>(Handle_NetworkSaveItemLockStates);

        messageBroker.Unsubscribe<SaveItemSortStates>(Handle_SaveItemSortStates);
        messageBroker.Unsubscribe<NetworkSaveItemSortStates>(Handle_NetworkSaveItemSortStates);
    }

    private void Handle_SaveItemLockStates(MessagePayload<SaveItemLockStates> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

            network.SendAll(new NetworkSaveItemLockStates(mainHeroId, obj.What.ItemLockIds));
        });
    }

    private void Handle_NetworkSaveItemLockStates(MessagePayload<NetworkSaveItemLockStates> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, obj.What.MainHeroId)) return;

            sessionInventoryPlayerDataInterface.SetInventoryLocks(obj.What.MainHeroId, obj.What.ItemLockIds);
        });
    }

    private void Handle_SaveItemSortStates(MessagePayload<SaveItemSortStates> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

            network.SendAll(new NetworkSaveItemSortStates(mainHeroId, obj.What.UsageType, obj.What.SortPreference));
        });
    }

    private void Handle_NetworkSaveItemSortStates(MessagePayload<NetworkSaveItemSortStates> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, obj.What.MainHeroId)) return;

            sessionInventoryPlayerDataInterface.SetSortPreference(obj.What.MainHeroId, obj.What.UsageType, obj.What.SortPreference);
        });
    }
}
