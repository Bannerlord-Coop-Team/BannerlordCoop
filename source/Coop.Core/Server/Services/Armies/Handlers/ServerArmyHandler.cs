using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Armies.Messages;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Messages.Lifetime;


namespace Coop.Core.Server.Services.Armies.Handlers;

/// <summary>
/// Server side handler for Kingdom internal and network messages
/// </summary>
public class ServerArmyHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ServerArmyHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        // This handles an internal message
        messageBroker.Subscribe<MobilePartyInArmyAdded>(HandleAddMobilePartyInArmy);
        messageBroker.Subscribe<MobilePartyInArmyRemoved>(HandleRemoveMobilePartyInArmy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MobilePartyInArmyAdded>(HandleAddMobilePartyInArmy);
        messageBroker.Unsubscribe<MobilePartyInArmyRemoved>(HandleRemoveMobilePartyInArmy);
    }

    private void HandleAddMobilePartyInArmy(MessagePayload<MobilePartyInArmyAdded> obj)
    {
        MobilePartyInArmyAdded mobilePartyInArmyAdded = obj.What;

        // Broadcast to all the clients that the state was changed
        var message = new NetworkAddMobilePartyInArmy(mobilePartyInArmyAdded.MobilePartyListId, mobilePartyInArmyAdded.ArmyId);
        
        network.SendAll(message);
    }

    private void HandleRemoveMobilePartyInArmy(MessagePayload<MobilePartyInArmyRemoved> obj)
    {
        MobilePartyInArmyRemoved mobilePartyInArmyRemoved = obj.What;

        // Broadcast to all the clients that the state was changed
        var message = new NetworkRemoveMobilePartyInArmy(mobilePartyInArmyRemoved.MobilePartyIds, mobilePartyInArmyRemoved.ArmyId);
        
        network.SendAll(message);
    }
}