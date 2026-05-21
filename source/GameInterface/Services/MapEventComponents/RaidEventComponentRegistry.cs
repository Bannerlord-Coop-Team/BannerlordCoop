using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents;


internal class RaidEventComponentRegistry : AutoRegistryBase<RaidEventComponent>
{
    public RaidEventComponentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(RaidEventComponent), new Type[] { typeof(MapEvent) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void OnClientCreated(RaidEventComponent obj, string id)
    {
    }

    public override void OnClientDestroyed(RaidEventComponent obj, string id)
    {
    }

    public override void OnServerCreated(RaidEventComponent obj, string id)
    {
    }

    public override void OnServerDestroyed(RaidEventComponent obj, string id)
    {
    }

    public override void RegisterAllObjects()
    {
    }
}
