using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Client.Services.Heroes.Handlers;

/// <summary>
/// Client side handler for hero related messages.
/// </summary>
internal class ClientCreateHeroHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientCreateHeroHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkChangeHeroName>(Handle_NetworkChangeHeroName);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkChangeHeroName>(Handle_NetworkChangeHeroName);
    }

    private void Handle_NetworkChangeHeroName(MessagePayload<NetworkChangeHeroName> payload)
    {
        var message = new ChangeHeroName(payload.What.Data);
        messageBroker.Publish(this, message);
    }
}
