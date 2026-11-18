using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents;


internal class SiegeSallyOutEventComponentRegistry : AutoRegistryBase<SiegeSallyOutEventComponent>
{
    public SiegeSallyOutEventComponentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(SiegeSallyOutEventComponent), new Type[] { typeof(MapEvent) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void OnClientCreated(SiegeSallyOutEventComponent obj, string id)
    {
    }

    public override void OnClientDestroyed(SiegeSallyOutEventComponent obj, string id)
    {
    }

    public override void OnServerCreated(SiegeSallyOutEventComponent obj, string id)
    {
    }

    public override void OnServerDestroyed(SiegeSallyOutEventComponent obj, string id)
    {
    }

    public override void RegisterAllObjects()
    {
    }
}
