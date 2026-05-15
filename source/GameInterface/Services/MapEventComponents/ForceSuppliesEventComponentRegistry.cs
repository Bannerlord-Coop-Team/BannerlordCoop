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

namespace GameInterface.Services.ForceSuppliesEventComponents;


internal class ForceSuppliesEventComponentRegistry : AutoRegistryBase<ForceSuppliesEventComponent>
{
    public ForceSuppliesEventComponentRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[]
    {
        AccessTools.Constructor(typeof(ForceSuppliesEventComponent), new Type[] { typeof(MapEvent) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void OnClientCreated(ForceSuppliesEventComponent obj, string id)
    {
    }

    public override void OnClientDestroyed(ForceSuppliesEventComponent obj, string id)
    {
    }

    public override void OnServerCreated(ForceSuppliesEventComponent obj, string id)
    {
    }

    public override void OnServerDestroyed(ForceSuppliesEventComponent obj, string id)
    {
    }

    public override void RegisterAllObjects()
    {
    }
}
