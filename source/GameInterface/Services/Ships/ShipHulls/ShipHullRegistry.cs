using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Ships.ShipHulls;

internal class ShipHullRegistry : AutoRegistryBase<ShipHull>
{
    public ShipHullRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(ShipHull));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var shipHull in MBObjectManager.Instance.GetObjects<ShipHull>(x => true))
        {
            RegisterExistingObject(shipHull.StringId, shipHull);
        }
    }

    public override void OnClientCreated(ShipHull obj, string id)
    {
    }

    public override void OnClientDestroyed(ShipHull obj, string id)
    {
    }

    public override void OnServerCreated(ShipHull obj, string id)
    {
    }

    public override void OnServerDestroyed(ShipHull obj, string id)
    {
    }
}
