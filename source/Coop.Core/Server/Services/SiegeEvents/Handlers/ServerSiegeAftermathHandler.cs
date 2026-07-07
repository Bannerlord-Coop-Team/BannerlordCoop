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

namespace Coop.Core.Server.Services.SiegeEvents.Handlers;

/// <summary>
/// Applies a player's siege aftermath choice authoritatively; the RNG effects replicate as normal
/// sync messages.
/// </summary>
internal class ServerSiegeAftermathHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ISiegeEventInterface siegeEventInterface;

    public ServerSiegeAftermathHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, ISiegeEventInterface siegeEventInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.siegeEventInterface = siegeEventInterface;
        messageBroker.Subscribe<NetworkRequestSiegeAftermath>(HandleRequest);
        messageBroker.Subscribe<SiegeAftermathApplied>(HandleApplied);
        messageBroker.Subscribe<SiegeAftermathChoicePrompted>(HandlePrompted);
    }

    private void HandlePrompted(MessagePayload<SiegeAftermathChoicePrompted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.LeaderParty, out var leaderPartyId)) return;

        // Broadcast; each client checks locally whether it is the leader.
        network.SendAll(new NetworkPromptSiegeAftermathChoice(settlementId, leaderPartyId));
    }

    private void HandleApplied(MessagePayload<SiegeAftermathApplied> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        network.SendAll(new NetworkSiegeAftermathApplied(settlementId, obj.AftermathType));
    }

    private void HandleRequest(MessagePayload<NetworkRequestSiegeAftermath> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.PartyId, out var party)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            siegeEventInterface.ApplySiegeAftermathChoice(party, settlement, obj.AftermathType);
        });
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestSiegeAftermath>(HandleRequest);
        messageBroker.Unsubscribe<SiegeAftermathApplied>(HandleApplied);
        messageBroker.Unsubscribe<SiegeAftermathChoicePrompted>(HandlePrompted);
    }
}
