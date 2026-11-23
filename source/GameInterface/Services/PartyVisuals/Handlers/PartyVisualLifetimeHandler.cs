using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyVisuals.Extensions;
using GameInterface.Services.PartyVisuals.Interfaces;
using GameInterface.Services.PartyVisuals.Messages;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Handlers;

/// <summary>Applies replicated party visuals and forwards headless party destruction.</summary>
public class PartyVisualLifetimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPartyVisualInterface partyVisuals;

    public PartyVisualLifetimeHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPartyVisualInterface partyVisuals)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.partyVisuals = partyVisuals;
        messageBroker.Subscribe<NetworkCreatePartyVisual>(Handle);
        messageBroker.Subscribe<MobilePartyDestroyed>(Handle);
        messageBroker.Subscribe<NetworkDestroyPartyVisual>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkCreatePartyVisual>(Handle);
        messageBroker.Unsubscribe<MobilePartyDestroyed>(Handle);
        messageBroker.Unsubscribe<NetworkDestroyPartyVisual>(Handle);
    }

    private void Handle(MessagePayload<NetworkCreatePartyVisual> payload)
    {
        // Resolve the handle behind party registration on the same game-thread queue.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(payload.What.MobilePartyHandle, out var mobileParty))
                return;

            using (new AllowedThread())
            {
                // A recreated model needs a refresh even if the previous model cleared the dirty flag.
                mobileParty.Party.SetVisualAsDirty();
                mobileParty.CreateNewPartyVisual();
                // A naval visual only builds its ship while the party is marked dirty, which the party's old visual already cleared
                mobileParty.SetNavalVisualAsDirty();
            }
        }, context: $"create party visual {payload.What.MobilePartyHandle}");
    }

    private void Handle(MessagePayload<MobilePartyDestroyed> payload)
    {
        // The registry publishes this before it removes the party's handle.
        partyVisuals.OnPartyDestroyed(payload.What.MobileParty);
    }

    private void Handle(MessagePayload<NetworkDestroyPartyVisual> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(payload.What.MobilePartyHandle, out var mobileParty))
                return;

            using (new AllowedThread())
            {
                mobileParty.RemovePartyVisual();
            }
        }, context: $"destroy party visual {payload.What.MobilePartyHandle}");
    }
}
