using Common.Messaging;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Client.Services.MobileParties.Messages.Fields;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Fields.Commands;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

public class ClientMobilePartyPropertyHandler : IHandler
{
    private readonly IMessageBroker messageBroker;

    public ClientMobilePartyPropertyHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<NetworkMobilePartyPropertyChanged>(Handle);
    }      

    private void Handle(MessagePayload<NetworkMobilePartyPropertyChanged> payload)
    {
        var data = payload.What;
        messageBroker.Publish(this, new ChangeMobilePartyProperty(data.PropertyType, data.Value1, data.Value2, data.Value3));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkMobilePartyPropertyChanged>(Handle);
    }
}