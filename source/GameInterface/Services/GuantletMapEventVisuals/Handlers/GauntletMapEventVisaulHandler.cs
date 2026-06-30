using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.GuantletMapEventVisuals.Messages;
using GameInterface.Services.ObjectManager;
using SandBox.GauntletUI.Map;
using Serilog;
using System;

namespace GameInterface.Services.GuantletMapEventVisuals.Handlers;

internal class GauntletMapEventVisaulHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GauntletMapEventVisaulHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public GauntletMapEventVisaulHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        messageBroker.Subscribe<GauntletMapEventVisualInitialized>(Handle_GauntletMapEventVisualInitialized);
        messageBroker.Subscribe<NetworkGauntletMapEventVisualInitialized>(Handle_NetworkGauntletMapEventVisualInitialized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GauntletMapEventVisualInitialized>(Handle_GauntletMapEventVisualInitialized);
        messageBroker.Unsubscribe<NetworkGauntletMapEventVisualInitialized>(Handle_NetworkGauntletMapEventVisualInitialized);
    }
    private void Handle_GauntletMapEventVisualInitialized(MessagePayload<GauntletMapEventVisualInitialized> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Instance, out var id))
            return;

        var message = new NetworkGauntletMapEventVisualInitialized(id, obj.Position, obj.IsVisible);
        network.SendAll(message);
    }

    private void Handle_NetworkGauntletMapEventVisualInitialized(MessagePayload<NetworkGauntletMapEventVisualInitialized> payload)
    {
        var instanceId = payload.What.InstanceId;
        var position = payload.What.Position;

        // Initializing the visual touches Gauntlet map UI, which is only safe on the main
        // thread. The visual is re-resolved on the main thread so that a matching destroy
        // which arrived first (and ran synchronously on the network thread) is observed here
        // and the now stale init is skipped.
        GameThread.Run(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<GauntletMapEventVisual>(instanceId, out var visual))
                return;

            // The visual's MapEvent syncs in separately; if it never resolved here it stays null and vanilla
            // Initialize throws on it. An un-synced visual has no battle to show, so skip the init.
            if (visual.MapEvent == null)
            {
                Logger.Warning("Skipping init of GauntletMapEventVisual {InstanceId}: its MapEvent did not resolve on this client", instanceId);
                return;
            }

            using (new AllowedThread())
            {
                try
                {
                    // Initialize from this client's own map-event visibility, not the server's. The server
                    // force-spots every party (no main party) so its value is always visible; map-event icon
                    // visibility is local (see MapEventVisibilityClientPatch), and the vanilla IsVisible setter
                    // keeps the visual in lock-step, so seeding the visual from the local value keeps the icon
                    // and battle sound consistent here instead of starting in the server-visible state.
                    visual.Initialize(position, visual.MapEvent.IsVisible);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to initialize GauntletMapEventVisual with InstanceId {InstanceId}", instanceId);
                }
            }
        });
    }
}
