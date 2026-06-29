using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.GuantletMapEventVisuals.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;

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

            using (new AllowedThread())
            {
                try
                {
                    // Initialize from this client's own map-event visibility, not the server's. The server
                    // force-spots every party (no main party) so its value is always visible; map-event icon
                    // visibility is local (see MapEventVisibilityClientPatch), and the vanilla IsVisible setter
                    // keeps the visual in lock-step, so seeding the visual from the local value keeps the icon
                    // and battle sound consistent here instead of starting in the server-visible state.
                    visual.Initialize(position, visual.MapEvent?.IsVisible ?? false);

                    // Client visuals are built via SkipConstructor, bypassing vanilla CreateMapEventVisual,
                    // so they never land in the creator's _listOfEvents that the map screen rebuilds icons
                    // from. Register it so the icon survives a screen rebuild (e.g. returning from a battle).
                    RegisterForScreenRebuild(visual);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to initialize GauntletMapEventVisual with InstanceId {InstanceId}", instanceId);
                }
            }
        });
    }

    private static readonly FieldInfo ListOfEventsField =
        AccessTools.Field(typeof(GauntletMapEventVisualCreator), "_listOfEvents");

    // Adds the visual to the creator's private _listOfEvents (what vanilla CreateMapEventVisual does),
    // so GauntletMapEventVisualsView.CreateLayout can rebuild the icon after the map screen recreates.
    private static void RegisterForScreenRebuild(GauntletMapEventVisual visual)
    {
        if (Campaign.Current?.VisualCreator?.MapEventVisualCreator is not GauntletMapEventVisualCreator creator)
            return;

        if (ListOfEventsField.GetValue(creator) is not List<GauntletMapEventVisual> events)
            return;

        if (!events.Contains(visual))
            events.Add(visual);
    }
}
