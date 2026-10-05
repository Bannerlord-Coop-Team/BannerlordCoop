using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents;


internal class SiegeAssaultEventComponentRegistry : AutoRegistryBase<SiegeAssaultEventComponent>
{
    public SiegeAssaultEventComponentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(SiegeAssaultEventComponent), new Type[] { typeof(MapEvent) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void OnClientCreated(SiegeAssaultEventComponent obj, string id)
    {
    }

    public override void OnClientDestroyed(SiegeAssaultEventComponent obj, string id)
    {
    }

    public override void OnServerCreated(SiegeAssaultEventComponent obj, string id)
    {
    }

    public override void OnServerDestroyed(SiegeAssaultEventComponent obj, string id)
    {
    }

    public override void RegisterAllObjects()
    {
    }
}
