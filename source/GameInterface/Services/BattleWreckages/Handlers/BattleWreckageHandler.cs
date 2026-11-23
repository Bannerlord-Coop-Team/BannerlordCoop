using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.BattleWreckages.Interfaces;
using GameInterface.Services.BattleWreckages.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;

namespace GameInterface.Services.BattleWreckages.Handlers;

internal class BattleWreckageHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IBattleWreckageInterface battleWreckageInterface;

    public BattleWreckageHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IBattleWreckageInterface battleWreckageInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.battleWreckageInterface = battleWreckageInterface;

        messageBroker.Subscribe<RequestStartWreckageEncounter>(Handle_RequestStartWreckageEncounter);
        messageBroker.Subscribe<NetworkRequestStartWreckageEncounter>(Handle_NetworkRequestStartWreckageEncounter);
        messageBroker.Subscribe<WreckageEncounterStarted>(Handle_WreckageEncounterStarted);
        messageBroker.Subscribe<NetworkOpenWreckageEncounter>(Handle_NetworkOpenWreckageEncounter);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestStartWreckageEncounter>(Handle_RequestStartWreckageEncounter);
        messageBroker.Unsubscribe<NetworkRequestStartWreckageEncounter>(Handle_NetworkRequestStartWreckageEncounter);
        messageBroker.Unsubscribe<WreckageEncounterStarted>(Handle_WreckageEncounterStarted);
        messageBroker.Unsubscribe<NetworkOpenWreckageEncounter>(Handle_NetworkOpenWreckageEncounter);
    }

    private void Handle_RequestStartWreckageEncounter(MessagePayload<RequestStartWreckageEncounter> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.BattleWreckage, out var battleWreckageId)) return;
        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;

        network.SendAll(new NetworkRequestStartWreckageEncounter(battleWreckageId, mainHeroId));
    }

    private void Handle_NetworkRequestStartWreckageEncounter(MessagePayload<NetworkRequestStartWreckageEncounter> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;
            if (!objectManager.TryGetObjectWithLogging<BattleWreckage>(data.BattleWreckageId, out var battleWreckage)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;

            battleWreckageInterface.StartWreckageEncounter(battleWreckage, mainHero);
        });
    }

    private void Handle_WreckageEncounterStarted(MessagePayload<WreckageEncounterStarted> obj)
    {
        var data = obj.What;

        if (!playerManager.TryGetPlayer(data.MainHero, out var player)) return;
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;
        if (!objectManager.TryGetIdWithLogging(data.BattleWreckage, out var battleWreckageId)) return;

        network.Send(peer, new NetworkOpenWreckageEncounter(battleWreckageId));
    }

    private void Handle_NetworkOpenWreckageEncounter(MessagePayload<NetworkOpenWreckageEncounter> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<BattleWreckage>(data.BattleWreckageId, out var battleWreckage)) return;

            battleWreckageInterface.OpenWreckageEncounter(battleWreckage);
        });
    }
}
