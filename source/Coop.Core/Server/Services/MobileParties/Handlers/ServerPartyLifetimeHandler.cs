using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages.Lifetime;
using Coop.Core.Server.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.MobileParties.Messages.Lifetime;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Handler for party lifetime messages on the server.
/// </summary>
internal class ServerPartyLifetimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ICoopServer server;
    private readonly INetworkConfiguration configuration;

    public ServerPartyLifetimeHandler(
        IMessageBroker messageBroker,
        INetwork network,
        ICoopServer server,
        INetworkConfiguration configuration)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.server = server;
        this.configuration = configuration;
        messageBroker.Subscribe<PartyCreated>(Handle_PartyCreated);
        messageBroker.Subscribe<PartyDestroyed>(Handle_PartyDestryed);
    }

    private void Handle_PartyDestryed(MessagePayload<PartyDestroyed> payload)
    {
        network.SendAll(new NetworkDestroyParty(payload.What.Data));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyCreated>(Handle_PartyCreated);
    }

    private void Handle_PartyCreated(MessagePayload<PartyCreated> payload)
    {
        var timeout = configuration.ObjectCreationTimeout;
        var responseProtocol = new ResponseProtocol<NetworkPartyCreated>(server, messageBroker, timeout);

        var triggerMessage = new NetworkCreateParty(payload.What.Data);
        var notifyMessage = new NewPartySynced();
        responseProtocol.StartResponseProtocol(triggerMessage, notifyMessage);
    }
}
