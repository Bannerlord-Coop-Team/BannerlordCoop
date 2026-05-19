using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents;


internal class MapEventComponentsRegistry : AutoRegistryBase<MapEventComponent>
{
    public MapEventComponentsRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void OnClientCreated(MapEventComponent obj, string id)
    {
    }

    public override void OnClientDestroyed(MapEventComponent obj, string id)
    {
    }

    public override void OnServerCreated(MapEventComponent obj, string id)
    {
    }

    public override void OnServerDestroyed(MapEventComponent obj, string id)
    {
    }

    public override void RegisterAllObjects()
    {
        foreach (var mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            if (mapEvent.StringId == null) {
                Logger.Warning("MapEvent with null StringId found, skipping registration of its components");
                continue;
            }

            if (mapEvent.Component == null)
            {
                Logger.Warning("MapEvent with StringId {StringId} has null Component, skipping registration", mapEvent.StringId);
                continue;
            }

            RegisterExistingObject(mapEvent.StringId, mapEvent.Component);
        }
    }
}
