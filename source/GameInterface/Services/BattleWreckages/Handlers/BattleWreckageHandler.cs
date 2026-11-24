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
    private readonly IBattleWreckageCampaignBehaviorInterface battleWreckageCampaignBehaviorInterface;

    public BattleWreckageHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IBattleWreckageCampaignBehaviorInterface battleWreckageCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.battleWreckageCampaignBehaviorInterface = battleWreckageCampaignBehaviorInterface;

        messageBroker.Subscribe<RequestStartWreckageEncounter>(Handle_RequestStartWreckageEncounter);
        messageBroker.Subscribe<NetworkRequestStartWreckageEncounter>(Handle_NetworkRequestStartWreckageEncounter);
        messageBroker.Subscribe<WreckageEncounterStarted>(Handle_WreckageEncounterStarted);
        messageBroker.Subscribe<NetworkOpenWreckageEncounter>(Handle_NetworkOpenWreckageEncounter);
        messageBroker.Subscribe<WreckageEncounterDenied>(Handle_WreckageEncounterDenied);
        messageBroker.Subscribe<NetworkDenyWreckageEncounter>(Handle_NetworkDenyWreckageEncounter);

        messageBroker.Subscribe<BattleWreckageAdded>(Handle_BattleWreckageAdded);
        messageBroker.Subscribe<NetworkAddBattleWreckage>(Handle_NetworkAddBattleWreckage);
        messageBroker.Subscribe<BattleWreckageRemoved>(Handle_BattleWreckageRemoved);
        messageBroker.Subscribe<NetworkRemoveBattleWreckage>(Handle_NetworkRemoveBattleWreckage);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestStartWreckageEncounter>(Handle_RequestStartWreckageEncounter);
        messageBroker.Unsubscribe<NetworkRequestStartWreckageEncounter>(Handle_NetworkRequestStartWreckageEncounter);
        messageBroker.Unsubscribe<WreckageEncounterStarted>(Handle_WreckageEncounterStarted);
        messageBroker.Unsubscribe<NetworkOpenWreckageEncounter>(Handle_NetworkOpenWreckageEncounter);
        messageBroker.Unsubscribe<WreckageEncounterDenied>(Handle_WreckageEncounterDenied);
        messageBroker.Unsubscribe<NetworkDenyWreckageEncounter>(Handle_NetworkDenyWreckageEncounter);

        messageBroker.Unsubscribe<BattleWreckageAdded>(Handle_BattleWreckageAdded);
        messageBroker.Unsubscribe<NetworkAddBattleWreckage>(Handle_NetworkAddBattleWreckage);
        messageBroker.Unsubscribe<BattleWreckageRemoved>(Handle_BattleWreckageRemoved);
        messageBroker.Unsubscribe<NetworkRemoveBattleWreckage>(Handle_NetworkRemoveBattleWreckage);
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

            battleWreckageCampaignBehaviorInterface.StartWreckageEncounter(battleWreckage, mainHero);
        });
    }

    private void Handle_WreckageEncounterStarted(MessagePayload<WreckageEncounterStarted> obj)
    {
        var data = obj.What;

        if (!playerManager.TryGetPlayer(data.MainHero, out var player)) return;
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;
        if (!objectManager.TryGetIdWithLogging(data.BattleWreckage, out var battleWreckageId)) return;

        network.Send(peer, new NetworkOpenWreckageEncounter(battleWreckageId, data.IsInterrupted, data.RequiredInvestigationDuration));
    }

    private void Handle_NetworkOpenWreckageEncounter(MessagePayload<NetworkOpenWreckageEncounter> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<BattleWreckage>(data.BattleWreckageId, out var battleWreckage)) return;

            battleWreckageCampaignBehaviorInterface.OpenWreckageEncounter(battleWreckage, data.IsInterrupted, data.RequiredInvestigationDuration);
        });
    }

    private void Handle_WreckageEncounterDenied(MessagePayload<WreckageEncounterDenied> obj)
    {
        if (!playerManager.TryGetPlayer(obj.What.MainHero, out var player)) return;
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;

        network.Send(peer, new NetworkDenyWreckageEncounter());
    }

    private void Handle_NetworkDenyWreckageEncounter(MessagePayload<NetworkDenyWreckageEncounter> obj)
    {
        GameThread.RunSafe(() =>
        {
            battleWreckageCampaignBehaviorInterface.ShowWreckageEncounterDenied();
        });
    }

    private void Handle_BattleWreckageAdded(MessagePayload<BattleWreckageAdded> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.BattleWreckage, out var battleWreckageId)) return;

        network.SendAll(new NetworkAddBattleWreckage(battleWreckageId));
    }

    private void Handle_NetworkAddBattleWreckage(MessagePayload<NetworkAddBattleWreckage> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<BattleWreckage>(data.BattleWreckageId, out var battleWreckage)) return;

            battleWreckageCampaignBehaviorInterface.AddWreckageToMap(battleWreckage);
        });
    }

    private void Handle_BattleWreckageRemoved(MessagePayload<BattleWreckageRemoved> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.BattleWreckage, out var battleWreckageId)) return;

        network.SendAll(new NetworkRemoveBattleWreckage(battleWreckageId));
    }

    private void Handle_NetworkRemoveBattleWreckage(MessagePayload<NetworkRemoveBattleWreckage> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<BattleWreckage>(data.BattleWreckageId, out var battleWreckage)) return;

            battleWreckageCampaignBehaviorInterface.RemoveWreckageFromMap(battleWreckage);
        });
    }
}
