using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Template.Messages;
using Coop.Core.Server.Services.Template.Messages;
using GameInterface.Services.Template.Messages;

namespace Coop.Core.Server.Services.Template.Handlers;

/// <summary>
/// TODO describe class
/// </summary>
internal class TemplateServerHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public TemplateServerHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        // This handles an internal message
        messageBroker.Subscribe<TemplateEventMessage>(Handle_TemplateEventMessage);

        // This handles a message from a client
        messageBroker.Subscribe<NetworkClientMessageTemplate>(Handle_NetworkClientMessageTemplate);
        
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TemplateEventMessage>(Handle_TemplateEventMessage);
        messageBroker.Unsubscribe<NetworkClientMessageTemplate>(Handle_NetworkClientMessageTemplate);
    }

    private void Handle_TemplateEventMessage(MessagePayload<TemplateEventMessage> obj)
    {
        var payload = obj.What;

        // Here you can add checks to allow or disallow the state change

        // Make sure to change the state on the server as well since the function was skipped
        var message = new TemplateCommandMessage();
        messageBroker.Publish(this, message);

        // Broadcast to all the clients that the state was changed
        var networkMessage = new NetworkServerMessageTemplate($"Example Data {payload.GetType().Name}");
        network.SendAll(networkMessage);
    }

    // This method is the same as the one above but you may want to handle a message from the client slightly differently
    private void Handle_NetworkClientMessageTemplate(MessagePayload<NetworkClientMessageTemplate> obj)
    {
        
        var payload = obj.What;

        // Here you can add checks to allow or disallow the state change

        // Make sure to change the state on the server as well since the function was skipped
        var message = new TemplateCommandMessage();
        messageBroker.Publish(this, message);

        // Broadcast to all the clients that the state was changed
        var networkMessage = new NetworkServerMessageTemplate($"Example Data {payload.GetType().Name}");
        network.SendAll(networkMessage);
    }
}
