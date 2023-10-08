using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Kingdoms.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.MobileParties.Messages;
using System;

namespace Coop.Core.Server.Services.Kingdoms.Handlers;

/// <summary>
/// Handles faction war declarations
/// </summary>
public class FactionWarHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IControllerIdProvider controllerIdProvider;

    private string controllerId => controllerIdProvider.ControllerId;

    public FactionWarHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<NetworkDeclareWarRequest>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkDeclareWarRequest>(Handle);
    }

    private void Handle(MessagePayload<NetworkDeclareWarRequest> obj)
    {
        var payload = obj.What;

        WarDeclared declareWarRequest = new WarDeclared(payload.Faction1Id, payload.Faction2Id, payload.Detail);

        messageBroker.Publish(this, declareWarRequest);

        NetworkDeclareWarApproved declareWarApproved = new NetworkDeclareWarApproved(payload.Faction1Id, payload.Faction2Id, payload.Detail);

        network.SendAll(declareWarApproved);
    }
}