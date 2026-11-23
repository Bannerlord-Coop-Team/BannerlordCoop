using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.OrderOfBattle.Data;
using GameInterface.Services.OrderOfBattle.Interfaces;
using GameInterface.Services.OrderOfBattle.Messages;
using GameInterface.Services.Players;
using System;

namespace GameInterface.Services.OrderOfBattle.Handlers;

internal class OrderOfBattleHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IOrderOfBattleCampaignBehaviorInterface orderOfBattleInterface;

    public OrderOfBattleHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IOrderOfBattleCampaignBehaviorInterface orderOfBattleInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.orderOfBattleInterface = orderOfBattleInterface;

        messageBroker.Subscribe<OrderOfBattleSaved>(Handle_OrderOfBattleSaved);
        messageBroker.Subscribe<NetworkSaveOrderOfBattle>(Handle_NetworkSaveOrderOfBattle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<OrderOfBattleSaved>(Handle_OrderOfBattleSaved);
        messageBroker.Unsubscribe<NetworkSaveOrderOfBattle>(Handle_NetworkSaveOrderOfBattle);
    }

    private void Handle_OrderOfBattleSaved(MessagePayload<OrderOfBattleSaved> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;

            var formations = orderOfBattleInterface.CreateFormations(data.FormationInfos);

            network.SendAll(new NetworkSaveOrderOfBattle(mainHeroId, formations, data.IsSiegeBattle, data.IsInArmy));
        });
    }

    private void Handle_NetworkSaveOrderOfBattle(MessagePayload<NetworkSaveOrderOfBattle> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;

            var formations = data.Formations ?? Array.Empty<OrderOfBattleFormation>();

            orderOfBattleInterface.SetPlayerFormations(data.MainHeroId, formations, data.IsSiegeBattle, data.IsInArmy);
        });
    }
}
