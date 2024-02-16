using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Armies.Messages;
using GameInterface.Services.Armies.Messages;


namespace Coop.Core.Client.Services.Armies.Handlers;

/// <summary>
/// Handles Network Communications from the Server regarding Armies.
/// </summary>
public class ClientArmyHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientArmyHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkAddMobilePartyInArmy>(HandleMobilePartyInArmyAdd);
        messageBroker.Subscribe<NetworkRemoveMobilePartyInArmy>(HandleMobilePartyInArmyRemove);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAddMobilePartyInArmy>(HandleMobilePartyInArmyAdd);
        messageBroker.Unsubscribe<NetworkRemoveMobilePartyInArmy>(HandleMobilePartyInArmyRemove);
    }

    private void HandleMobilePartyInArmyAdd(MessagePayload<NetworkAddMobilePartyInArmy> payload)
    {
        var obj = payload.What;
        var message = new AddMobilePartyInArmy(obj.MobilePartyId, obj.ArmyId);

        messageBroker.Publish(this, message);
    }

    private void HandleMobilePartyInArmyRemove(MessagePayload<NetworkRemoveMobilePartyInArmy> payload)
    {
        var obj = payload.What;
        var message = new RemoveMobilePartyInArmy(obj.MobilePartyId, obj.ArmyId);

        messageBroker.Publish(this, message);
    }
}
