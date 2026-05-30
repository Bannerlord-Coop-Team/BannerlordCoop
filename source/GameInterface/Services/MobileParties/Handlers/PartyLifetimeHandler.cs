using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;

internal class PartyLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyLifetimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public PartyLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<PartyCreated>(Handle_PartyCreated);
        messageBroker.Subscribe<NetworkCreateParty>(Handle_NetworkCreateParty);
        messageBroker.Subscribe<PartyDestroyed>(Handle_PartyDestroyed);
        messageBroker.Subscribe<NetworkDestroyParty>(Handle_DestroyParty);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyCreated>(Handle_PartyCreated);
        messageBroker.Unsubscribe<NetworkCreateParty>(Handle_NetworkCreateParty);
        messageBroker.Unsubscribe<PartyDestroyed>(Handle_PartyDestroyed);
        messageBroker.Unsubscribe<NetworkDestroyParty>(Handle_DestroyParty);
    }

    private void Handle_PartyCreated(MessagePayload<PartyCreated> payload)
    {
        var party = payload.What.Instance;
        if (!objectManager.AddNewObject(party, out var id))
        {
            Logger.Error("Failed to register party {id}", party?.StringId);
            return;
        }
        network.SendAll(new NetworkCreateParty(id));
    }

    private void Handle_NetworkCreateParty(MessagePayload<NetworkCreateParty> payload)
    {
        var id = payload.What.StringId;
        var newParty = ObjectHelper.SkipConstructor<MobileParty>();

        using (new AllowedThread())
        {
            newParty.StringId = id;
        }

        objectManager.AddExisting(id, newParty);
    }

    private void Handle_PartyDestroyed(MessagePayload<PartyDestroyed> payload)
    {
        var party = payload.What.Instance;
        if (objectManager.TryGetId(party, out var id) == false) return;
        if (objectManager.Remove(party) == false)
        {
            Logger.Error("Unable to remove party with id {id}", id);
            return;
        }
        network.SendAll(new NetworkDestroyParty(id));
    }

    private void Handle_DestroyParty(MessagePayload<NetworkDestroyParty> payload)
    {
        var stringId = payload.What.PartyId;
        if (objectManager.TryGetObject<MobileParty>(stringId, out var party) == false) return;
        if (objectManager.Remove(party) == false)
        {
            Logger.Error("Failed to remove party {partyId} from object manager", party.StringId);
        }
    }
}