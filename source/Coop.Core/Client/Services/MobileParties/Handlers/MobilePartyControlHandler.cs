using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Messages.Control;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

/// <summary>
/// Handles changes to control of mobile party entities.
/// </summary>
public class MobilePartyControlHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IControllerIdProvider controllerIdProvider;

    private string controllerId => controllerIdProvider.ControllerId;

    public MobilePartyControlHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IControllerIdProvider controllerIdProvider) 
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<MainPartyChanged>(Handle);
        messageBroker.Subscribe<NetworkGrantPartyControl>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MainPartyChanged>(Handle);
        messageBroker.Unsubscribe<NetworkGrantPartyControl>(Handle);
    }

    private void Handle(MessagePayload<MainPartyChanged> obj)
    {
        network.SendAll(new NetworkRequestMobilePartyControl(controllerId, obj.What.NewPartyId));
    }

    private void Handle(MessagePayload<NetworkGrantPartyControl> obj)
    {
        var controllerId = obj.What.ControllerId;
        messageBroker.Publish(this, new UpdateMobilePartyControl(controllerId, obj.What.PartyId));
    }
}
