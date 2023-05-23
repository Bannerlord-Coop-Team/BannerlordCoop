using Common.Messaging;
using Common.Network;
using Coop.Core.Templates.ServiceTemplate.Messages;

namespace Coop.Core.Templates.ServiceTemplate.Handlers;

internal class HandlerTemplate : IHandler
{
    private readonly IMessageBroker _messageBroker;
    private readonly INetwork _network;

    public HandlerTemplate(IMessageBroker messageBroker, INetwork network)
    {
        _messageBroker = messageBroker;
        _network = network;

        _messageBroker.Subscribe<EventTemplate>(Handle_EventTemplate);
    }

    public void Dispose()
    {
        _messageBroker.Unsubscribe<EventTemplate>(Handle_EventTemplate);
    }

    private void Handle_EventTemplate(MessagePayload<EventTemplate> obj)
    {
        // Send message over the network example
        _network.SendAll(new NetworkMessageTemplate());

        // Send message internally example
        _messageBroker.Publish(this, new CommandTemplate());
    }
}
