using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Ships.ShipSlots;

internal class ShipSlotRegistry : AutoRegistryBase<ShipSlot>
{
    public ShipSlotRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(ShipSlot));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var shipSlot in MBObjectManager.Instance.GetObjects<ShipSlot>(x => true))
        {
            RegisterExistingObject(shipSlot.StringId, shipSlot);
        }
    }

    public override void OnClientCreated(ShipSlot obj, string id)
    {
    }

    public override void OnClientDestroyed(ShipSlot obj, string id)
    {
    }

    public override void OnServerCreated(ShipSlot obj, string id)
    {
    }

    public override void OnServerDestroyed(ShipSlot obj, string id)
    {
    }
}
