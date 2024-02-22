using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages.Lifetime;
using Coop.Core.Server.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.MobileParties.Messages.Lifetime;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

/// <summary>
/// Handles the lifetime of a party on the client
/// </summary>
internal class ClientPartyLifetimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientPartyLifetimeHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkCreateParty>(Handle_NetworkCreateParty);
        messageBroker.Subscribe<PartyCreated>(Handle_PartyCreated);
        messageBroker.Subscribe<NetworkDestroyParty>(Handle_NetworkDestroyParty);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkCreateParty>(Handle_NetworkCreateParty);
        messageBroker.Unsubscribe<PartyCreated>(Handle_PartyCreated);
        messageBroker.Unsubscribe<NetworkDestroyParty>(Handle_NetworkDestroyParty);
    }

    private void Handle_NetworkCreateParty(MessagePayload<NetworkCreateParty> payload)
    {
        messageBroker.Publish(this, new CreateParty(payload.What.Data));
    }

    private void Handle_PartyCreated(MessagePayload<PartyCreated> payload)
    {
        network.SendAll(new NetworkPartyCreated());
    }

    private void Handle_NetworkDestroyParty(MessagePayload<NetworkDestroyParty> payload)
    {
        messageBroker.Publish(this, new DestroyParty(payload.What.Data));
    }
}
