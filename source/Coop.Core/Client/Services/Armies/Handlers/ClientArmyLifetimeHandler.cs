using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Armies.Messages;
using Coop.Core.Server.Services.Armies.Messages.Lifetime;
using GameInterface.Services.Armies.Messages.Lifetime;

namespace Coop.Core.Client.Services.Armies.Handlers;

/// <summary>
/// Handles the lifetime of a Army on the client
/// </summary>
internal class ClientArmyLifetimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientArmyLifetimeHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkCreateArmy>(Handle_NetworkCreateArmy);
        messageBroker.Subscribe<ArmyCreated>(Handle_ArmyCreated);
        messageBroker.Subscribe<NetworkDestroyArmy>(Handle_NetworkDestroyArmy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkCreateArmy>(Handle_NetworkCreateArmy);
        messageBroker.Unsubscribe<ArmyCreated>(Handle_ArmyCreated);
        messageBroker.Unsubscribe<NetworkDestroyArmy>(Handle_NetworkDestroyArmy);
    }

    private void Handle_NetworkCreateArmy(MessagePayload<NetworkCreateArmy> payload)
    {
        messageBroker.Publish(this, new CreateArmy(payload.What.Data));
    }

    private void Handle_ArmyCreated(MessagePayload<ArmyCreated> payload)
    {
        network.SendAll(new NetworkArmyCreated());
    }

    private void Handle_NetworkDestroyArmy(MessagePayload<NetworkDestroyArmy> payload)
    {
        messageBroker.Publish(this, new DestroyArmy(payload.What.Data));
    }
}
