using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.ViewData.Data;
using GameInterface.Services.ViewData.Interfaces;
using GameInterface.Services.ViewData.Messages;

namespace GameInterface.Services.ViewData.Handlers;

internal class ViewDataHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IViewDataTrackerInterface viewDataTrackerInterface;

    private ViewDataPlayerData viewDataPlayerData;
    private string loadedPlayerHeroId;

    public ViewDataHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IViewDataTrackerInterface viewDataTrackerInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.viewDataTrackerInterface = viewDataTrackerInterface;

        messageBroker.Subscribe<InitializeClientViewData>(Handle_InitializeClientViewData);
        messageBroker.Subscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
        messageBroker.Subscribe<ViewDataChanged>(Handle_ViewDataChanged);
        messageBroker.Subscribe<NetworkSaveViewData>(Handle_NetworkSaveViewData);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeClientViewData>(Handle_InitializeClientViewData);
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle_PlayerHeroChanged);
        messageBroker.Unsubscribe<ViewDataChanged>(Handle_ViewDataChanged);
        messageBroker.Unsubscribe<NetworkSaveViewData>(Handle_NetworkSaveViewData);
    }

    private void Handle_InitializeClientViewData(MessagePayload<InitializeClientViewData> obj)
    {
        viewDataPlayerData = obj.What.ViewDataPlayerData;
    }

    // Need to load the view data when the hero changes for the player
    private void Handle_PlayerHeroChanged(MessagePayload<PlayerHeroChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.NewHero, out string playerHeroId)) return;

        // Null check for players without existing view data
        PlayerViewData viewData = null;
        viewDataPlayerData?.PlayerViewData?.TryGetValue(playerHeroId, out viewData);

        // After a succession the join snapshot has nothing under the heir, and the tracker already holds this player's data
        if (viewData != null || loadedPlayerHeroId == null)
        {
            // Loading fires change events, and a snapshot of the half loaded tracker would overwrite the saved data
            loadedPlayerHeroId = null;
            viewDataTrackerInterface.LoadPlayerViewData(viewData);
        }

        loadedPlayerHeroId = playerHeroId;
    }

    private void Handle_ViewDataChanged(MessagePayload<ViewDataChanged> obj)
    {
        var mainHero = obj.What.MainHero;

        GameThread.RunSafe(() =>
        {
            // Changes before this player's data is loaded are the server save's, which would overwrite it
            if (!objectManager.TryGetId(mainHero, out var mainHeroId) || mainHeroId != loadedPlayerHeroId) return;

            network.SendAll(new NetworkSaveViewData(mainHeroId, viewDataTrackerInterface.CreatePlayerViewData()));
        });
    }

    private void Handle_NetworkSaveViewData(MessagePayload<NetworkSaveViewData> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;

            viewDataTrackerInterface.SetPlayerViewData(data.MainHeroId, data.ViewData ?? new PlayerViewData());
        });
    }
}
