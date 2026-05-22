using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.WeaponDesigns;

internal class WeaponDesignRegistry : AutoRegistryBase<WeaponDesign>
{
    public WeaponDesignRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(WeaponDesign));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var itemObject in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
        {
            objectManager.AddNewObject(itemObject.WeaponDesign, out var _);
        }
    }

    public override void OnClientCreated(WeaponDesign obj, string id)
    {
    }

    public override void OnClientDestroyed(WeaponDesign obj, string id)
    {
    }

    public override void OnServerCreated(WeaponDesign obj, string id)
    {
    }

    public override void OnServerDestroyed(WeaponDesign obj, string id)
    {
    }
}