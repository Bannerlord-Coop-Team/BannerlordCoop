using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Control;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Handles changing the control of mobile party entities.
/// </summary>
public class MobilePartyControlHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public MobilePartyControlHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkRequestMobilePartyControl>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestMobilePartyControl>(Handle);
    }

    private void Handle(MessagePayload<NetworkRequestMobilePartyControl> obj)
    {
        string partyId = obj.What.PartyId;

        messageBroker.Publish(this, new UpdateMobilePartyControl(partyId, true));

        network.SendAll(new NetworkGrantPartyControl(partyId));
    }
}
