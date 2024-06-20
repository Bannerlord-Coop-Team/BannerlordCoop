using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Client.Services.MobileParties.Messages.Fields;
using GameInterface.Services.MobileParties.Messages.Fields.Events;
using GameInterface.Services.MobileParties.Patches;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Server handler for all fields of the MobileParty class
/// </summary>
public class ServerMobilePartyPropertyHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ServerMobilePartyPropertyHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<MobilePartyPropertyChanged>(Handle);

    }
    
    private void Handle(MessagePayload<MobilePartyPropertyChanged> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkMobilePartyPropertyChanged((int)data._propertyType, data.value1, data.value2));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MobilePartyPropertyChanged>(Handle);
    }
}