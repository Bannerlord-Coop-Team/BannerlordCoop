using Common.Messaging;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;

namespace GameInterface.Services.MobileParties.Handlers;

internal class MobilePartyControlHandler : IHandler
{
    private readonly IMessageBroker _messageBroker;
    private readonly IMobilePartyInterface _partyInterface;

    public MobilePartyControlHandler(IMessageBroker messageBroker, IMobilePartyInterface partyInterface)
    {
        _messageBroker = messageBroker;
        _partyInterface = partyInterface;

        _messageBroker.Subscribe<RegisterAllPartiesAsControlled>(Handle_RegisterAllPartiesAsControlled);
        _messageBroker.Subscribe<SetInstanceOwnerId>(Handle_SetInstanceOwnerId);
    }

    private void Handle_RegisterAllPartiesAsControlled(MessagePayload<RegisterAllPartiesAsControlled> obj)
    {
        var ownerId = obj.What.OwnerId;
        _partyInterface.RegisterAllPartiesAsControlled(ownerId);
    }
    
    // TODO: This does not belong here.
    private void Handle_SetInstanceOwnerId(MessagePayload<SetInstanceOwnerId> obj)
    {
        var ownerId = obj.What.OwnerId;
        _partyInterface.SetInstanceOwnerId(ownerId);
        
    }

    public void Dispose()
    {
        _messageBroker.Unsubscribe<RegisterAllPartiesAsControlled>(Handle_RegisterAllPartiesAsControlled);
    }
}
