using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents;


internal class SiegeOutsideEventComponentRegistry : AutoRegistryBase<SiegeOutsideEventComponent>
{
    public SiegeOutsideEventComponentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(SiegeOutsideEventComponent), new Type[] { typeof(MapEvent) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void OnClientCreated(SiegeOutsideEventComponent obj, string id)
    {
    }

    public override void OnClientDestroyed(SiegeOutsideEventComponent obj, string id)
    {
    }

    public override void OnServerCreated(SiegeOutsideEventComponent obj, string id)
    {
    }

    public override void OnServerDestroyed(SiegeOutsideEventComponent obj, string id)
    {
    }

    public override void RegisterAllObjects()
    {
    }
}
