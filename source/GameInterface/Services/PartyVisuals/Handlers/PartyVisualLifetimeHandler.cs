using System;
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.PartyVisuals.Messages;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Handlers;

public class PartyVisualLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyVisualLifetimeHandler>();

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

        var partyVisualId = payload.What.PartyVisualId;

        if (MobilePartyVisualManager.Current != null)
        {
            // Normal client: the map visuals manager mutates its party list and builds native visual
            // entities here, while its OnTick walks that same list in parallel on the main thread.
            // Doing this on the network thread races that tick and corrupts memory, so marshal it onto
            // the main thread. Re-read Current there: it can be torn down or replaced (campaign exit,
            // save reload, mission entry) between this enqueue and the queued action running.
            RunOnGameThreadGuarded("create", partyVisualId, () =>
            {
                var visualManager = MobilePartyVisualManager.Current;
                if (visualManager == null) return;

                visualManager.AddNewPartyVisualForParty(partyBase.MobileParty);
                objectManager.AddExisting(partyVisualId, partyBase.GetPartyVisual());
            });
        }
        else
        {
            // No visuals manager (headless server / tests): construct directly so the object is
            // still tracked for sync without participating in map rendering.
            using (new AllowedThread())
            {
                objectManager.AddExisting(partyVisualId, new MobilePartyVisual(partyBase));
            }
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
        var partyVisualId = payload.What.PartyVisualId;

        // Defer the whole removal onto the main thread so it runs in network order relative to the
        // create handler (which also defers). Resolving and removing the visual here, on the network
        // thread, would race a create whose registration is still queued: the lookup would miss the
        // not-yet-registered id, the destroy would be dropped, and the queued create would then leave
        // a zombie visual on the map.
        RunOnGameThreadGuarded("destroy", partyVisualId, () =>
        {
            if (!objectManager.TryGetObjectWithLogging(partyVisualId, out MobilePartyVisual partyVisual))
                return;

            // Deregister first so the id is freed even if the native removal below throws.
            objectManager.Remove(partyVisual);
            MobilePartyVisualManager.Current?.RemovePartyVisualForParty(partyVisual.MapEntity.MobileParty);
        });
    }

    /// <summary>
    /// Runs party-visual work on the game loop thread with mod patches suppressed, logging any failure
    /// instead of letting it escape the game loop. The map visuals manager and the object registry must
    /// only be mutated on the main thread (its OnTick walks them in parallel), and the deferred action
    /// no longer runs inside the message broker's own try/catch, so it carries its own.
    /// </summary>
    private static void RunOnGameThreadGuarded(string operation, string partyVisualId, Action action)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to {Operation} party visual {VisualId}", operation, partyVisualId);
                }
            }
        });
    }
}
