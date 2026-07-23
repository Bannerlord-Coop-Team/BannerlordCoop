using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents;


internal class SiegeAmbushEventComponentRegistry : AutoRegistryBase<SiegeAmbushEventComponent>
{
    public SiegeAmbushEventComponentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(SiegeAmbushEventComponent), new Type[] { typeof(MapEvent) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void OnClientCreated(SiegeAmbushEventComponent obj, string id)
    {
    }

    public override void OnClientDestroyed(SiegeAmbushEventComponent obj, string id)
    {
    }

    public override void OnServerCreated(SiegeAmbushEventComponent obj, string id)
    {
    }

    public override void OnServerDestroyed(SiegeAmbushEventComponent obj, string id)
    {
    }

    public override void RegisterAllObjects()
    {
    }
}
