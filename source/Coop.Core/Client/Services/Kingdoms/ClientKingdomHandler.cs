using Common.Messaging;
using Common.Network;

namespace Coop.Core.Client.Services.Kingdoms;

/// <summary>
/// Client side handler for Kingdom internal and network messages
/// </summary>
public class ClientKingdomHandler : IHandler
{

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientKingdomHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
    }
    
    public void Dispose()
    {
    }
}


