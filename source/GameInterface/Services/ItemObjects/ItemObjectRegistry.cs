using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.ItemObjects;

public class ItemObjectRegistry : AutoRegistryBase<ItemObject>
{
    public ItemObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(ItemObject));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        // Must order by string id as this is not deterministic on load
        foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>().OrderBy(i => i.StringId))
        {
            RegisterExistingObject(item.StringId, item);
        }
    }

    public override void OnClientCreated(ItemObject obj, string id)
    {
    }

    public override void OnClientDestroyed(ItemObject obj, string id)
    {
    }

    public override void OnServerCreated(ItemObject obj, string id)
    {
    }

    public override void OnServerDestroyed(ItemObject obj, string id)
    {
    }
}
