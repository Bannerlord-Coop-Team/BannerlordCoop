using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Kingdoms.Messages;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.MobileParties.Messages;
using System;

namespace Coop.Core.Client.Services.Kingdoms.Handlers;

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
        messageBroker.Subscribe<DeclareWar>(Handle);
        messageBroker.Subscribe<NetworkDeclareWarApproved>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<DeclareWar>(Handle);
    }

    private void Handle(MessagePayload<DeclareWar> obj)
    {
        var payload = obj.What;

        NetworkDeclareWarRequest declareWarRequest = new NetworkDeclareWarRequest(payload.Faction1Id, payload.Faction2Id, payload.Detail);

        network.SendAll(declareWarRequest);
    }

    private void Handle(MessagePayload<NetworkDeclareWarApproved> obj)
    {
        var payload = obj.What;

        WarDeclared warDeclared = new WarDeclared(payload.Faction1Id, payload.Faction2Id, payload.Detail);

        messageBroker.Publish(this, warDeclared);
    }
}