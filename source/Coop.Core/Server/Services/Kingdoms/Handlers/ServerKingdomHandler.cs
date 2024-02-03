using Common.Messaging;
using Common.Network;


namespace Coop.Core.Server.Services.Kingdoms.Handlers;

/// <summary>
/// Handles network related data for Kingdoms
/// </summary>
public class ServerKingdomHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ServerKingdomHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
    }
   
    public void Dispose()
    {
    }
}