using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.PartyVisuals.Extensions;
using GameInterface.Services.PartyVisuals.Messages;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Handlers;

public class PartyVisualLifetimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public PartyVisualLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<PartyVisualCreated>(Handle);
        messageBroker.Subscribe<NetworkCreatePartyVisual>(Handle);
        messageBroker.Subscribe<PartyVisualDestroyed>(Handle);
        messageBroker.Subscribe<NetworkDestroyPartyVisual>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyVisualCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreatePartyVisual>(Handle);
        messageBroker.Unsubscribe<PartyVisualDestroyed>(Handle);
        messageBroker.Unsubscribe<NetworkDestroyPartyVisual>(Handle);
    }

    private void Handle(MessagePayload<PartyVisualCreated> payload)
    {
        if (!objectManager.AddNewObject(payload.What.MobilePartyVisual, out var visualId))
            return;

        // The visual is keyed off the mobile party. Replicate the mobile party id (always present on
        // the server) rather than the party base id, whose MobileParty back-link the client syncs
        // separately and may not have applied yet when the create arrives.
        var mobileParty = payload.What.PartyBase?.MobileParty;
        if (mobileParty == null)
            return;

        if (!objectManager.TryGetIdWithLogging(mobileParty, out string mobilePartyId))
            return;

        network.SendAll(new NetworkCreatePartyVisual(visualId, mobilePartyId));
    }

    private void Handle(MessagePayload<NetworkCreatePartyVisual> payload)
    {
        var mobilePartyId = payload.What.MobilePartyId;
        if (mobilePartyId == null) return;

        var partyVisualId = payload.What.PartyVisualId;

        // Resolve the party and build its visual on the main thread, in network order behind the
        // party-creation handler that runs on the same FIFO game-loop queue. Resolving the id here
        // (not on the poll thread) keeps it ordered behind that registration, and reads the mobile
        // party directly rather than PartyBase.MobileParty, whose back-link the client syncs
        // separately and may not have applied yet when this create arrives. The visuals manager
        // mutates its party list here while OnTick walks it on the main thread, so this must not race.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(mobilePartyId, out var mobileParty))
                return;

            using (new AllowedThread())
            {
                mobileParty.CreateNewPartyVisual();

                var partyVisual = mobileParty.Party.GetPartyVisual();
                if (partyVisual != null)
                    objectManager.AddExisting(partyVisualId, partyVisual);
            }
        }, context: $"create party visual {partyVisualId}");
    }

    private void Handle(MessagePayload<PartyVisualDestroyed> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MobilePartyVisual, out string partyVisualId))
            return;

        if (!objectManager.TryGetIdWithLogging(payload.What.MobileParty, out string mobilePartyId))
            return;

        objectManager.Remove(payload.What.MobilePartyVisual);

        network.SendAll(new NetworkDestroyPartyVisual(partyVisualId, mobilePartyId));
    }

    private void Handle(MessagePayload<NetworkDestroyPartyVisual> payload)
    {
        var partyVisualId = payload.What.PartyVisualId;
        var mobilePartyId = payload.What.MobilePartyId;

        GameThread.RunSafe(() =>
        {
            if (objectManager.TryGetObject<MobilePartyVisual>(partyVisualId, out var registeredPartyVisual))
                objectManager.Remove(registeredPartyVisual);

            if (!objectManager.TryGetObjectWithLogging<MobileParty>(mobilePartyId, out var mobileParty))
                return;

            using (new AllowedThread())
            {
                var partyVisual = mobileParty.Party.GetPartyVisual();
                if (partyVisual != null)
                    objectManager.Remove(partyVisual);

                MobilePartyVisualManager.Current?.RemovePartyVisualForParty(mobileParty);
            }
        }, context: $"destroy party visual {mobilePartyId}");
    }
}
