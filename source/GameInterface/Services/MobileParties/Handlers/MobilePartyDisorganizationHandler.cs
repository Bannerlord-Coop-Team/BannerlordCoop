using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

internal class MobilePartyDisorganizationHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public MobilePartyDisorganizationHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        messageBroker.Subscribe<MobilePartyDisorganizationChanged>(HandleChanged);
        messageBroker.Subscribe<NetworkMobilePartyDisorganizationChanged>(HandleNetworkChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MobilePartyDisorganizationChanged>(HandleChanged);
        messageBroker.Unsubscribe<NetworkMobilePartyDisorganizationChanged>(HandleNetworkChanged);
    }

    private void HandleChanged(MessagePayload<MobilePartyDisorganizationChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var change = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetHandleWithLogging(change.Party, out var partyId)) return;
            network.SendAll(new NetworkMobilePartyDisorganizationChanged(partyId, change.IsDisorganized));
        });
    }

    private void HandleNetworkChanged(MessagePayload<NetworkMobilePartyDisorganizationChanged> payload)
    {
        if (ModInformation.IsServer) return;
        var change = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(change.PartyId, out var party)) return;
            if (party.IsDisorganized == change.IsDisorganized) return;
            using (new AllowedThread())
            {
                // SetDisorganized(true) would replace the separately replicated expiry time.
                party._isDisorganized = change.IsDisorganized;
                party.UpdateVersionNo();
            }
        });
    }
}
