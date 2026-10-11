using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Ships;

/// <summary>
/// Registry for the War Sails <see cref="ShipHull"/> catalog, keyed by StringId so ship messages can address hulls.
/// </summary>
internal class ShipHullRegistry : AutoRegistryBase<ShipHull>
{
    public ShipHullRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    // XML catalog loaded identically on every machine, never created mid-session.
    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var hull in MBObjectManager.Instance.GetObjectTypeList<ShipHull>())
        {
            RegisterExistingObject(hull.StringId, hull);
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
