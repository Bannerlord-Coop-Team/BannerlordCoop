using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Client.Services.MobileParties.Packets;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.Settlements.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.Settlements.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Handles server communication related to party behavior synchronization.
/// </summary>
/// <seealso cref="Client.Services.MobileParties.Handlers.ClientMobilePartyBehaviorHandler"/>
/// <seealso cref="GameInterface.Services.MobileParties.Handlers.MobilePartyBehaviorHandler"/>
public class ServerMobilePartyBehaviorHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ServerMobilePartyBehaviorHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        messageBroker.Subscribe<ControlledPartyBehaviorUpdated>(Handle);
        messageBroker.Subscribe<NetworkChangeWagePaymentLimitRequest>(HandleWagePaymentLimitRequest);

    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<ControlledPartyBehaviorUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkChangeWagePaymentLimitRequest>(HandleWagePaymentLimitRequest);

    }

    private void Handle(MessagePayload<ControlledPartyBehaviorUpdated> obj)
    {
        var data = obj.What.BehaviorUpdateData;

        network.SendAll(new UpdatePartyBehaviorPacket(ref data));

        messageBroker.Publish(this, new UpdatePartyBehavior(ref data));
    }

    private void HandleWagePaymentLimitRequest(MessagePayload<NetworkChangeWagePaymentLimitRequest> payload)
    {
        var peer = (NetPeer)payload.Who;
        var obj = payload.What;

        var allOtherPeersMessage = new NetworkChangeWagePaymentLimit(obj.MobilePartyId, obj.WageAmount);
        network.SendAllBut(peer, allOtherPeersMessage);

        var updateServerMessage = new ChangeWagePaymentLimit(obj.MobilePartyId, obj.WageAmount);
        messageBroker.Publish(this, updateServerMessage); // sub on MobileParty
    }
}