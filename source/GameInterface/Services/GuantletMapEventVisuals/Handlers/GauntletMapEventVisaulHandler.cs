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
        if (!objectManager.TryGetObjectWithLogging<GauntletMapEventVisual>(payload.What.InstanceId, out var visual))
            return;

        using(new AllowedThread())
        {
            try
            {
                // Initialize from this client's own map-event visibility, not the server's. The server
                // force-spots every party (no main party) so its value is always visible; map-event icon
                // visibility is local (see MapEventVisibilityClientPatch), and the vanilla IsVisible setter
                // keeps the visual in lock-step, so seeding the visual from the local value keeps the icon
                // and battle sound consistent here instead of starting in the server-visible state.
                visual.Initialize(payload.What.Position, visual.MapEvent?.IsVisible ?? false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize GauntletMapEventVisual with InstanceId {InstanceId}", payload.What.InstanceId);
            }
        }
    }
}
