using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Packets;
using GameInterface.Services.MobileParties.Messages.Behavior;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Handles server communication related to party behavior synchronization.
/// </summary>
/// <seealso cref="Client.Services.MobileParties.Handlers.MobilePartyBehaviorHandler"/>
/// <seealso cref="GameInterface.Services.MobileParties.Handlers.MobilePartyBehaviorHandler"/>
public class MobilePartyBehaviorHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public MobilePartyBehaviorHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        messageBroker.Subscribe<ControlledPartyBehaviorUpdated>(Handle);
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<ControlledPartyBehaviorUpdated>(Handle);
    }

    private void Handle(MessagePayload<ControlledPartyBehaviorUpdated> obj)
    {
        var data = obj.What.BehaviorUpdateData;

        network.SendAll(new UpdatePartyBehaviorPacket(ref data));

        messageBroker.Publish(this, new UpdatePartyBehavior(ref data));
    }
}