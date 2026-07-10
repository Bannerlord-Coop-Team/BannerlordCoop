using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Messages;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Client.Services.SiegeEvents.Handlers;

/// <summary>
/// Sends the local player's siege aftermath choice to the server.
/// </summary>
internal class ClientSiegeAftermathHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ISiegeEventInterface siegeEventInterface;

    public ClientSiegeAftermathHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, ISiegeEventInterface siegeEventInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.siegeEventInterface = siegeEventInterface;
        messageBroker.Subscribe<SiegeAftermathAttempted>(HandleAttempt);
        messageBroker.Subscribe<NetworkSiegeAftermathApplied>(HandleApplied);
        messageBroker.Subscribe<NetworkPromptSiegeAftermathChoice>(HandleChoicePrompt);
    }

    private void HandleChoicePrompt(MessagePayload<NetworkPromptSiegeAftermathChoice> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.LeaderPartyId, out var leaderParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            siegeEventInterface.PromptLocalAftermathChoice(leaderParty, settlement);
        });
    }

    private void HandleApplied(MessagePayload<NetworkSiegeAftermathApplied> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            siegeEventInterface.SetLocalAftermathNarration(settlement, obj.AftermathType);
        });
    }

    // Runs on the game thread already — published from the ApplyAftermath prefix; only resolves ids and sends, so no GameThread.RunSafe.
    private void HandleAttempt(MessagePayload<SiegeAftermathAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        network.SendAll(new NetworkRequestSiegeAftermath(partyId, settlementId, obj.AftermathType));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeAftermathAttempted>(HandleAttempt);
        messageBroker.Unsubscribe<NetworkSiegeAftermathApplied>(HandleApplied);
        messageBroker.Unsubscribe<NetworkPromptSiegeAftermathChoice>(HandleChoicePrompt);
    }
}
