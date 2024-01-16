using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Template.Messages;
using Coop.Core.Server.Services.Template.Messages;
using GameInterface.Services.Template.Messages;

namespace Coop.Core.Client.Services.Template.Handlers;

/// <summary>
/// TODO describe class
/// </summary>
internal class TemplateClientHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public TemplateClientHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        // This handles a message from the server
        messageBroker.Subscribe<NetworkServerMessageTemplate>(Handle_NetworkServerMessageTemplate);

        // This handles an internal message
        // For functionality that the client can control
        messageBroker.Subscribe<TemplateEventMessage>(Handle_TemplateEventMessage);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkServerMessageTemplate>(Handle_NetworkServerMessageTemplate);
        messageBroker.Unsubscribe<TemplateEventMessage>(Handle_TemplateEventMessage);
    }

    private void Handle_NetworkServerMessageTemplate(MessagePayload<NetworkServerMessageTemplate> obj)
    {
        var payload = obj.What;

        // Changes the state on the client
        var message = new TemplateCommandMessage();
        messageBroker.Publish(this, message);
    }

    private void Handle_TemplateEventMessage(MessagePayload<TemplateEventMessage> obj)
    {
        var payload = obj.What;

        // Note how we do not change the state in this method, we delegate it to the server and the state will 
        // be updated in Handle_NetworkServerMessageTemplate

        // Broadcast to all the clients that the state was changed
        var networkMessage = new NetworkClientMessageTemplate($"Example Data {payload.GetType().Name}");
        network.SendAll(networkMessage);
    }
}
