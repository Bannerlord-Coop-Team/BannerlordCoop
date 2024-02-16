using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Armies.Messages;
using Coop.Core.Server.Services.Armies.Messages.Lifetime;
using GameInterface.Services.Armies.Messages.Lifetime;

namespace Coop.Core.Server.Services.Armies.Handlers;

/// <summary>
/// Handler for Army lifetime messages on the server.
/// </summary>
internal class ServerArmyLifetimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ICoopServer server;
    private readonly INetworkConfiguration configuration;

    public ServerArmyLifetimeHandler(
        IMessageBroker messageBroker,
        INetwork network,
        ICoopServer server,
        INetworkConfiguration configuration)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.server = server;
        this.configuration = configuration;
        messageBroker.Subscribe<ArmyCreated>(Handle_ArmyCreated);
        messageBroker.Subscribe<ArmyDestroyed>(Handle_ArmyDestryed);
    }

    private void Handle_ArmyDestryed(MessagePayload<ArmyDestroyed> payload)
    {
        network.SendAll(new NetworkDestroyArmy(payload.What.Data));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ArmyCreated>(Handle_ArmyCreated);
    }

    private void Handle_ArmyCreated(MessagePayload<ArmyCreated> payload)
    {
        var timeout = configuration.ObjectCreationTimeout;
        var responseProtocol = new ResponseProtocol<NetworkArmyCreated>(server, messageBroker, timeout);

        var triggerMessage = new NetworkCreateArmy(payload.What.Data);
        var notifyMessage = new NewArmySynced();
        responseProtocol.StartResponseProtocol(triggerMessage, notifyMessage);
    }
}
