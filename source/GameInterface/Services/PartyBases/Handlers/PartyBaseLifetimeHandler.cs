using Common.Messaging;
using Common.Network;
using GameInterface.Registry.Auto;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases.Handlers;
internal class PartyBaseLifetimeHandler : IHandler
{
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly ILogger logger;
    public PartyBaseLifetimeHandler(IObjectManager objectManager, INetwork network, IMessageBroker messageBroker, ILogger logger)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.messageBroker = messageBroker;
        this.logger = logger;

        messageBroker.Subscribe<InstanceDestroyed<MobileParty>>(Handle_PartyDestroyed);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InstanceDestroyed<MobileParty>>(Handle_PartyDestroyed);
    }

    private void Handle_PartyDestroyed(MessagePayload<InstanceDestroyed<MobileParty>> payload)
    {
        var partyBase = payload.What.Instance.Party;
        messageBroker.Publish(this, new InstanceDestroyed<PartyBase>(partyBase));
    }
}
