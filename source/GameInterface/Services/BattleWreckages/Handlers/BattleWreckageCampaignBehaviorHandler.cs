using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.BattleWreckages.Interfaces;
using GameInterface.Services.BattleWreckages.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Interfaces;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.Core;

namespace GameInterface.Services.BattleWreckages.Handlers;

internal class BattleWreckageCampaignBehaviorHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IBattleWreckageCampaignBehaviorInterface battleWreckageCampaignBehaviorInterface;
    private readonly ITroopRosterInterface troopRosterInterface;

    public BattleWreckageCampaignBehaviorHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IBattleWreckageCampaignBehaviorInterface battleWreckageCampaignBehaviorInterface,
        ITroopRosterInterface troopRosterInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.battleWreckageCampaignBehaviorInterface = battleWreckageCampaignBehaviorInterface;
        this.troopRosterInterface = troopRosterInterface;

        messageBroker.Subscribe<RequestStartWreckageEncounter>(Handle_RequestStartWreckageEncounter);
        messageBroker.Subscribe<NetworkRequestStartWreckageEncounter>(Handle_NetworkRequestStartWreckageEncounter);
        messageBroker.Subscribe<WreckageEncounterStarted>(Handle_WreckageEncounterStarted);
        messageBroker.Subscribe<NetworkOpenWreckageEncounter>(Handle_NetworkOpenWreckageEncounter);
        messageBroker.Subscribe<WreckageEncounterDenied>(Handle_WreckageEncounterDenied);
        messageBroker.Subscribe<NetworkDenyWreckageEncounter>(Handle_NetworkDenyWreckageEncounter);

        messageBroker.Subscribe<RequestLeaveWreckageEncounter>(Handle_RequestLeaveWreckageEncounter);
        messageBroker.Subscribe<NetworkRequestLeaveWreckageEncounter>(Handle_NetworkRequestLeaveWreckageEncounter);
        messageBroker.Subscribe<WreckageEncounterLeft>(Handle_WreckageEncounterLeft);
        messageBroker.Subscribe<NetworkCloseWreckageEncounter>(Handle_NetworkCloseWreckageEncounter);

        messageBroker.Subscribe<WreckageInvestigationResultsApplied>(Handle_WreckageInvestigationResultsApplied);
        messageBroker.Subscribe<NetworkShowWreckageInvestigationResults>(Handle_NetworkShowWreckageInvestigationResults);

        messageBroker.Subscribe<RequestSelectWreckageConsequences>(Handle_RequestSelectWreckageConsequences);
        messageBroker.Subscribe<NetworkRequestSelectWreckageConsequences>(Handle_NetworkRequestSelectWreckageConsequences);
        messageBroker.Subscribe<RecoverTroopsDecisionRequired>(Handle_RecoverTroopsDecisionRequired);
        messageBroker.Subscribe<NetworkShowRecoverTroopsDecision>(Handle_NetworkShowRecoverTroopsDecision);

        messageBroker.Subscribe<RequestAcceptRecoverTroops>(Handle_RequestAcceptRecoverTroops);
        messageBroker.Subscribe<NetworkRequestAcceptRecoverTroops>(Handle_NetworkRequestAcceptRecoverTroops);

        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestStartWreckageEncounter>(Handle_RequestStartWreckageEncounter);
        messageBroker.Unsubscribe<NetworkRequestStartWreckageEncounter>(Handle_NetworkRequestStartWreckageEncounter);
        messageBroker.Unsubscribe<WreckageEncounterStarted>(Handle_WreckageEncounterStarted);
        messageBroker.Unsubscribe<NetworkOpenWreckageEncounter>(Handle_NetworkOpenWreckageEncounter);
        messageBroker.Unsubscribe<WreckageEncounterDenied>(Handle_WreckageEncounterDenied);
        messageBroker.Unsubscribe<NetworkDenyWreckageEncounter>(Handle_NetworkDenyWreckageEncounter);

        messageBroker.Unsubscribe<RequestLeaveWreckageEncounter>(Handle_RequestLeaveWreckageEncounter);
        messageBroker.Unsubscribe<NetworkRequestLeaveWreckageEncounter>(Handle_NetworkRequestLeaveWreckageEncounter);
        messageBroker.Unsubscribe<WreckageEncounterLeft>(Handle_WreckageEncounterLeft);
        messageBroker.Unsubscribe<NetworkCloseWreckageEncounter>(Handle_NetworkCloseWreckageEncounter);

        messageBroker.Unsubscribe<WreckageInvestigationResultsApplied>(Handle_WreckageInvestigationResultsApplied);
        messageBroker.Unsubscribe<NetworkShowWreckageInvestigationResults>(Handle_NetworkShowWreckageInvestigationResults);

        messageBroker.Unsubscribe<RequestSelectWreckageConsequences>(Handle_RequestSelectWreckageConsequences);
        messageBroker.Unsubscribe<NetworkRequestSelectWreckageConsequences>(Handle_NetworkRequestSelectWreckageConsequences);
        messageBroker.Unsubscribe<RecoverTroopsDecisionRequired>(Handle_RecoverTroopsDecisionRequired);
        messageBroker.Unsubscribe<NetworkShowRecoverTroopsDecision>(Handle_NetworkShowRecoverTroopsDecision);

        messageBroker.Unsubscribe<RequestAcceptRecoverTroops>(Handle_RequestAcceptRecoverTroops);
        messageBroker.Unsubscribe<NetworkRequestAcceptRecoverTroops>(Handle_NetworkRequestAcceptRecoverTroops);

        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
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

        if (!playerManager.TryGetPeer(data.MainHero, out var peer)) return;
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
        if (!playerManager.TryGetPeer(obj.What.MainHero, out var peer)) return;

        network.Send(peer, new NetworkDenyWreckageEncounter());
    }

    private void Handle_NetworkDenyWreckageEncounter(MessagePayload<NetworkDenyWreckageEncounter> obj)
    {
        GameThread.RunSafe(() =>
        {
            battleWreckageCampaignBehaviorInterface.ShowWreckageEncounterDenied();
        });
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

        if (!playerManager.TryGetPeer(data.MainHero, out var peer)) return;

        network.Send(peer, new NetworkCloseWreckageEncounter(data.Reason, data.RequiredInvestigationDuration));
    }

    private void Handle_NetworkCloseWreckageEncounter(MessagePayload<NetworkCloseWreckageEncounter> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            battleWreckageCampaignBehaviorInterface.CloseWreckageEncounter(data.Reason, data.RequiredInvestigationDuration);
        });
    }

    private void Handle_WreckageInvestigationResultsApplied(MessagePayload<WreckageInvestigationResultsApplied> obj)
    {
        var data = obj.What;

        if (!playerManager.TryGetPeer(data.MainHero, out var peer)) return;

        network.Send(peer, new NetworkShowWreckageInvestigationResults(
            troopRosterInterface.PackTroopRosterData(data.LootedTroops),
            data.LootedItems.ToArray(),
            data.ConsequenceExplanations,
            data.LootedGoldAmount));
    }

    private void Handle_NetworkShowWreckageInvestigationResults(MessagePayload<NetworkShowWreckageInvestigationResults> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            battleWreckageCampaignBehaviorInterface.ShowWreckageInvestigationResults(
                data.LootedTroops,
                data.LootedItems ?? Array.Empty<ItemRosterElement>(), // An empty item array arrives as null
                data.ConsequenceExplanations,
                data.LootedGoldAmount);
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

        if (!playerManager.TryGetPeer(data.MainHero, out var peer)) return;

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
            battleWreckageCampaignBehaviorInterface.InterruptDisconnectedPlayers();
        });
    }
}
