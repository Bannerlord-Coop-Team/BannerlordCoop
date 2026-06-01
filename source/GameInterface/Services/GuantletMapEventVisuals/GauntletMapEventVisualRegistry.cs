using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.GuantletMapEventVisuals;

internal class GauntletMapEventVisualRegistry : AutoRegistryBase<GauntletMapEventVisual>
{
    public override bool Debug => true;
    public GauntletMapEventVisualRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(GauntletMapEventVisual));

    public override IEnumerable<MethodBase> DestroyMethods => new MethodBase[]
    {
        AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.OnMapEventEnd))
    };

    public override void RegisterAllObjects()
    {
        foreach (var mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            if (mapEvent.StringId == null) continue;

            if (mapEvent.MapEventVisual is not GauntletMapEventVisual mapEventVisual) continue;

            RegisterExistingObject(mapEvent.StringId, mapEventVisual);
        }
    }

    private static readonly FieldInfo OnDeactivateField =
        AccessTools.Field(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual._onDeactivate));

    private static readonly FieldInfo OnInitializedField =
        AccessTools.Field(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual._onInitialized));

    private static readonly FieldInfo OnVisibilityChangedField =
        AccessTools.Field(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual._onVisibilityChanged));

    public override void OnClientCreated(GauntletMapEventVisual visual, string id)
    {
        var visualCreator = Campaign.Current.VisualCreator.MapEventVisualCreator as GauntletMapEventVisualCreator;

        OnInitializedField.SetValue(
            visual,
            new Action<GauntletMapEventVisual>(visualCreator.OnMapEventInitialized));

        OnVisibilityChangedField.SetValue(
            visual,
            new Action<GauntletMapEventVisual>(visualCreator.OnMapEventVisibilityChanged));

        OnDeactivateField.SetValue(
            visual,
            new Action<GauntletMapEventVisual>(visualCreator.OnMapEventOver));
    }

    public override void OnClientDestroyed(GauntletMapEventVisual obj, string id)
    {
        obj.OnMapEventEnd();
    }

    public override void OnServerCreated(GauntletMapEventVisual obj, string id)
    {
    }

    public override void OnServerDestroyed(GauntletMapEventVisual obj, string id)
    {
    }
}
