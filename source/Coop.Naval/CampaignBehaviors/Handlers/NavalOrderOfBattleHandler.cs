using Common;
using Common.Messaging;
using Common.Network;
using Coop.Naval.CampaignBehaviors.Interfaces;
using Coop.Naval.CampaignBehaviors.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.OrderOfBattle.Data;
using GameInterface.Services.Players;
using System;

namespace Coop.Naval.CampaignBehaviors.Handlers;

internal class NavalOrderOfBattleHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly INavalOrderOfBattleCampaignBehaviorInterface navalOrderOfBattleInterface;

    public NavalOrderOfBattleHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        INavalOrderOfBattleCampaignBehaviorInterface navalOrderOfBattleInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.navalOrderOfBattleInterface = navalOrderOfBattleInterface;

        messageBroker.Subscribe<NavalOrderOfBattleSaved>(Handle_NavalOrderOfBattleSaved);
        messageBroker.Subscribe<NetworkSaveNavalOrderOfBattle>(Handle_NetworkSaveNavalOrderOfBattle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NavalOrderOfBattleSaved>(Handle_NavalOrderOfBattleSaved);
        messageBroker.Unsubscribe<NetworkSaveNavalOrderOfBattle>(Handle_NetworkSaveNavalOrderOfBattle);
    }

    private void Handle_NavalOrderOfBattleSaved(MessagePayload<NavalOrderOfBattleSaved> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;

            var formations = navalOrderOfBattleInterface.CreateFormations(data.FormationInfos);

            network.SendAll(new NetworkSaveNavalOrderOfBattle(mainHeroId, formations, data.IsInArmy));
        });
    }

    private void Handle_NetworkSaveNavalOrderOfBattle(MessagePayload<NetworkSaveNavalOrderOfBattle> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;

            var formations = data.Formations ?? Array.Empty<NavalOrderOfBattleFormation>();

            navalOrderOfBattleInterface.SetPlayerFormations(data.MainHeroId, formations, data.IsInArmy);
        });
    }
}
