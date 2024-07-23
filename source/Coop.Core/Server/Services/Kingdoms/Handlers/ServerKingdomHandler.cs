using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Messages;

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
        messageBroker.Subscribe<DecisionAdded>(HandleLocalDecisionAdded);
        messageBroker.Subscribe<DecisionRemoved>(HandleLocalDecisionRemoved);
    }

    private void HandleLocalDecisionRemoved(MessagePayload<DecisionRemoved> obj)
    {
        var payload = obj.What;
        var message = new NetworkRemoveDecision(payload.KingdomId, payload.Index);
        network.SendAll(message);
    }

    private void HandleLocalDecisionAdded(MessagePayload<DecisionAdded> obj)
    {
        var payload = obj.What;
        var message = new NetworkAddDecision(payload.KingdomId, payload.Data, payload.IgnoreInfluenceCost, payload.RandomNumber);
        network.SendAll(message);
    }


    public void Dispose()
    {
        messageBroker.Unsubscribe<DecisionAdded>(HandleLocalDecisionAdded);
        messageBroker.Unsubscribe<DecisionRemoved>(HandleLocalDecisionRemoved);
    }
}