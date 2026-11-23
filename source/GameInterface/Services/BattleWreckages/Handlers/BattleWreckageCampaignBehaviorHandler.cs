using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.BattleWreckages.Interfaces;
using GameInterface.Services.BattleWreckages.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BattleWreckages.Handlers;

internal class BattleWreckageCampaignBehaviorHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IBattleWreckageCampaignBehaviorInterface battleWreckageCampaignBehaviorInterface;

    public BattleWreckageCampaignBehaviorHandler(
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

        messageBroker.Subscribe<RequestLeaveWreckageEncounter>(Handle_RequestLeaveWreckageEncounter);
        messageBroker.Subscribe<NetworkRequestLeaveWreckageEncounter>(Handle_NetworkRequestLeaveWreckageEncounter);
        messageBroker.Subscribe<WreckageEncounterLeft>(Handle_WreckageEncounterLeft);
        messageBroker.Subscribe<NetworkCloseWreckageEncounter>(Handle_NetworkCloseWreckageEncounter);

        messageBroker.Subscribe<RequestApplyWreckageInvestigationResults>(Handle_RequestApplyWreckageInvestigationResults);
        messageBroker.Subscribe<NetworkRequestApplyWreckageInvestigationResults>(Handle_NetworkRequestApplyWreckageInvestigationResults);
        messageBroker.Subscribe<WreckageInvestigationResultsApplied>(Handle_WreckageInvestigationResultsApplied);
        messageBroker.Subscribe<NetworkShowWreckageInvestigationResults>(Handle_NetworkShowWreckageInvestigationResults);

        messageBroker.Subscribe<RequestSelectWreckageConsequences>(Handle_RequestSelectWreckageConsequences);
        messageBroker.Subscribe<NetworkRequestSelectWreckageConsequences>(Handle_NetworkRequestSelectWreckageConsequences);
        messageBroker.Subscribe<RecoverTroopsDecisionRequired>(Handle_RecoverTroopsDecisionRequired);
        messageBroker.Subscribe<NetworkShowRecoverTroopsDecision>(Handle_NetworkShowRecoverTroopsDecision);
        messageBroker.Subscribe<WreckageConsequencesSelected>(Handle_WreckageConsequencesSelected);
        messageBroker.Subscribe<NetworkShowWreckageResultsConnectionMenu>(Handle_NetworkShowWreckageResultsConnectionMenu);

        messageBroker.Subscribe<RequestAcceptRecoverTroops>(Handle_RequestAcceptRecoverTroops);
        messageBroker.Subscribe<NetworkRequestAcceptRecoverTroops>(Handle_NetworkRequestAcceptRecoverTroops);

        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestLeaveWreckageEncounter>(Handle_RequestLeaveWreckageEncounter);
        messageBroker.Unsubscribe<NetworkRequestLeaveWreckageEncounter>(Handle_NetworkRequestLeaveWreckageEncounter);
        messageBroker.Unsubscribe<WreckageEncounterLeft>(Handle_WreckageEncounterLeft);
        messageBroker.Unsubscribe<NetworkCloseWreckageEncounter>(Handle_NetworkCloseWreckageEncounter);

        messageBroker.Unsubscribe<RequestApplyWreckageInvestigationResults>(Handle_RequestApplyWreckageInvestigationResults);
        messageBroker.Unsubscribe<NetworkRequestApplyWreckageInvestigationResults>(Handle_NetworkRequestApplyWreckageInvestigationResults);
        messageBroker.Unsubscribe<WreckageInvestigationResultsApplied>(Handle_WreckageInvestigationResultsApplied);
        messageBroker.Unsubscribe<NetworkShowWreckageInvestigationResults>(Handle_NetworkShowWreckageInvestigationResults);

        messageBroker.Unsubscribe<RequestSelectWreckageConsequences>(Handle_RequestSelectWreckageConsequences);
        messageBroker.Unsubscribe<NetworkRequestSelectWreckageConsequences>(Handle_NetworkRequestSelectWreckageConsequences);
        messageBroker.Unsubscribe<RecoverTroopsDecisionRequired>(Handle_RecoverTroopsDecisionRequired);
        messageBroker.Unsubscribe<NetworkShowRecoverTroopsDecision>(Handle_NetworkShowRecoverTroopsDecision);
        messageBroker.Unsubscribe<WreckageConsequencesSelected>(Handle_WreckageConsequencesSelected);
        messageBroker.Unsubscribe<NetworkShowWreckageResultsConnectionMenu>(Handle_NetworkShowWreckageResultsConnectionMenu);

        messageBroker.Unsubscribe<RequestAcceptRecoverTroops>(Handle_RequestAcceptRecoverTroops);
        messageBroker.Unsubscribe<NetworkRequestAcceptRecoverTroops>(Handle_NetworkRequestAcceptRecoverTroops);

        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    private void Handle_RequestLeaveWreckageEncounter(MessagePayload<RequestLeaveWreckageEncounter> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;

        var message = new NetworkRequestLeaveWreckageEncounter(mainHeroId, data.Reason);
        network.SendAll(message);
    }

    private void Handle_NetworkRequestLeaveWreckageEncounter(MessagePayload<NetworkRequestLeaveWreckageEncounter> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;

            battleWreckageCampaignBehaviorInterface.LeaveWreckageEncounter(mainHero, data.Reason);
        });
    }

    private void Handle_WreckageEncounterLeft(MessagePayload<WreckageEncounterLeft> obj)
    {
        var data = obj.What;

        if (!playerManager.TryGetPlayer(data.MainHero, out var player)) return;
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;

        network.Send(peer, new NetworkCloseWreckageEncounter(data.Reason));
    }

    private void Handle_NetworkCloseWreckageEncounter(MessagePayload<NetworkCloseWreckageEncounter> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            battleWreckageCampaignBehaviorInterface.CloseWreckageEncounter(data.Reason);
        });
    }

    private void Handle_RequestApplyWreckageInvestigationResults(MessagePayload<RequestApplyWreckageInvestigationResults> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        network.SendAll(new NetworkRequestApplyWreckageInvestigationResults(mainHeroId));
    }

    private void Handle_NetworkRequestApplyWreckageInvestigationResults(MessagePayload<NetworkRequestApplyWreckageInvestigationResults> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;

            battleWreckageCampaignBehaviorInterface.ApplyWreckageInvestigationResults(mainHero);
        });
    }

    private void Handle_WreckageInvestigationResultsApplied(MessagePayload<WreckageInvestigationResultsApplied> obj)
    {
        if (!playerManager.TryGetPlayer(obj.What.MainHero, out var player)) return;
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;

        network.Send(peer, new NetworkShowWreckageInvestigationResults());
    }

    private void Handle_NetworkShowWreckageInvestigationResults(MessagePayload<NetworkShowWreckageInvestigationResults> obj)
    {
        GameThread.RunSafe(() =>
        {
            battleWreckageCampaignBehaviorInterface.ShowWreckageInvestigationResults();
        });
    }

    private void Handle_RequestSelectWreckageConsequences(MessagePayload<RequestSelectWreckageConsequences> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        network.SendAll(new NetworkRequestSelectWreckageConsequences(mainHeroId));
    }

    private void Handle_NetworkRequestSelectWreckageConsequences(MessagePayload<NetworkRequestSelectWreckageConsequences> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;

            battleWreckageCampaignBehaviorInterface.SelectWreckageConsequences(mainHero);
        });
    }

    private void Handle_RecoverTroopsDecisionRequired(MessagePayload<RecoverTroopsDecisionRequired> obj)
    {
        var data = obj.What;

        if (!playerManager.TryGetPlayer(data.MainHero, out var player)) return;
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;

        network.Send(peer, new NetworkShowRecoverTroopsDecision(data.RecoverTroopCount, data.RequiredInvestigationDuration));
    }

    private void Handle_NetworkShowRecoverTroopsDecision(MessagePayload<NetworkShowRecoverTroopsDecision> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            battleWreckageCampaignBehaviorInterface.ShowRecoverTroopsDecision(data.RecoverTroopCount, data.RequiredInvestigationDuration);
        });
    }

    private void Handle_WreckageConsequencesSelected(MessagePayload<WreckageConsequencesSelected> obj)
    {
        if (!playerManager.TryGetPlayer(obj.What.MainHero, out var player)) return;
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;

        network.Send(peer, new NetworkShowWreckageResultsConnectionMenu());
    }

    private void Handle_NetworkShowWreckageResultsConnectionMenu(MessagePayload<NetworkShowWreckageResultsConnectionMenu> obj)
    {
        GameThread.RunSafe(() =>
        {
            battleWreckageCampaignBehaviorInterface.ShowWreckageResultsConnectionMenu();
        });
    }

    private void Handle_RequestAcceptRecoverTroops(MessagePayload<RequestAcceptRecoverTroops> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        network.SendAll(new NetworkRequestAcceptRecoverTroops(mainHeroId));
    }

    private void Handle_NetworkRequestAcceptRecoverTroops(MessagePayload<NetworkRequestAcceptRecoverTroops> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.IsSenderControllingHero(obj.Who, data.MainHeroId)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;

            battleWreckageCampaignBehaviorInterface.AcceptRecoverTroops(mainHero);
        });
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> obj)
    {
        GameThread.RunSafe(() =>
        {
            battleWreckageCampaignBehaviorInterface.RemoveDisconnectedPlayers();
        });
    }
}
