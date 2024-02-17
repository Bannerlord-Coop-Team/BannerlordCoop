using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages.Data;
using GameInterface.Services.MobileParties.Messages.Data;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Server side handler for party related messages.
/// </summary>
internal class ServerPartyDataHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ServerPartyDataHandler(
        IMessageBroker messageBroker,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        messageBroker.Subscribe<PartyArmyChanged>(Handle_ArmyChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyArmyChanged>(Handle_ArmyChanged);
    }

    private void Handle_ArmyChanged(MessagePayload<PartyArmyChanged> payload)
    {
        network.SendAll(new NetworkChangePartyArmy(payload.What.Data));
    }
}
