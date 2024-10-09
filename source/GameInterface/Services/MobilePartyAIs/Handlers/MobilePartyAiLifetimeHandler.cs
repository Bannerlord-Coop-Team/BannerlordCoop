using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.MobilePartyAIs.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MobilePartyAIs.Handlers;
internal class MobilePartyAiLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyAiLifetimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public MobilePartyAiLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<MobilePartyAiCreated>(Handle_MobilePartyAiCreated);
        messageBroker.Subscribe<NetworkCreateMobilePartyAi>(Handle_NetworkCreateMobilePartyAi);

        messageBroker.Subscribe<MobilePartyAiDestroyed>(Handle_MobilePartyAiDestroyed);
        messageBroker.Subscribe<NetworkDestroyMobilePartyAi>(Handle_NetworkDestroyMobilePartyAi);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MobilePartyAiCreated>(Handle_MobilePartyAiCreated);
        messageBroker.Unsubscribe<NetworkCreateMobilePartyAi>(Handle_NetworkCreateMobilePartyAi);

        messageBroker.Unsubscribe<MobilePartyAiDestroyed>(Handle_MobilePartyAiDestroyed);
        messageBroker.Unsubscribe<NetworkDestroyMobilePartyAi>(Handle_NetworkDestroyMobilePartyAi);
    }

    private void Handle_MobilePartyAiCreated(MessagePayload<MobilePartyAiCreated> payload)
    {
        if (objectManager.AddNewObject(payload.What.Instance, out var partyAiId) == false) return;

        var partyId = payload.What.Party.StringId;

        network.SendAll(new NetworkCreateMobilePartyAi(partyAiId, partyId));
    }


    private void Handle_NetworkCreateMobilePartyAi(MessagePayload<NetworkCreateMobilePartyAi> payload)
    {
        var partyId = payload.What.PartyId;
        var aiId = payload.What.MobilePartyAiId;

        if (objectManager.TryGetObject<MobileParty>(partyId, out var party) == false) return;

        var newAi = ObjectHelper.SkipConstructor<MobilePartyAi>();

        objectManager.AddExisting(aiId, newAi);
    }

    private void Handle_MobilePartyAiDestroyed(MessagePayload<MobilePartyAiDestroyed> payload)
    {
        var ai = payload.What.Instance;

        if (objectManager.TryGetId(ai, out var aiId) == false) return;

        network.SendAll(new NetworkDestroyMobilePartyAi(aiId));
    }

    private void Handle_NetworkDestroyMobilePartyAi(MessagePayload<NetworkDestroyMobilePartyAi> payload)
    {
        var aiId = payload.What.MobilePartyAiId;

        if (objectManager.TryGetObject<MobilePartyAi>(aiId, out var ai) == false) return;

        if (objectManager.Remove(ai) == false)
        {
            Logger.Error("Failed to remove Ai with id {id} from object manager", aiId);
            return;
        }
    }
}
