using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;

namespace Coop.Core.Server.Services.Kingdoms.Handlers;

/// <summary>
/// Handles network related data for Kingdoms
/// </summary>
public class ServerKingdomHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public ServerKingdomHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<DecisionAdded>(HandleLocalDecisionAdded);
        messageBroker.Subscribe<DecisionRemoved>(HandleLocalDecisionRemoved);
        messageBroker.Subscribe<KingdomPolicyChanged>(HandleLocalKingdomPolicyChanged);
    }

    private void HandleLocalKingdomPolicyChanged(MessagePayload<KingdomPolicyChanged> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Kingdom, out var kingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.Policy, out var policyId)) return;

        var message = new NetworkChangeKingdomPolicy(kingdomId, policyId, payload.IsAdd);
        network.SendAll(message);
    }

    private void HandleLocalDecisionRemoved(MessagePayload<DecisionRemoved> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Kingdom, out var kingdomId)) return;

        var message = new NetworkRemoveDecision(kingdomId, payload.Index);
        network.SendAll(message);
    }

    private void HandleLocalDecisionAdded(MessagePayload<DecisionAdded> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Kingdom, out var kingdomId)) return;

        var message = new NetworkAddDecision(kingdomId, payload.Data, payload.IgnoreInfluenceCost, payload.RandomNumber);
        network.SendAll(message);
    }


    public void Dispose()
    {
        messageBroker.Unsubscribe<DecisionAdded>(HandleLocalDecisionAdded);
        messageBroker.Unsubscribe<DecisionRemoved>(HandleLocalDecisionRemoved);
        messageBroker.Unsubscribe<KingdomPolicyChanged>(HandleLocalKingdomPolicyChanged);
    }
}