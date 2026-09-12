using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Handlers;

internal class PatrolInteractionsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PatrolInteractionsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface;

    public PatrolInteractionsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionInteractionsPlayerDataInterface = sessionInteractionsPlayerDataInterface;

        messageBroker.Subscribe<AddPatrolPartyInteraction>(Handle_AddPatrolPartyInteraction);
        messageBroker.Subscribe<NetworkAddPatrolPartyInteraction>(Handle_NetworkAddPatrolPartyInteraction);

        messageBroker.Subscribe<PatrolPartyHostileAction>(Handle_PatrolPartyHostileAction);
        messageBroker.Subscribe<NetworkPatrolPartyHostileAction>(Handle_NetworkPatrolPartyHostileAction);

        messageBroker.Subscribe<MobilePartyDestroyed>(Handle_MobilePartyDestroyed);
        messageBroker.Subscribe<NetworkPatrolPartyDestroyed>(Handle_NetworkPatrolPartyDestroyed);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AddPatrolPartyInteraction>(Handle_AddPatrolPartyInteraction);
        messageBroker.Unsubscribe<NetworkAddPatrolPartyInteraction>(Handle_NetworkAddPatrolPartyInteraction);

        messageBroker.Unsubscribe<PatrolPartyHostileAction>(Handle_PatrolPartyHostileAction);
        messageBroker.Unsubscribe<NetworkPatrolPartyHostileAction>(Handle_NetworkPatrolPartyHostileAction);

        messageBroker.Unsubscribe<MobilePartyDestroyed>(Handle_MobilePartyDestroyed);
        messageBroker.Unsubscribe<NetworkPatrolPartyDestroyed>(Handle_NetworkPatrolPartyDestroyed);
    }

    private void Handle_AddPatrolPartyInteraction(MessagePayload<AddPatrolPartyInteraction> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.PartyHomeSettlement, out var partyHomeSettlementId)) return;

        var message = new NetworkAddPatrolPartyInteraction(mainHeroId, partyHomeSettlementId, data.CampaignTime._numTicks);
        network.SendAll(message);
    }

    private void Handle_NetworkAddPatrolPartyInteraction(MessagePayload<NetworkAddPatrolPartyInteraction> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            // Validate ids before adding to CoopSession
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var _)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.PartyHomeSettlementId, out var _)) return;

            sessionInteractionsPlayerDataInterface.SetPlayerPatrolInteraction(data.MainHeroId, data.PartyHomeSettlementId, new CampaignTime(data.CampaignTimeNumTicks));
        });
    }

    private void Handle_PatrolPartyHostileAction(MessagePayload<PatrolPartyHostileAction> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainParty, out var mainPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(data.ConversationParty, out var conversationPartyId)) return;

        var message = new NetworkPatrolPartyHostileAction(mainPartyId, conversationPartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkPatrolPartyHostileAction(MessagePayload<NetworkPatrolPartyHostileAction> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.MainPartyId, out var mainParty)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.ConversationPartyId, out var conversationParty)) return;

            BeHostileAction.ApplyEncounterHostileAction(mainParty, conversationParty);
        });
    }

    private void Handle_MobilePartyDestroyed(MessagePayload<MobilePartyDestroyed> obj)
    {
        var data = obj.What;
        
        // Don't process anything for destroyed mobile parties that aren't patrols or naval like vanilla
        if (!data.MobileParty.IsPatrolParty || data.MobileParty.PatrolPartyComponent.IsNaval) return;

        if (!objectManager.TryGetIdWithLogging(data.MobileParty.HomeSettlement, out var patrolHomeSettlementId)) return;

        // Update CoopSession data on server
        sessionInteractionsPlayerDataInterface.RemoveInteractedPatrolForAllPlayers(patrolHomeSettlementId);

        var message = new NetworkPatrolPartyDestroyed(patrolHomeSettlementId);
        network.SendAll(message);
    }

    private void Handle_NetworkPatrolPartyDestroyed(MessagePayload<NetworkPatrolPartyDestroyed> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetPatrolPartiesBehavior(out var patrolPartiesBehavior)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.PatrolHomeSettlementId, out var patrolHomeSettlement)) return;

            patrolPartiesBehavior._interactedPatrolParties.Remove(patrolHomeSettlement);
        });
    }

    private bool TryGetPatrolPartiesBehavior(out PatrolPartiesCampaignBehavior patrolPartiesBehavior)
    {
        patrolPartiesBehavior = Campaign.Current?.GetCampaignBehavior<PatrolPartiesCampaignBehavior>();
        if (patrolPartiesBehavior != null) return true;

        Logger.Debug("Skipping patrol parties interaction update because the campaign behavior is unavailable.");
        return false;
    }
}
