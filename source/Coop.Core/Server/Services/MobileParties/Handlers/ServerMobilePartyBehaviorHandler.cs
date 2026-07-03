using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Behavior;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Handles server communication related to party behavior synchronization.
/// </summary>
/// <seealso cref="Client.Services.MobileParties.Handlers.ClientMobilePartyBehaviorHandler"/>
/// <seealso cref="GameInterface.Services.MobileParties.Handlers.MobilePartyBehaviorHandler"/>
public class ServerMobilePartyBehaviorHandler : IHandler
{
    private readonly IMessageBroker messageBroker;

    public ServerMobilePartyBehaviorHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<ControlledPartyBehaviorUpdated>(Handle);
    }
    public void Dispose()
    {
        messageBroker.Unsubscribe<ControlledPartyBehaviorUpdated>(Handle);
    }

    private void Handle(MessagePayload<ControlledPartyBehaviorUpdated> obj)
    {
        var data = obj.What.BehaviorUpdateData;

        messageBroker.Publish(this, new UpdatePartyBehavior(ref data));
    }
}