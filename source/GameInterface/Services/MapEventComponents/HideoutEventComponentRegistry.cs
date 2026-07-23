using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents;


internal class HideoutEventComponentRegistry : AutoRegistryBase<HideoutEventComponent>
{
    public HideoutEventComponentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(HideoutEventComponent), new Type[] { typeof(MapEvent), typeof(bool) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void OnClientCreated(HideoutEventComponent obj, string id)
    {
    }

    public override void OnClientDestroyed(HideoutEventComponent obj, string id)
    {
    }

    public override void OnServerCreated(HideoutEventComponent obj, string id)
    {
    }

    public override void OnServerDestroyed(HideoutEventComponent obj, string id)
    {
    }

    public override void RegisterAllObjects()
    {
    }
}
