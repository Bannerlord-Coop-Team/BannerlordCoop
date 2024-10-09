using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Sieges.Messages;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.Sieges.Handlers;
internal class SiegeEventLifetimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;


    public SiegeEventLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<SiegeEventCreated>(Handle);
        messageBroker.Subscribe<NetworkCreateSiegeEvent>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeEventCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateSiegeEvent>(Handle);
    }


    private void Handle(MessagePayload<SiegeEventCreated> payload)
    {
        if (objectManager.AddNewObject(payload.What.Instance, out var siegeEventId) == false) return;

        network.SendAll(new NetworkCreateSiegeEvent(siegeEventId));
    }

    private void Handle(MessagePayload<NetworkCreateSiegeEvent> payload)
    {
        var newSiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();

        objectManager.AddExisting(payload.What.SiegeId, newSiegeEvent);
    }
}
