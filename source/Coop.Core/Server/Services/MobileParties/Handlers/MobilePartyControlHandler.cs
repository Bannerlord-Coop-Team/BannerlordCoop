using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Messages.Control;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Handles changing the control of mobile party entities.
/// </summary>
public class MobilePartyControlHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public MobilePartyControlHandler(
        IMessageBroker messageBroker,
        INetwork network)
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
        var partyId = obj.What.PartyId;
        var controllerId = obj.What.ControllerId;

        messageBroker.Publish(this, new UpdateMobilePartyControl(controllerId, partyId));

        network.SendAll(new NetworkGrantPartyControl(controllerId, partyId));
    }
}
