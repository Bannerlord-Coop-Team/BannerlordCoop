using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.GuantletMapEventVisuals.Messages;
using GameInterface.Services.MapEvents.Initialization;
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
    private readonly IMapEventBattleSizeCorrection mapEventBattleSizeCorrection;
    private readonly IMapEventInitializationTracker mapEventInitializationTracker;

    public GauntletMapEventVisaulHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IMapEventBattleSizeCorrection mapEventBattleSizeCorrection,
        IMapEventInitializationTracker mapEventInitializationTracker)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.mapEventBattleSizeCorrection = mapEventBattleSizeCorrection;
        this.mapEventInitializationTracker = mapEventInitializationTracker;
        messageBroker.Subscribe<GauntletMapEventVisualInitialized>(Handle_GauntletMapEventVisualInitialized);
        messageBroker.Subscribe<NetworkGauntletMapEventVisualInitialized>(Handle_NetworkGauntletMapEventVisualInitialized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GauntletMapEventVisualInitialized>(Handle_GauntletMapEventVisualInitialized);
        messageBroker.Unsubscribe<NetworkGauntletMapEventVisualInitialized>(Handle_NetworkGauntletMapEventVisualInitialized);

        // Drop any pending battle-size corrections so they don't carry stale ids into the next session.
        mapEventBattleSizeCorrection.Reset();
    }
    private void Handle_GauntletMapEventVisualInitialized(MessagePayload<GauntletMapEventVisualInitialized> payload)
    {
        var obj = payload.What;
        var mapEvent = obj.Instance?.MapEvent;
        if (mapEventInitializationTracker.IsPending(obj.Instance)
            || mapEventInitializationTracker.IsPending(mapEvent)
            || mapEventInitializationTracker.IsBuilding(mapEvent))
        {
            return;
        }

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
        // thread. Re-resolve this post-commit delta on the main thread so a matching destroy
        // is observed and a stale init is skipped.
        GameThread.Run(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<GauntletMapEventVisual>(instanceId, out var visual))
                return;

            // A committed visual must already reference its MapEvent. Treat a missing edge as a stale or
            // malformed delta instead of letting vanilla Initialize throw.
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

                    // Reinforcements can change the ambient battle size after initialization. Track field
                    // battles and sally-outs, the event types whose visuals read that value.
                    var mapEvent = visual.MapEvent;
                    if (mapEvent.IsFieldBattle || mapEvent.IsSallyOut)
                    {
                        mapEventBattleSizeCorrection.Register(visual);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to initialize GauntletMapEventVisual with InstanceId {InstanceId}", instanceId);
                }
            }
        });
    }
}
