using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.PartyVisuals.Messages;
using SandBox.View;
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

        if (!objectManager.TryGetIdWithLogging(payload.What.PartyBase, out string partyBaseId))
            return;

        network.SendAll(new NetworkCreatePartyVisual(visualId, partyBaseId));
    }

    private void Handle(MessagePayload<NetworkCreatePartyVisual> payload)
    {
        if (payload.What.PartyBaseId == null) return;

        if (!objectManager.TryGetObjectWithLogging<PartyBase>(payload.What.PartyBaseId, out var partyBase))
            return;

        using(new AllowedThread())
        {
            MobilePartyVisual newVisual;

            // MobilePartyVisualManager.Current throws (rather than returning null) while the
            // map view is not initialized — e.g. on a client that is still on the loading
            // screen when the server announces a new party. Resolve it null-safely so the
            // no-manager fallback below stays reachable.
            var visualManager = SandBoxViewSubModule.SandBoxViewVisualManager?.GetEntityComponent<MobilePartyVisualManager>();
            if (visualManager != null)
            {
                // Normal client: let the map visuals manager create the visual so the party renders.
                visualManager.AddNewPartyVisualForParty(partyBase.MobileParty);
                newVisual = partyBase.GetPartyVisual();
            }
            else
            {
                // No visuals manager (headless server / tests): construct directly so the object is
                // still tracked for sync without participating in map rendering.
                newVisual = new MobilePartyVisual(partyBase);
            }

            objectManager.AddExisting(payload.What.PartyVisualId, newVisual);
        }
    }

    private void Handle(MessagePayload<PartyVisualDestroyed> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MobilePartyVisual, out string visualId))
            return;

        objectManager.Remove(payload.What.MobilePartyVisual);

        network.SendAll(new NetworkDestroyPartyVisual(visualId));
    }

    private void Handle(MessagePayload<NetworkDestroyPartyVisual> payload)
    {
        if (!objectManager.TryGetObjectWithLogging(payload.What.PartyVisualId, out MobilePartyVisual partyVisual))
            return;

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                var visualManager = SandBoxViewSubModule.SandBoxViewVisualManager?.GetEntityComponent<MobilePartyVisualManager>();
                var mobileParty = partyVisual.MapEntity?.MobileParty;

                // Without a map view there is nothing rendering the visual; unregistering it
                // (below) is all that is needed.
                if (visualManager == null || mobileParty == null) return;

                visualManager.RemovePartyVisualForParty(mobileParty);
            }
        });

        objectManager.Remove(partyVisual);
    }
}
